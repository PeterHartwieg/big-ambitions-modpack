using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BAModAPI;
using HARTModPack.Core;
using UI.Notification;
using UnityEngine;

namespace HARTModPack
{
    // Runs the pack check on the main thread: once after the other mods have loaded at startup, and again from the
    // "Check again" button. Notices wait until the game has a notification list and are worded from a fresh scan.
    sealed class PackRunner : MonoBehaviour
    {
        const float LoadWaitSeconds = 10f;   // fallback when the game's "mods loaded" event doesn't arrive
        const float SettleSeconds = 2f;
        const float SteamTimeoutSeconds = 120f;

        static PackRunner instance;
        static bool modsLoaded;
        static HashSet<string> initEntries;      // startup-scope entries when that scope finished loading
        static bool startupNoticesQueued;        // once per session, even if a second copy of the pack takes over

        bool running;
        SteamState steam = SteamState.NotConnected;
        List<SubscribedItem> subscribed;
        readonly HashSet<ulong> turnedOn = new HashSet<ulong>();

        public static void Begin()
        {
            if (instance != null) return;
            ModEvents.onModsLoaded += OnModsLoaded;
            var go = new GameObject("HARTModPack");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PackRunner>();
            instance.StartCoroutine(instance.Run(true));
        }

        public static void End()
        {
            ModEvents.onModsLoaded -= OnModsLoaded;
            if (instance == null) return;
            Destroy(instance.gameObject);
            instance = null;
        }

        public static void CheckAgain()
        {
            if (instance == null || instance.running) return;
            instance.StartCoroutine(instance.Run(false));
        }

        // The first call after this mod loads is the end of the Initialization scope, this mod's own.
        static void OnModsLoaded()
        {
            if (modsLoaded) return;
            modsLoaded = true;
            try { initEntries = GameScan.InitializationEntryIds(); } catch (Exception) { initEntries = null; }
        }

        IEnumerator Run(bool atStartup)
        {
            running = true;
            try
            {
                if (atStartup)
                {
                    // ModEvents.onModsLoaded fires once the Initialization scope, this mod's own, has finished loading.
                    float waited = 0f;
                    while (!modsLoaded && waited < LoadWaitSeconds) { yield return new WaitForSecondsRealtime(0.5f); waited += 0.5f; }
                    yield return new WaitForSecondsRealtime(SettleSeconds);
                }

                if (!WorkshopMembers.SteamConnected())
                {
                    steam = SteamState.NotConnected;
                    subscribed = null;
                    Log.Info("Steam isn't connected; skipped turning on Workshop members.");
                }
                else
                {
                    Task<List<SubscribedItem>> listing = null;
                    try { listing = WorkshopMembers.ListAsync(); }
                    catch (Exception ex) { Log.Warn("Couldn't list Workshop subscriptions: " + ex.Message); }
                    float waited = 0f;
                    while (listing != null && !listing.IsCompleted && waited < SteamTimeoutSeconds) { yield return new WaitForSecondsRealtime(0.5f); waited += 0.5f; }
                    List<SubscribedItem> result = null;
                    if (listing != null && !listing.IsCompleted) Log.Warn("Steam didn't list the subscriptions within " + SteamTimeoutSeconds + " s; checked without them.");
                    else if (listing != null)
                    {
                        try { result = listing.Result; }
                        catch (Exception ex) { Log.Warn("Couldn't list Workshop subscriptions: " + ex.GetBaseException().Message); }
                    }
                    if (result != null && !WorkshopMembers.LooksComplete(result))
                    {
                        Log.Warn("Steam's list of subscriptions (" + result.Count + " mods) misses mods the game loaded; checked without it.");
                        result = null;
                    }
                    steam = result != null ? SteamState.Listed : SteamState.ListingFailed;
                    subscribed = result;
                    if (result != null)
                    {
                        if (WorkshopMembers.ModsPanelOpen()) Log.Info("Waiting for the Mods panel to close before turning on members.");
                        while (WorkshopMembers.ModsPanelOpen()) yield return new WaitForSecondsRealtime(0.5f);
                        HashSet<ulong> added = null;
                        try { added = WorkshopMembers.TurnOn(result); }
                        catch (Exception ex) { Log.Error("Turning on members failed: " + ex); }
                        if (added != null && added.Count > 0)
                        {
                            turnedOn.UnionWith(added);
                            int count = added.Count;
                            StartCoroutine(Notify(() => Texts.TurnedOn(count), 12f, null, null));
                        }
                    }
                }

                var statuses = Scan(true);
                if (atStartup && statuses != null && !startupNoticesQueued)
                {
                    startupNoticesQueued = true;
                    StartCoroutine(StartupNotices());
                }
            }
            finally
            {
                running = false;
            }
        }

