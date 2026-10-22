using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BAModAPI;
using BigAmbitions.ModsInternal;
using HARTModPack.Core;

namespace HARTModPack
{
    // Reads what the game discovered, loaded and refused this launch. Read-only: it never writes to a mod folder.
    static class GameScan
    {
        public static PackSnapshot Snapshot(SteamState steam, List<SubscribedItem> subscribed, HashSet<ulong> turnedOn)
        {
            var snapshot = new PackSnapshot
            {
                Steam = steam,
                Subscribed = subscribed ?? new List<SubscribedItem>(),
                TurnedOnThisLaunch = turnedOn ?? new HashSet<ulong>(),
            };
            var workshopNames = snapshot.Subscribed.Where(s => s.AssemblyName != null).GroupBy(s => s.Id.ToString()).ToDictionary(g => g.Key, g => g.First().AssemblyName);

            // Mods whose start failed are still discovered entries; they count as failed, not loaded. The game keeps
            // start failures only until the next scope loads (ModLifecycleLoader.SyncDiscoveryFailures), so an
            // Initialization-scope entry that isn't among the active mods of that scope also counts as failed.
            // Entries of later scopes (main menu, city) count as on once discovered: they start with their scene.
            var failedIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in ModDiscoveryRegistry.FailedModReasons) failedIds[pair.Key] = pair.Value;
            foreach (var pair in ModLifecycleLoader.FailedModMessages) failedIds[pair.Key] = pair.Value;
            HashSet<string> started;
            if (!ModLifecycleLoader.ActiveModIdsByScope.TryGetValue(ModActivationScope.Initialization, out started)) started = new HashSet<string>();
            List<DiscoveredModEntry> initEntries;
            if (ModDiscoveryRegistry.Entries.TryGetValue(ModActivationScope.Initialization, out initEntries))
            {
                foreach (var entry in initEntries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.ModId) && !started.Contains(entry.ModId) && !failedIds.ContainsKey(entry.ModId))
                        failedIds[entry.ModId] = (entry.ModDisplayName ?? entry.ModId) + ": didn't start. Player.log has the error.";
                }
            }

            string workshopRoot = null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entries in ModDiscoveryRegistry.Entries.Values)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || entry.EntryType == null || string.IsNullOrEmpty(entry.ModId)) continue;
                    bool workshop = IsWorkshopId(entry.ModId);
                    if (workshop && workshopRoot == null && !string.IsNullOrEmpty(entry.ModFolder)) workshopRoot = SafeParent(entry.ModFolder);
                    if (failedIds.ContainsKey(entry.ModId) || !seen.Add(entry.ModId)) continue;
                    Assembly assembly = entry.EntryType.Assembly;
                    var info = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute));
                    snapshot.Loaded.Add(new LoadedCopy
                    {
                        AssemblyName = assembly.GetName().Name,
                        Source = workshop ? ModSource.Workshop : ModSource.Local,
                        ModId = entry.ModId,
                        InformationalVersion = info == null ? null : info.InformationalVersion,
                        AssemblyVersion = assembly.GetName().Version,
                    });
                }
            }

            foreach (var pair in failedIds)
            {
                string name = null;
                if (IsWorkshopId(pair.Key))
                {
                    if (!workshopNames.TryGetValue(pair.Key, out name) && workshopRoot != null) name = RootAssemblyName(Path.Combine(workshopRoot, pair.Key));
                }
                else name = RootAssemblyName(pair.Key);
                if (name != null) snapshot.Failed.Add(new FailedMod { AssemblyName = name, Reason = pair.Value });
            }
            return snapshot;
        }

        // Assembly names of the mod folders in ModsLocal, read from DLL metadata without loading anything.
        public static List<string> LocalAssemblyNames()
        {
            var names = new List<string>();
            try
            {
                string root = ModDiscoveryRegistry.ModsLocalPath;
                if (!Directory.Exists(root)) return names;
                foreach (string folder in Directory.GetDirectories(root))
                {
                    string name = RootAssemblyName(folder);
                    if (name != null) names.Add(name);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't read ModsLocal: " + ex.Message);
            }
            return names;
        }

        // Same rule as the game's loader: exactly one DLL in the folder's root is the mod.
        public static string RootAssemblyName(string folder)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
                string[] dlls = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly);
                if (dlls.Length != 1) return null;
                return AssemblyName.GetAssemblyName(dlls[0]).Name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The game's mod ID is the Workshop item ID for Workshop mods and the folder path for ModsLocal ones.
        static bool IsWorkshopId(string modId)
        {
            ulong id;
            return ulong.TryParse(modId, out id) && id != 0;
        }

        static string SafeParent(string folder)
        {
            try { return Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)); }
            catch (Exception) { return null; }
        }
    }
}
