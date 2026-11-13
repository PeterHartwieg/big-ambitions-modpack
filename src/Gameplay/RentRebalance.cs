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
        [ThreadStatic] static int rivalEstimates, livingReads;
        static readonly Harmony living = new Harmony(GameplayFeatures.HarmonyId + ".rent.living");
        static bool livingTried, livingBlocked;
        public static bool Available { get; private set; }

        public static IList<MethodBase> ResolveLivingReaders(Assembly rlo)
        {
            return RloLine.LivingRentReaders.Select(r =>
            {
                var type = rlo.GetType(r[0], false);
                var method = type == null ? null : AccessTools.DeclaredMethod(type, r[1]);
                return method != null && method.IsStatic ? (MethodBase)method : null;
            }).ToList();
        }

        public static void ApplyLivingReaders(Harmony owner, IList<MethodBase> readers)
        {
            foreach (var reader in readers.Where(r => r != null))
                owner.Patch(reader, prefix: new HarmonyMethod(typeof(RentRebalance), "LivingPrefix"),
                    finalizer: new HarmonyMethod(typeof(RentRebalance), "LivingFinalizer"));
        }

        static void OnLoad(object sender, AssemblyLoadEventArgs e) { TryLiving(e.LoadedAssembly); }

        static void TryLiving(Assembly a)
        {
            if (livingTried || a == null || a.GetName().Name != Targets.RealLifeOverhaul.AssemblyName) return;
            livingTried = true;
            if (!RloGate.Accepts(a)) return;
            try
            {
                var readers = ResolveLivingReaders(a);
                if (RloLine.AllLivingReadersNeeded(a.GetName().Version) && readers.Any(r => r == null))
                {
                    livingBlocked = true;
                    Log.Warn("Rent rebalance: Real Life Overhaul's living economy rent reads changed; the rule leaves every rent at the game's value with this RLO.");
                    return;
                }
                ApplyLivingReaders(living, readers);
            }
            catch (Exception ex)
            {
                living.UnpatchAll(living.Id);
                livingBlocked = true;
                Log.Error("Rent rebalance: Real Life Overhaul's living economy reads could not be separated; the rule leaves every rent at the game's value: " + ex.Message);
            }
        }

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
                AppDomain.CurrentDomain.AssemblyLoad += OnLoad;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) TryLiving(a);
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
            AppDomain.CurrentDomain.AssemblyLoad -= OnLoad;
            harmony.UnpatchAll(harmony.Id);
            living.UnpatchAll(living.Id);
            Available = livingTried = livingBlocked = false;
        }

        static void RentPostfix(Building __instance, ref int __result)
        {
            try
            {
                if (!Rules.RentRebalance.Enabled || rivalEstimates > 0 || livingReads > 0 || livingBlocked || ReferenceEquals(__instance, null)) return;
                __result = RentRules.Scale(__result, __instance.BuildingType);
            }
            catch (Exception ex) { Log.Error("Rent rebalance: " + ex.Message); }
        }

        static void LivingPrefix() { livingReads++; }

        static Exception LivingFinalizer(Exception __exception)
        {
            if (livingReads > 0) livingReads--;
            return __exception;
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
