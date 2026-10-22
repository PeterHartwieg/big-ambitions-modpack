using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigAmbitions.ModsInternal;
using BigAmbitions.Mods;
using HARTModPack.Core;
using Localizor;

namespace HARTModPack
{
    // Settings > Mods > HART. Mod Pack: a summary, a "Check again" button and one line per pack member.
    // Option labels are localisation keys, so the changing lines go into a small locale table the mod writes
    // under the game's data folder (never into a mod folder) and registers with the game's localiser.
    static class StatusPage
    {
        const string StatusLocaleId = "HARTModPack.status";
        static string modId;
        static List<string> lastLines;
        static bool tableRegistered;
        static bool watchingDiscovery;

        static string TableFolder
        {
            get { return Path.Combine(Path.Combine(UnityEngine.Application.persistentDataPath, "HARTModPack"), "StatusLocale"); }
        }

        public static void Register(string id)
        {
            modId = id;
            if (!watchingDiscovery)
            {
                // The game drops locale folders it didn't register itself whenever it rediscovers mods.
                ModDiscoveryRegistry.OnDiscoveryUpdated += ReRegisterTable;
                watchingDiscovery = true;
            }
            lastLines = null;
            Build(null, false);
        }

        public static void Unregister()
        {
            if (watchingDiscovery)
            {
                ModDiscoveryRegistry.OnDiscoveryUpdated -= ReRegisterTable;
                watchingDiscovery = false;
            }
            if (tableRegistered)
            {
                try { LocalizorManager.UnregisterExternalLocalization(StatusLocaleId, true); } catch (Exception) { }
                tableRegistered = false;
            }
            if (modId != null)
            {
                try { OptionsService.RemoveModOptions(modId); } catch (Exception) { }
            }
            modId = null;
        }

        // Rewrites the table and rebuilds the page only when a line changed. A rebuild while the page is open
        // re-runs every mod's option controls (the game does the same each time the page opens).
        public static void Show(string summary, List<string> memberLines, List<string> notes)
        {
            var lines = new List<string> { summary };
            lines.AddRange(memberLines);
            lines.AddRange(notes);
            if (lastLines != null && lastLines.SequenceEqual(lines)) return;
            if (!WriteTable(lines))
            {
                lastLines = null;
                Build(null, true);
                return;
            }
            lastLines = lines;
            var built = new List<string>();
            for (int i = 0; i < lines.Count; i++) built.Add(LocaleFile.Key(i));
            Build(built, false);
        }

        static bool WriteTable(List<string> lines)
        {
            try
            {
                string folder = TableFolder;
                Directory.CreateDirectory(folder);
                // Only the current languages get a copy; older copies would show stale lines after a language switch.
                foreach (string old in Directory.GetFiles(folder, "*.json")) File.Delete(old);
                string json = LocaleFile.Build(lines);
                File.WriteAllText(Path.Combine(folder, "en.json"), json);
                string locale = LocalizorManager.LoadedLocale;
                if (!string.IsNullOrEmpty(locale) && locale != "en") File.WriteAllText(Path.Combine(folder, locale + ".json"), json);
                LocalizorManager.RegisterExternalLocalization(StatusLocaleId, folder, true);
                tableRegistered = true;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't write the status lines for the Mods page; they are in Player.log only: " + ex.Message);
                return false;
            }
        }

        static void ReRegisterTable()
        {
            if (!tableRegistered) return;
            try { LocalizorManager.RegisterExternalLocalization(StatusLocaleId, TableFolder, true); }
            catch (Exception ex) { Log.Warn("Couldn't re-register the status lines: " + ex.Message); }
        }

        static void Build(List<string> statusKeys, bool unavailable)
        {
            if (modId == null) return;
            var options = new ModOptions().AddHeader(LocaleKeys.Title);
            if (statusKeys == null || statusKeys.Count == 0)
            {
                options.AddHeader(unavailable ? LocaleKeys.Unavailable : LocaleKeys.Checking);
                options.AddButton(LocaleKeys.CheckAgain, PackRunner.CheckAgain);
            }
            else
            {
                options.AddHeader(statusKeys[0]);
                options.AddButton(LocaleKeys.CheckAgain, PackRunner.CheckAgain);
                options.AddSplitter().AddHeader(LocaleKeys.MembersHeader);
                for (int i = 1; i < statusKeys.Count; i++) options.AddHeader(statusKeys[i]);
            }
            // Register replaces this mod's earlier page.
            OptionsService.Register(modId, options);
        }
    }
}
