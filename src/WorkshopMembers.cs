using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BigAmbitions.ModsInternal;
using HARTModPack.Core;
using Helpers;

namespace HARTModPack
{
    // Lists the player's subscribed Workshop mods and turns on pack members the game left off.
    // Steam may have downloaded a subscription while the game was closed; the game then never adds it to
    // steam_mod_manifest.txt (SteamModDownloader.EnsureModInstalled only adds items it downloads itself).
    static class WorkshopMembers
    {
        const int PageSize = 50; // Steam's page size, as in SteamModDownloader.GetSubscribedModItems
        const int MaxPages = 40;

        public static bool SteamConnected()
        {
            try { return SteamHelper.IsConnectedToSteam(); }
            catch (Exception) { return false; }
        }

        // A read-only Steam query through the game's SteamHelper.GetUserSubscribedItems, the call the game's own
        // listing makes. Not SteamModLoadingService.GetSubscribedMods: that also downloads and enables any
        // subscribed item without a folder, whatever it is. Steam's item types live in Facepunch.Steamworks.Win64
        // on Windows and Facepunch.Steamworks.Posix on the Mac, so they are read by reflection instead of being
        // compiled against; tools/check-api.sh checks these members on both.
        public static async Task<List<SubscribedItem>> ListAsync()
        {
            MethodInfo query = typeof(SteamHelper).GetMethod("GetUserSubscribedItems", BindingFlags.Public | BindingFlags.Static);
            ParameterInfo[] parameters = query == null ? null : query.GetParameters();
            if (parameters == null || parameters.Length != 4 || parameters[0].ParameterType != typeof(int))
                throw new MissingMethodException("SteamHelper.GetUserSubscribedItems(int, string, string, SteamId) not found");
            object anyUser = Activator.CreateInstance(parameters[3].ParameterType); // default SteamId: the logged-in user

            var items = new List<SubscribedItem>();
            for (int page = 1; page <= MaxPages; page++)
            {
                var task = (Task)query.Invoke(null, new object[] { page, "mod", null, anyUser });
                await task;
                var list = task.GetType().GetProperty("Result").GetValue(task, null) as IList;
                if (list == null || list.Count == 0) break;
                foreach (object entry in list)
                {
                    ulong id = ReadId(entry);
                    if (id == 0) continue;
                    var directory = entry.GetType().GetProperty("Directory").GetValue(entry, null) as string;
                    items.Add(new SubscribedItem { Id = id, AssemblyName = GameScan.RootAssemblyName(directory), Enabled = ModManifest.Contains(id) });
                }
                if (list.Count < PageSize) break;
            }
            return items;
        }

        // Item.Id is a Steamworks.Data.PublishedFileId, a struct around a public ulong field "Value".
        static ulong ReadId(object item)
        {
            object id = item.GetType().GetProperty("Id").GetValue(item, null);
            if (id == null) return 0;
            FieldInfo value = id.GetType().GetField("Value");
            return value == null ? 0 : Convert.ToUInt64(value.GetValue(id));
        }

        // Adds the planned items to the manifest and remembers each, so the pack never turns the same item on twice.
        public static HashSet<ulong> TurnOn(List<SubscribedItem> items)
        {
            var turnedOn = new HashSet<ulong>();
            var before = new HashSet<ulong>();
            foreach (var item in items)
            {
                if (UnityEngine.PlayerPrefs.GetInt(NoticePolicy.TurnedOnPrefix + item.Id, 0) == 1) before.Add(item.Id);
            }
            var plan = TurnOnPlanner.Plan(items, GameScan.LocalAssemblyNames(), before);
            foreach (string note in plan.Notes) Log.Info(note);
            foreach (ulong id in plan.ToAdd)
            {
                try
                {
                    ModManifest.Add(id);
                    if (!ModManifest.Contains(id)) continue;
                    UnityEngine.PlayerPrefs.SetInt(NoticePolicy.TurnedOnPrefix + id, 1);
                    turnedOn.Add(id);
                    var item = items.Find(i => i.Id == id);
                    Log.Info("Turned on Workshop item " + id + " (" + (item == null ? "?" : item.AssemblyName) + "). It loads after a restart.");
                }
                catch (Exception ex)
                {
                    Log.Error("Couldn't turn on Workshop item " + id + ": " + ex.Message);
                }
            }
            if (turnedOn.Count > 0) UnityEngine.PlayerPrefs.Save();
            return turnedOn;
        }

        // The Mods panel loads every mod added to the manifest while it is open as soon as it closes
        // (ModsView.OnManifestChanged), so the pack waits until it is closed. The Mod Creator replaces the panel
        // while that session lasts, so it counts too. Both objects are inactive when closed; FindAnyObjectByType skips inactive ones.
        public static bool ModsPanelOpen()
        {
            try
            {
                return UnityEngine.Object.FindAnyObjectByType<BigAmbitions.ModsPanel>() != null
                    || UnityEngine.Object.FindAnyObjectByType<BigAmbitions.UploadModPanel>() != null;
            }
            catch (Exception) { return false; }
        }

        // Steam's helper answers with an empty page when it times out or loses the connection, which looks like
        // "no subscriptions". Every Workshop mod the game discovered this launch is subscribed, so a listing
        // without one of them, or an empty one while mods are enabled, is incomplete.
        public static bool LooksComplete(List<SubscribedItem> items)
        {
            var listed = new HashSet<ulong>();
            foreach (var item in items) listed.Add(item.Id);
            foreach (ulong id in GameScan.DiscoveredWorkshopIds()) if (!listed.Contains(id)) return false;
            return items.Count > 0 || ModManifest.GetIds().Count == 0;
        }
    }
}
