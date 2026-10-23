using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BAModAPI.Services;
using HardModePatch.Core;
using HarmonyLib;
using Helpers;

namespace HardModePatch
{
    // Our own balance rule (Core/HappinessRules). Three hooks on the game's HappinessHelper:
    // - OnHappinessModifiersLoaded: set the listed vanilla modifiers' amount and hours (RLO adds its own types later
    //   and they are never in the list);
    // - AddModifier: an outside activity's result gets its whole duration from one visit;
    // - UpdateHappiness: "no home" counts -15 instead of -50 while RLO says a hotel room is booked. RLO is read by
    //   name, never patched; without it the game's -50 stands. The phone shows the amount the last update left, and
//   the game updates only on the hour or a new mood, so a booking that changes in between (a load, a check-in,
//   checkout) is caught once a second and the update is run then.
    // The game's values are kept, so switching the rule off puts them back at once.
    // Keep every patched method in ResolveHooks, also exercised by the offline checks.
    public static class HappinessRebalance
    {
        static readonly Harmony harmony = new Harmony(GameplayFeatures.HarmonyId + ".happiness");
        static readonly Dictionary<string, int[]> gameValues = new Dictionary<string, int[]>();
        static FieldInfo registry;
        static Func<bool> hotelBooked;
        static bool hotelLookupDone;
        static float nextLook;
        public static bool Available { get; private set; }

        public static IList<MethodBase> ResolveHooks()
        {
            var loaded = AccessTools.DeclaredMethod(typeof(HappinessHelper), "OnHappinessModifiersLoaded", new[] { typeof(IList<HappinessModifier>) });
            var add = AccessTools.DeclaredMethod(typeof(HappinessHelper), "AddModifier", new[] { typeof(string), typeof(int), typeof(bool) });
            var update = AccessTools.DeclaredMethod(typeof(HappinessHelper), "UpdateHappiness", Type.EmptyTypes);
            return new MethodBase[] { loaded, add, update };
        }

        public static void ApplyHooks(Harmony owner, IList<MethodBase> hooks)
        {
            owner.Patch(hooks[0], postfix: new HarmonyMethod(typeof(HappinessRebalance), "LoadedPostfix"));
            owner.Patch(hooks[1], prefix: new HarmonyMethod(typeof(HappinessRebalance), "AddPrefix"));
            owner.Patch(hooks[2], prefix: new HarmonyMethod(typeof(HappinessRebalance), "UpdatePrefix"));
        }

        public static void Start()
        {
            try
            {
                var hooks = ResolveHooks();
                registry = AccessTools.Field(typeof(HappinessHelper), "Modifiers");
                if (hooks.Any(h => h == null) || registry == null) throw new MissingMethodException("Happiness hooks changed.");
                ApplyHooks(harmony, hooks);
                Rules.HappinessRebalance.Changed += OnRuleChanged;
                UnityLifecycleProvider.OnUpdate += WatchBooking;
                Available = true;
            }
            catch (Exception ex)
            {
                harmony.UnpatchAll(harmony.Id);
                Available = false;
                Log.Error("Happiness rebalance unavailable; the game's happiness applies: " + ex);
            }
        }

        public static void Stop()
        {
            Rules.HappinessRebalance.Changed -= OnRuleChanged;
            UnityLifecycleProvider.OnUpdate -= WatchBooking;
            harmony.UnpatchAll(harmony.Id);
            Restore(Registered());
            Available = false;
        }

        static IEnumerable<HappinessModifier> Registered()
        {
            var dict = registry == null ? null : registry.GetValue(null) as Dictionary<string, HappinessModifier>;
            return dict == null ? Enumerable.Empty<HappinessModifier>() : dict.Values.ToList();
        }

        static void LoadedPostfix(IList<HappinessModifier> modifiers)
        {
            try
            {
                if (modifiers == null) return;
                foreach (var m in modifiers)
                {
                    if (ReferenceEquals(m, null) || m.type == null) continue;
                    if (!gameValues.ContainsKey(m.type)) gameValues[m.type] = new[] { m.amount, m.hoursDuration, m.maxHoursDuration };
                }
                if (Rules.HappinessRebalance.Enabled) Apply(modifiers);
            }
            catch (Exception ex) { Log.Error("Happiness rebalance load: " + ex.Message); }
        }

        static void OnRuleChanged(bool enabled) { Reapply(); }

