using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Buildings;
using HardModePatch.Core;
using HarmonyLib;
using Helpers;

namespace HardModePatch
{
    // Our own balance rule, not a patch to another author's mod. The market rent the rent screen shows, the
    // deposit (the game multiplies it by its usual days) and the rent stored at signing all come from
    // Building.GetBuildingDailyMarketRent, so one postfix keeps what the player sees and pays the same. A lease
    // already signed keeps its rent. Rivals' weekly estimates subtract a week of the same market rent; while one
    // runs for a business the player neither rents nor owns, the game's rent stands.
    // Keep every patched method in ResolveHooks, also exercised by the offline HookCheck.
    public static class RentRebalance
    {
        static readonly Harmony harmony = new Harmony(GameplayFeatures.HarmonyId + ".rent");
        [ThreadStatic] static int rivalEstimates;
        public static bool Available { get; private set; }

        public static IList<MethodBase> ResolveHooks()
        {
            var rent = AccessTools.DeclaredMethod(typeof(Building), "GetBuildingDailyMarketRent", Type.EmptyTypes);
            var estimate = AccessTools.DeclaredMethod(typeof(EstimatedWeeklyIncomeHelper), "GetEstimatedWeeklyIncome");
            return new MethodBase[]
            {
                rent != null && rent.ReturnType == typeof(int) ? rent : null,
                estimate != null && estimate.IsStatic && estimate.GetParameters().Length > 0
                    && estimate.GetParameters()[0].ParameterType == typeof(BuildingRegistration) ? estimate : null
            };
        }

        public static void ApplyHooks(Harmony owner, IList<MethodBase> hooks)
        {
            owner.Patch(hooks[0], postfix: new HarmonyMethod(typeof(RentRebalance), "RentPostfix"));
            owner.Patch(hooks[1], prefix: new HarmonyMethod(typeof(RentRebalance), "EstimatePrefix"),
                finalizer: new HarmonyMethod(typeof(RentRebalance), "EstimateFinalizer"));
        }

        public static void Start()
        {
            try
            {
                var hooks = ResolveHooks();
                if (hooks.Any(h => h == null)) throw new MissingMethodException("Rent hooks changed.");
                ApplyHooks(harmony, hooks);
                Available = true;
            }
            catch (Exception ex)
            {
                harmony.UnpatchAll(harmony.Id);
                Available = false;
                Log.Error("Rent rebalance unavailable; the game's rents apply: " + ex);
            }
        }

        public static void Stop()
        {
            harmony.UnpatchAll(harmony.Id);
            Available = false;
        }

        static void RentPostfix(Building __instance, ref int __result)
        {
            try
            {
                if (!Rules.RentRebalance.Enabled || rivalEstimates > 0 || ReferenceEquals(__instance, null)) return;
                __result = RentRules.Scale(__result, __instance.BuildingType);
            }
            catch (Exception ex) { Log.Error("Rent rebalance: " + ex.Message); }
        }

        static void EstimatePrefix(BuildingRegistration registration, out bool __state)
        {
            __state = false;
            try
            {
                if (registration != null && RentRules.KeepGameRentInEstimate(registration.RentedByPlayer, registration.BuildingOwnedByPlayer))
                {
                    rivalEstimates++;
                    __state = true;
                }
            }
            catch (Exception ex) { Log.Error("Rent rebalance estimate: " + ex.Message); }
        }

        static Exception EstimateFinalizer(Exception __exception, bool __state)
        {
            if (__state && rivalEstimates > 0) rivalEstimates--;
            return __exception;
        }
    }
}
