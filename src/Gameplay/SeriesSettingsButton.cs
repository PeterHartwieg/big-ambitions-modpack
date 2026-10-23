using System;
using System.Collections.Generic;
using System.Linq;
using BigAmbitions.Mods;
using HardModePatch.Core;
using UI.Notification;

namespace HardModePatch
{
    // "Apply HART series settings" on our options page. It runs only when pressed. For each value in
    // Core/SeriesSettings.Options it finds the registered option by its owner's assembly and its id, writes the value the
    // way the game's own option controls do (an int in PlayerPrefs under "m:<modId>:<optionId>", toggles 0 or 1), calls
    // the option's OnValueChanged when the value changes, and logs it. It writes only at the main menu. Anything it can't find
    // is skipped and listed. Then PlayerPrefs.Save and one notice asking for a restart, since mods read their options at start.
    public static class SeriesSettingsButton
    {
        public const string NoticeKey = "hardmode_series_applied", PartialKey = "hardmode_series_applied_partial", FailedKey = "hardmode_series_failed", MainMenuKey = "hardmode_series_main_menu";

        sealed class Live
        {
            public FoundOption Found;
            public ModOption Option;
            public ModOptions Page;
        }

        static string OwnerOf(Delegate callback)
        {
            if (callback == null || callback.Method == null || callback.Method.DeclaringType == null) return null;
            return callback.Method.DeclaringType.Assembly.GetName().Name;
        }

        static Delegate CallbackOf(ModOption option)
        {
            var toggle = option as ToggleOption;
            if (toggle != null) return toggle.OnValueChanged;
            var slider = option as SliderOption;
            if (slider != null) return slider.OnValueChanged;
            var dropdown = option as DropdownOption;
            return dropdown != null ? dropdown.OnValueChanged : null;
        }

        // Every persisted toggle, slider and dropdown the game has registered, with the assembly that registered it.
        // An option without a callback takes the owner of its page's other options.
        static List<Live> Registered()
        {
            var found = new List<Live>();
            foreach (var entry in OptionsService.RegisteredEntries)
            {
                var page = entry.Value;
                if (page == null || page.Options == null) continue;
                string pageOwner = page.Options.Select(o => OwnerOf(CallbackOf(o))).FirstOrDefault(o => o != null);
                foreach (var option in page.Options)
                {
                    if (option == null || string.IsNullOrEmpty(option.Id)) continue;
                    string owner = OwnerOf(CallbackOf(option)) ?? pageOwner;
                    FoundOption seen = null;
                    var slider = option as SliderOption;
                    var dropdown = option as DropdownOption;
                    if (option is ToggleOption) seen = new FoundOption(owner, entry.Key, option.Id, OptionKind.Toggle, 0, 1);
                    else if (slider != null) seen = new FoundOption(owner, entry.Key, option.Id, OptionKind.Slider, slider.Min, slider.Max);
                    else if (dropdown != null) seen = new FoundOption(owner, entry.Key, option.Id, OptionKind.Dropdown, 0, dropdown.ChoiceKeys.Length - 1);
                    if (seen != null) found.Add(new Live { Found = seen, Option = option, Page = page });
                }
            }
            return found;
        }

        static int Default(ModOption option)
        {
            var toggle = option as ToggleOption;
            if (toggle != null) return toggle.DefaultValue ? 1 : 0;
            var slider = option as SliderOption;
            if (slider != null) return slider.DefaultValue;
            var dropdown = option as DropdownOption;
            return dropdown != null ? dropdown.DefaultIndex : 0;
        }

        static void Invoke(ModOption option, int value)
        {
            var toggle = option as ToggleOption;
            if (toggle != null) { if (toggle.OnValueChanged != null) toggle.OnValueChanged(value != 0); return; }
            var slider = option as SliderOption;
            if (slider != null) { if (slider.OnValueChanged != null) slider.OnValueChanged(value); return; }
            var dropdown = option as DropdownOption;
            if (dropdown != null && dropdown.OnValueChanged != null) dropdown.OnValueChanged(value);
        }

        // What the button would write, without writing: "label -> key = value" or "label: skipped, reason". Used by
        // the offline SettingsCheck against the mods' real option pages.
        public static IList<string> Preview()
        {
            var foundList = Registered().Select(r => r.Found).ToList();
            var lines = new List<string>();
            foreach (var wanted in SeriesSettings.Options)
            {
                string reason;
                var found = SeriesSettings.Pick(wanted, foundList, out reason);
                lines.Add(found == null ? wanted.Label + ": skipped, " + reason : wanted.Label + " -> " + SeriesSettings.PrefKey(found.ModId, found.Id) + " = " + wanted.Value);
            }
            return lines;
        }

