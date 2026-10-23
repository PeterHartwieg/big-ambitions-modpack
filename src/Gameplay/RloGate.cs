using System;
using System.Reflection;
using HardModePatch.Core;
using HarmonyLib;
using UI.Notification;

namespace HardModePatch
{
    // Real Life Overhaul updates through the Workshop on its own schedule, so viewers get each release the day it ships.
    // Everything HMP does to or with RLO (factory diplomas, running costs, owner job pay, the hotel "no home" rule) runs
    // only on an RLO build whose code was reviewed for it (Core/Targets). Any other build gets one log line and, when a
    // save loads, one notice; those features stay off and RLO runs as its author published it. Such an RLO drops HMP's
    // diploma records from a save the next time it saves, so they are copied to HMP's own save line first and given back
    // when a reviewed RLO and HMP's courses are loaded again (Core/DiplomaHold).
    public static class RloGate
    {
        static readonly Harmony harmony = new Harmony(GameplayFeatures.HarmonyId + ".rlogate");
        static readonly object gate = new object();
        static Assembly seen;
        static bool accepted, noticeShown;

        public static string Message { get; private set; }

        // An RLO assembly was seen and it isn't a reviewed build.
        public static bool Refused { get { lock (gate) return seen != null && !accepted; } }

        public static MethodBase ResolveHook()
        {
            var loaded = AccessTools.DeclaredMethod(typeof(GlobalEvents), "InvokeOnGameLoaded", Type.EmptyTypes);
            return loaded != null && loaded.IsStatic && loaded.ReturnType == typeof(void) ? loaded : null;
        }

        public static void Start()
        {
            try
            {
                var hook = ResolveHook();
                if (hook == null) throw new MissingMethodException("GlobalEvents.InvokeOnGameLoaded changed.");
                harmony.Patch(hook, prefix: new HarmonyMethod(typeof(RloGate), "GameLoadedPrefix"));
            }
            catch (Exception ex)
            {
                harmony.UnpatchAll(harmony.Id);
                Log.Error("Real Life Overhaul check on save load unavailable; held diploma records are not given back this session: " + ex.Message);
            }
        }

        public static void Stop()
        {
            harmony.UnpatchAll(harmony.Id);
        }

        // Decided once per RLO assembly, logged once.
        public static bool Accepts(Assembly rlo)
        {
            if (rlo == null) return false;
            lock (gate)
            {
                if (ReferenceEquals(rlo, seen)) return accepted;
                seen = rlo;
                string hash = Patcher.HashOf(rlo);
                accepted = TargetGate.Reviewed(hash, Targets.RealLifeOverhaul.VerifiedHashes);
                var version = rlo.GetName().Version;
                string described = "Real Life Overhaul " + (version == null ? "?" : version.ToString(3)) + " (" + (hash ?? "hash unreadable") + ")";
                if (accepted)
                {
                    Message = null;
                    Log.Info(described + ": a reviewed version; factory diplomas, running costs, owner job pay, utility closure reasons, night chirping mute and the hotel rule can run.");
                }
                else
                {
                    Message = described + " is a version HART. Mod Pack hasn't reviewed. Factory diplomas, running costs, owner job pay"
                        + ", utility closure reasons, night chirping mute and the hotel 'no home' rule are off until HART. Mod Pack is updated; RLO runs as its author published it."
                        + " A save's factory diploma records are kept for that update.";
                    Log.Warn(Message);
                }
                return accepted;
            }
        }

        static void GameLoadedPrefix()
        {
            try { OnGameLoaded(SaveGameManager.Current); }
            catch (Exception ex) { Log.Error("Real Life Overhaul check on save load failed: " + ex); }
        }

        // Runs before game-loaded subscribers, so before RLO's careers director first reads the save (a timer after load).
        internal static bool OnGameLoaded(GameInstance game)
        {
            bool refused;
            Assembly rlo;
            lock (gate)
            {
                if (seen == null) return false;
                refused = !accepted;
                rlo = seen;
            }
            // Game-loaded also fires when character creation opens, before the city exists; nothing is read then.
            if (game == null || game.BuildingRegistrations == null || game.BuildingRegistrations.Count == 0) return false;
            var data = game.modData;
            string line = null, held = null;
            if (data != null)
            {
                data.TryGetValue(DiplomaHold.CareersKey, out line);
                data.TryGetValue(DiplomaHold.HoldKey, out held);
            }
            // RLO can't read our courses: an unreviewed build, or a reviewed one whose catalogue didn't take them.
            if (refused || !RealLifeOverhaulPatch.CoursesRegistered)
            {
                string kept = DiplomaHold.Keep(line, held, game.Day);
                if (kept != null && kept != held)
                {
                    data[DiplomaHold.HoldKey] = kept;
                    Log.Info("Factory diplomas: " + DiplomaHold.Count(kept) + " diploma record(s) of this save held until Real Life Overhaul can read them again.");
                }
                if (refused && (kept != null || RloRulesOn())) Notice();
                return kept != null || refused;
            }
            if (held == null) return false;
            var settled = DiplomaHold.Restore(line, held, game.Day, Limit(rlo, "RealLifeOverhaul.Careers.StudyRules", "LiveMost", 4),
                Limit(rlo, "RealLifeOverhaul.Careers.StudyFile", "ResultsMost", 40));
            if (settled == null)
            {
                Log.Warn("Factory diplomas: held diploma records can't be given back yet (no careers record in this save, or another format); they stay held.");
                return false;
            }
            if (settled.Careers != null) data[DiplomaHold.CareersKey] = settled.Careers;
            if (settled.Done)
            {
                data.Remove(DiplomaHold.HoldKey);
                Log.Info("Factory diplomas: held diploma records settled; " + settled.Records + " record(s) given back or moved on by the weeks they were held. The hold is cleared.");
                return true;
            }
            data[DiplomaHold.HoldKey] = settled.Hold;
            Log.Warn("Factory diplomas: " + DiplomaHold.Count(settled.Hold) + " held diploma record(s) can't be given back yet (" + settled.Waiting + "); they stay held"
                + (settled.Records > 0 ? ", and " + settled.Records + " record(s) still in RLO's line were moved on" : "") + ".");
            return true;
        }

        // RLO's own limit, or the 0.49.0 value when it can't be read.
        static int Limit(Assembly rlo, string type, string name, int otherwise)
        {
            try
            {
                var field = rlo.GetType(type, false)?.GetField(name, BindingFlags.Public | BindingFlags.Static);
                return field != null && field.IsLiteral && field.FieldType == typeof(int) ? (int)field.GetRawConstantValue() : otherwise;
            }
            catch (Exception) { return otherwise; }
        }

        // A player who uses none of the RLO rules doesn't need to hear that they are off.
        static bool RloRulesOn()
        {
            return Rules.FactoryDiplomas.Enabled || Rules.EquipmentUtilities.Enabled || Rules.OfficeIt.Enabled
                || Rules.OwnerJobFullPay.Enabled || Rules.HappinessRebalance.Enabled || Rules.MuteRloNightChirping.Enabled;
        }

        static void Notice()
        {
            lock (gate)
            {
                if (noticeShown) return;
                noticeShown = true;
            }
            try { Notifications.Show(NotificationType.Warning, "hardmode_rlo_unreviewed", null, 15f, "HmpRloUnreviewed"); }
            catch (Exception ex) { Log.Error("Could not show the Real Life Overhaul notice: " + ex.Message); }
        }
    }
}