        static void Reapply()
        {
            try
            {
                var all = Registered().ToList();
                Restore(all);
                if (Rules.HappinessRebalance.Enabled) Apply(all);
                HappinessHelper.UpdateHappiness();
            }
            catch (Exception ex) { Log.Error("Happiness rebalance switch: " + ex.Message); }
        }

        static void Apply(IEnumerable<HappinessModifier> modifiers)
        {
            int changed = 0;
            foreach (var m in modifiers)
            {
                var value = ReferenceEquals(m, null) ? null : HappinessRules.Find(m.type);
                if (value == null) continue;
                m.amount = value.Amount;
                if (value.Hours != -1) m.hoursDuration = value.Hours;
                if (value.MaxHours != -1) m.maxHoursDuration = value.MaxHours;
                changed++;
            }
            if (changed > 0) Log.Info("Happiness rebalance: " + changed + " of the game's modifiers set.");
        }

        static void Restore(IEnumerable<HappinessModifier> modifiers)
        {
            foreach (var m in modifiers)
            {
                int[] game;
                if (ReferenceEquals(m, null) || m.type == null || !gameValues.TryGetValue(m.type, out game)) continue;
                m.amount = game[0];
                m.hoursDuration = game[1];
                m.maxHoursDuration = game[2];
            }
        }

        static void AddPrefix(string type, ref int customHoursDuration, ref bool additiveHours)
        {
            try
            {
                if (!Rules.HappinessRebalance.Enabled) return;
                var value = HappinessRules.Find(type);
                if (value == null || !value.Outside) return;
                customHoursDuration = HappinessRules.GrantedHours(type, customHoursDuration);
                additiveHours = false;
            }
            catch (Exception ex) { Log.Error("Happiness rebalance grant: " + ex.Message); }
        }

        static void UpdatePrefix()
        {
            try
            {
                var dict = registry.GetValue(null) as Dictionary<string, HappinessModifier>;
                HappinessModifier noHome;
                int[] game;
                if (dict == null || !dict.TryGetValue(HappinessRules.NoHome, out noHome) || ReferenceEquals(noHome, null)
                    || !gameValues.TryGetValue(HappinessRules.NoHome, out game)) return;
                bool booked = Rules.HappinessRebalance.Enabled && HotelBooked();
                noHome.amount = HappinessRules.NoHomeAmount(game[0], booked);
            }
            catch (Exception ex) { Log.Error("Happiness rebalance no-home: " + ex.Message); }
        }

        // Runs the game's own update when "no home" no longer matches the booking; the update's prefix sets the amount.
        static void WatchBooking()
        {
            try
            {
                if (UnityEngine.Time.unscaledTime < nextLook) return;
                nextLook = UnityEngine.Time.unscaledTime + 1f;
                var save = SaveGameManager.Current;
                if (!Rules.HappinessRebalance.Enabled || save == null || save.gameVariables == null || save.gameVariables.disableHappiness) return;
                var dict = registry.GetValue(null) as Dictionary<string, HappinessModifier>;
                HappinessModifier noHome;
                int[] game;
                if (dict == null || !dict.TryGetValue(HappinessRules.NoHome, out noHome) || ReferenceEquals(noHome, null)
                    || !gameValues.TryGetValue(HappinessRules.NoHome, out game)) return;
                bool booked = HotelBooked();
                if (noHome.amount == HappinessRules.NoHomeAmount(game[0], booked)) return;
                HappinessHelper.UpdateHappiness();
                Log.Info("Happiness rebalance: 'no home' is now " + noHome.amount + (booked ? " (hotel room booked)." : " (no hotel room)."));
            }
            catch (Exception ex)
            {
                UnityLifecycleProvider.OnUpdate -= WatchBooking;
                Log.Error("Happiness rebalance booking watch stopped: " + ex.Message);
            }
        }

        static bool HotelBooked()
        {
            if (!hotelLookupDone)
            {
                hotelLookupDone = true;
                var type = AccessTools.TypeByName("RealLifeOverhaul.TravelState");
                // An unreviewed RLO build: RloGate has said so once; 'no home' keeps the game's value.
                if (type != null && !RloGate.Accepts(type.Assembly)) return false;
                var getter = type == null ? null : AccessTools.PropertyGetter(type, "HasRoom");
                if (getter != null && getter.IsStatic && getter.ReturnType == typeof(bool))
                    hotelBooked = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
                else Log.Warn("Happiness rebalance: RLO's hotel booking not found; 'no home' stays at the game's value.");
            }
            return hotelBooked != null && hotelBooked();
        }
    }
}