        // The main menu is open: its controller is registered (the game's InstanceBehavior singleton, unregistered on
        // destroy). SaveGameManager.Current is no guide, since the game keeps the last save there after returning.
        static bool MainMenuOpen() { return MainMenuController.IsInitialized; }

        static bool AtMainMenu()
        {
            try { return MainMenuOpen(); }
            catch (Exception)
            {
                var save = SaveGameManager.Current;
                return save == null || save.BuildingRegistrations == null || save.BuildingRegistrations.Count == 0;
            }
        }

        public static void Apply()
        {
            if (!GameplayFeatures.Active) return;
            try { Run(); }
            catch (Exception ex)
            {
                Log.Error("Series settings: stopped: " + ex);
                Notice(FailedKey);
            }
        }

        static void Run()
        {
            // In a game the Mods page runs every option's callback with the stored value each time it is opened, and some
            // callbacks act on the loaded save at once (RLO's clothes rule is kept per save, HMP's rules start charging).
            // So the button writes only at the main menu.
            if (!AtMainMenu())
            {
                Log.Info("Series settings: not at the main menu; nothing written. Press the button at the main menu.");
                Notice(MainMenuKey);
                return;
            }
            int changed = 0, same = 0;
            var skipped = new List<string>();
            List<Live> registered;
            try { registered = Registered(); }
            catch (Exception ex) { Log.Error("Series settings: could not read the registered mod options, nothing written: " + ex); Notice(FailedKey); return; }
            var foundList = registered.Select(r => r.Found).ToList();
            Log.Info("Series settings: applying " + SeriesSettings.Options.Length + " values.");
            foreach (var wanted in SeriesSettings.Options)
            {
                string reason;
                var found = SeriesSettings.Pick(wanted, foundList, out reason);
                if (found == null)
                {
                    skipped.Add(wanted.Label);
                    Log.Warn("Series settings: skipped " + wanted.Label + ": " + reason + ".");
                    continue;
                }
                var option = registered.First(r => ReferenceEquals(r.Found, found)).Option;
                string key = SeriesSettings.PrefKey(found.ModId, found.Id);
                try
                {
                    bool stored = UnityEngine.PlayerPrefs.HasKey(key);
                    int before = stored ? UnityEngine.PlayerPrefs.GetInt(key) : Default(option);
                    if (before == wanted.Value)
                    {
                        // Stored even when it is the mod's default, so a later default change doesn't move it.
                        if (!stored) UnityEngine.PlayerPrefs.SetInt(key, wanted.Value);
                        same++;
                        Log.Info("Series settings: " + wanted.Label + " already " + wanted.Value + (stored ? "." : " (the default, now stored)."));
                        continue;
                    }
                    UnityEngine.PlayerPrefs.SetInt(key, wanted.Value);
                    Invoke(option, wanted.Value);
                    changed++;
                    Log.Info("Series settings: " + wanted.Label + " " + before + " -> " + wanted.Value + " (" + wanted.Why + ").");
                }
                catch (Exception ex)
                {
                    skipped.Add(wanted.Label);
                    Log.Error("Series settings: " + wanted.Label + " failed after writing " + key + ": " + ex.Message);
                }
            }
            try { UnityEngine.PlayerPrefs.Save(); }
            catch (Exception ex) { Log.Error("Series settings: PlayerPrefs.Save failed; the values hold until the game quits normally: " + ex.Message); }
            // No page is registered again: that rebuilds every mod's controls and fires all their callbacks. An open
            // Mods page shows the new values once it is opened again.
            Log.Info("Series settings: " + changed + " changed, " + same + " already set, " + skipped.Count + " skipped"
                + (skipped.Count > 0 ? " (" + string.Join(", ", skipped.ToArray()) + ")" : "") + ". Restart the game to use them.");
            Notice(skipped.Count > 0 ? PartialKey : NoticeKey);
        }

        static void Notice(string key)
        {
            try { Notifications.Show(NotificationType.Info, key, null, 15f, "HmpSeriesSettings", null, true, false); }
            catch (Exception ex) { Log.Error("Could not show the series settings notice: " + ex.Message); }
        }
    }
}
