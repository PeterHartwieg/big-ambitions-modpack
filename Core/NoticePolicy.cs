namespace HARTModPack.Core
{
    // When notices show. The game stores the remembered values in PlayerPrefs; this decides from them.
    public static class NoticePolicy
    {
        public const string DismissedKey = "hartmodpack.check_dismissed";
        public const string ReminderKey = "hartmodpack.settings_reminded";
        public const string TurnedOnPrefix = "hartmodpack.turned_on.";

        // The check notice shows until the player clicks it. A click hides that exact list for this pack version;
        // a new problem or a new pack version shows it again. It waits while a restart is pending, and when Steam
        // didn't list the subscriptions, because switched-off members would then read as missing.
        public static bool ShowCheck(Notice notice, string dismissed, bool restartPending, SteamState steam)
        {
            if (restartPending || steam == SteamState.ListingFailed || notice == null || !notice.Any) return false;
            return dismissed != notice.Fingerprint;
        }

        // The settings reminder shows once per pack version, and only while the integrated gameplay (which has the button) is active.
        public static bool ShowReminder(string reminded, bool settingsModOn, bool restartPending)
        {
            return !restartPending && settingsModOn && reminded != Pack.Version;
        }
    }
}
