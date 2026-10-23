using System;
using HARTModPack.Core;
using HarmonyLib;

// Keep the old internal namespaces and save keys so existing scenario and diploma records survive the move.
// This is compiled into HARTModPack.dll, with one entry point in HARTModPackMod.
namespace HardModePatch
{
    static class GameplayFeatures
    {
        public const string HarmonyId = "hartmodpack.gameplay";
        static readonly LegacyPatchGuard guard = new LegacyPatchGuard(Start, Stop, () => Log.Warn(Texts.LegacyPatchNotice));
        static Patcher patcher;
        static string modId;
        public static bool Active { get { return guard.Active; } }
        public static bool Blocked { get { return guard.Blocked; } }

        public static void Begin(string id)
        {
            modId = id;
            guard.Begin();
            HARTModPack.StatusPage.RefreshGameplay();
        }

        public static void End() { guard.Dispose(); }

        static void Start()
        {
            Options.Load(modId);
            RloGate.Start();
            CustomGamePrefill.Start();
            IndebtedScenario.Start();
            RentRebalance.Start();
            HappinessRebalance.Start();
            OwnerJobPay.Start();
            RunningCostsPatch.Start();
            UtilityClosureReasonPatch.Start();
            patcher = new Patcher(new Harmony(HarmonyId), new ITargetPatch[]
            {
                new HardcoreAmbitionsPatch(), new MeaningfulAmbiencePatch(), new BakeryPatch(),
                new InternetCafePatch(), new VendingMachinesPatch(), new EmpireBankPatch(), new HyperrealPatch(), new RealLifeOverhaulPatch()
            });
            patcher.Start();
            RealLifeOverhaulPatch.ReportAvailability();
            Options.Register(modId, patcher);
        }

        static void Stop()
        {
            // Every cleanup runs even if another fails. Only this pack's Harmony owners are removed.
            Cleanup(Options.Unregister);
            if (patcher != null) Cleanup(patcher.Stop);
            patcher = null;
            Cleanup(RloGate.Stop);
            Cleanup(CustomGamePrefill.Stop);
            Cleanup(IndebtedScenario.Stop);
            Cleanup(RentRebalance.Stop);
            Cleanup(HappinessRebalance.Stop);
            Cleanup(OwnerJobPay.Stop);
            Cleanup(RunningCostsPatch.Stop);
            Cleanup(UtilityClosureReasonPatch.Stop);
        }

        static void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception ex) { Log.Error("Stopping gameplay: " + ex); }
        }
    }

    static class Log
    {
        // Outside Unity the offline checks cannot call the engine logger.
        public static void Info(string message) { try { UnityEngine.Debug.Log("[HART. Mod Pack] " + message); } catch (System.Security.SecurityException) { } }
        public static void Warn(string message) { try { UnityEngine.Debug.LogWarning("[HART. Mod Pack] " + message); } catch (System.Security.SecurityException) { } }
        public static void Error(string message) { try { UnityEngine.Debug.LogError("[HART. Mod Pack] " + message); } catch (System.Security.SecurityException) { } }
    }
}
