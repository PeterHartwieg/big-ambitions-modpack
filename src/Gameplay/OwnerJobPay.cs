using System;
using System.Linq;
using System.Reflection;
using HardModePatch.Core;
using HarmonyLib;

namespace HardModePatch
{
    // Our own balance rule: Real Life Overhaul pays every job and gig 15% less for each business the player runs
    // (CareerCatalogue.OwnerFactor, never under 55%). With the rule on, that factor is 1: the player's own labour
    // pays the same whether or not they own a business. RLO is patched by name once its assembly is loaded, only in a
    // reviewed RLO build (RloGate); if OwnerFactor(int) is not there as reviewed, nothing is patched and the cut stays.
    public static class OwnerJobPay
    {
        static readonly Harmony harmony = new Harmony(GameplayFeatures.HarmonyId + ".ownerpay");
        static bool patched;
        public static bool Available { get; private set; }

        public static void Start()
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) TryPatch(assembly);
        }

        public static void Stop()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            harmony.UnpatchAll(harmony.Id);
            patched = false;
            Available = false;
        }

        static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            TryPatch(args.LoadedAssembly);
        }

        static void TryPatch(Assembly assembly)
        {
            if (patched || assembly == null || assembly.GetName().Name != Targets.RealLifeOverhaul.AssemblyName) return;
            if (!RloGate.Accepts(assembly)) { patched = true; return; }
            try
            {
                var type = assembly.GetType("RealLifeOverhaul.CareerCatalogue");
                var method = type == null ? null : type.GetMethod("OwnerFactor", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int) }, null);
                if (method == null || method.ReturnType != typeof(float))
                {
                    Log.Warn("Owner job pay: RLO's CareerCatalogue.OwnerFactor not found; RLO's pay cut for owners stays.");
                    return;
                }
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(OwnerJobPay), "Prefix"));
                patched = true;
                Available = true;
                Log.Info("Owner job pay: RLO's OwnerFactor patched; rule " + (Rules.OwnerJobFullPay.Enabled ? "on (jobs pay in full)" : "off (RLO's cut applies)") + ".");
            }
            catch (Exception ex)
            {
                harmony.UnpatchAll(harmony.Id);
                Log.Error("Owner job pay unavailable; RLO's cut applies: " + ex.Message);
            }
        }

        static bool Prefix(ref float __result)
        {
            if (!Rules.OwnerJobFullPay.Enabled) return true;
            __result = 1f;
            return false;
        }
    }
}