        // Scans the game and updates the Mods page. Returns null if the scan failed.
        List<MemberStatus> Scan(bool log)
        {
            try
            {
                var snapshot = GameScan.Snapshot(steam, subscribed, turnedOn, initEntries);
                var statuses = StatusEvaluator.Evaluate(snapshot);
                var lines = statuses.Select(Texts.Line).ToList();
                if (log) foreach (string line in lines) Log.Info(line);
                StatusPage.Show(Texts.Summary(statuses, steam) + " Checked at " + DateTime.Now.ToString("HH:mm:ss") + ".", lines, StatusEvaluator.ExcludedOn(snapshot).Select(Texts.ExcludedLine).ToList());
                return statuses;
            }
            catch (Exception ex)
            {
                Log.Error("Pack check failed: " + ex);
                return null;
            }
        }

        // The check notice and the settings reminder, decided when the game can show them, from a fresh scan.
        IEnumerator StartupNotices()
        {
            while (Notifications.onShow == null) yield return new WaitForSecondsRealtime(1f);
            while (running) yield return new WaitForSecondsRealtime(0.5f);
            var statuses = Scan(false);
            if (statuses == null) yield break;
            bool restartPending = turnedOn.Count > 0;

            var notice = Texts.Check(statuses);
            string dismissed = UnityEngine.PlayerPrefs.HasKey(NoticePolicy.DismissedKey) ? UnityEngine.PlayerPrefs.GetString(NoticePolicy.DismissedKey) : null;
            if (NoticePolicy.ShowCheck(notice, dismissed, restartPending, steam))
            {
                string fingerprint = notice.Fingerprint;
                Show(notice.Text, 20f, () =>
                {
                    UnityEngine.PlayerPrefs.SetString(NoticePolicy.DismissedKey, fingerprint);
                    UnityEngine.PlayerPrefs.Save();
                    Log.Info("Pack notice hidden until the list changes.");
                });
            }
            bool settingsModOn = statuses.Any(s => s.Member.AssemblyName == "HardModePatch" && s.State == MemberState.On);
            string reminded = UnityEngine.PlayerPrefs.HasKey(NoticePolicy.ReminderKey) ? UnityEngine.PlayerPrefs.GetString(NoticePolicy.ReminderKey) : null;
            if (NoticePolicy.ShowReminder(reminded, settingsModOn, restartPending) && Show(Texts.SettingsReminder(), 15f, null))
            {
                UnityEngine.PlayerPrefs.SetString(NoticePolicy.ReminderKey, Pack.Version);
                UnityEngine.PlayerPrefs.Save();
            }
        }

        static IEnumerator Notify(Func<string> text, float seconds, Action onClick, Action onShown)
        {
            while (Notifications.onShow == null) yield return new WaitForSecondsRealtime(1f);
            if (Show(text(), seconds, onClick) && onShown != null) onShown();
        }

        static bool Show(string text, float seconds, Action onClick)
        {
            try
            {
                Notifications.Show(NotificationType.Info, LocaleKeys.Notice, new Dictionary<string, string> { { "text", text } },
                    seconds, null, onClick, true, false);
                Log.Info("Notice: " + text);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't show a notice: " + ex.Message + ". Text: " + text);
                return false;
            }
        }
    }
}
