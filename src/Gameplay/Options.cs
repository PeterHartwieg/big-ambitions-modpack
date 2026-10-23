using System;
using System.Collections.Generic;
using System.Linq;
using BigAmbitions.Mods;
using HardModePatch.Core;

namespace HardModePatch
{
    // The native options page: one header per target mod with its patch state, then that mod's rules.
    // The game keeps each toggle in PlayerPrefs under "m:<modId>:<optionId>"; the rules read those at load
    // so they hold before the page is first opened, and the callbacks only carry later changes.
    static class Options
    {
        static string registeredId;
        static Patcher patcher;

        public static string PrefKey(string modId, string optionId) { return "m:" + modId + ":" + optionId; }

        public static void Load(string modId)
        {
            foreach (var rule in Rules.All)
            {
                string key = PrefKey(modId, rule.Id);
                rule.Enabled = UnityEngine.PlayerPrefs.HasKey(key) ? UnityEngine.PlayerPrefs.GetInt(key) != 0 : rule.Default;
            }
            Log.Info("Rules: " + string.Join(", ", Rules.All.Select(r => r.Id + "=" + (r.Enabled ? "on" : "off")).ToArray()) + ".");
        }

        public static void Register(string modId, Patcher source)
        {
            patcher = source;
            registeredId = modId;
            Refresh();
        }

        // Called again once a target has been inspected, so the page shows its state.
        public static void Refresh()
        {
            if (registeredId != null) HARTModPack.StatusPage.RefreshGameplay();
        }

        public static void Unregister()
        {
            if (registeredId == null) return;
            registeredId = null;
            patcher = null;
        }

        public static void AppendTo(ModOptions options)
        {
            options.AddSplitter().AddHeader("hardmode_series_header");
            options.AddButton(HARTModPack.Core.ButtonCaptions.ApplyLabelKey, SeriesSettingsButton.Apply);
            AddRule(options, Rules.SeriesPrefill);
            if (!CustomGamePrefill.Available) options.AddHeader("hardmode_series_prefill_unavailable");
            options.AddSplitter().AddHeader("hardmode_scenario_header");
            AddRule(options, Rules.IndebtedScenario);
            if (!IndebtedScenario.Available) options.AddHeader("hardmode_scenario_unavailable");
            options.AddSplitter().AddHeader("hardmode_rent_header");
            AddRule(options, Rules.RentRebalance);
            if (!RentRebalance.Available) options.AddHeader("hardmode_rent_unavailable");
            options.AddSplitter().AddHeader("hardmode_happiness_header");
            AddRule(options, Rules.HappinessRebalance);
            if (!HappinessRebalance.Available) options.AddHeader("hardmode_happiness_unavailable");
            options.AddSplitter().AddHeader("hardmode_ownerpay_header");
            AddRule(options, Rules.OwnerJobFullPay);
            if (!OwnerJobPay.Available) options.AddHeader("hardmode_ownerpay_unavailable");
            options.AddSplitter().AddHeader("hardmode_runningcosts_header");
            AddRule(options, Rules.EquipmentUtilities);
            AddRule(options, Rules.OfficeIt);
            AddRule(options, Rules.UtilityClosureReasons);
            if (!RunningCostsPatch.Available) options.AddHeader("hardmode_runningcosts_unavailable");
            options.AddSplitter().AddHeader("hardmode_nightsound_header");
            AddRule(options, Rules.MuteRloNightChirping);
            if (!NightChirpingPatch.Available) options.AddHeader("hardmode_nightsound_unavailable");
            foreach (var target in Rules.All.Where(r => r.Target != null).Select(r => r.Target).Distinct())
            {
                options.AddSplitter()
                    .AddHeader("hardmode_target_" + target.Key)
                    .AddHeader(StateKey(patcher == null ? TargetState.Missing : patcher.StateOf(target)));
                foreach (var rule in Rules.All.Where(r => r.Target == target))
                {
                    AddRule(options, rule);
                }
            }
        }

        static void AddRule(ModOptions options, Rule rule)
        {
            options.AddToggle(rule.Id, rule.LabelKey, rule.Default, value =>
            {
                if (!GameplayFeatures.Active || rule.Enabled == value) return;
                Log.Info(rule.Id + " turned " + (value ? "on" : "off") + ".");
                try
                {
                    rule.Set(value);
                    if (rule == Rules.FactoryDiplomas) RealLifeOverhaulPatch.ReportAvailability();
                }
                catch (Exception ex) { Log.Error(rule.Id + ": " + ex); }
            });
        }

        static string StateKey(TargetState state)
        {
            switch (state)
            {
                case TargetState.Verified: return "hardmode_state_verified";
                case TargetState.Unverified: return "hardmode_state_unverified";
                case TargetState.Incompatible: return "hardmode_state_incompatible";
                case TargetState.Refused: return "hardmode_state_refused";
                default: return "hardmode_state_missing";
            }
        }
    }
}
