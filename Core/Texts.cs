using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HARTModPack.Core
{
    public sealed class Notice
    {
        public string Text;
        public string Fingerprint;
        public bool Any { get { return !string.IsNullOrEmpty(Text); } }
    }

    // Every English line the player sees. The game only shows them.
    public static class Texts
    {
        public static string TurnedOn(int count)
        {
            return "HART. Mod Pack turned on " + count + (count == 1 ? " mod" : " mods") + ". Restart the game to load " + (count == 1 ? "it." : "them.");
        }

        public static string SettingsReminder()
        {
            return "HART. Mod Pack: apply the series settings once from Settings > Mods > " + Pack.SettingsModTitle + " > " + Pack.SettingsButton + ", then restart.";
        }

        public static string Line(MemberStatus s)
        {
            string name = s.Member.Title;
            switch (s.State)
            {
                case MemberState.On:
                    var parts = new List<string> { "on" };
                    if (s.Version != null) parts.Add(s.Version);
                    if (s.Verdict == VersionVerdict.Untested) parts.Add("not tested with this pack");
                    if (s.LoadedTwice) parts.Add("loaded twice, from the Workshop and ModsLocal: turn one off");
                    else parts.Add(s.FromLocal ? "ModsLocal" : "Workshop");
                    return name + ": " + string.Join(", ", parts.ToArray());
                case MemberState.TurnedOn:
                    return name + ": turned on, restart the game to load it";
                case MemberState.SwitchedOff:
                    return name + ": subscribed but switched off in Main menu > Mods" + (s.Member.Required ? "" : " (optional)");
                case MemberState.Failed:
                    return name + ": the game didn't load it. " + Shorten(s.Reason);
                case MemberState.NotLoaded:
                    return name + ": switched on after the game started; restart the game to load it";
                default:
                    return name + (s.Member.Required ? ": not installed" : ": not installed (optional)");
            }
        }

        public static string Summary(IList<MemberStatus> statuses, SteamState steam)
        {
            var required = statuses.Where(s => s.Member.Required).ToList();
            int on = required.Count(s => s.State == MemberState.On);
            string text = on == required.Count
                ? "All " + required.Count + " required mods are on."
                : on + " of " + required.Count + " required mods are on.";
            if (steam == SteamState.NotConnected) text += " Steam isn't connected, so Workshop subscriptions weren't checked.";
            else if (steam == SteamState.ListingFailed) text += " Steam didn't list the Workshop subscriptions, so switched-off mods may show as not installed. Press Check again.";
            return text;
        }

        public static string ExcludedLine(string assemblyName)
        {
            string title = assemblyName == "PersistentCity" ? "Persistent City" : assemblyName;
            return title + " is on. It isn't part of the pack; HART. Mod Pack leaves it alone.";
        }

        // One notice for everything that needs the player. Optional members appear only when they are loaded twice.
        public static Notice Check(IList<MemberStatus> statuses)
        {
            var missing = statuses.Where(s => s.Member.Required && s.State == MemberState.Missing).Select(s => s.Member.Title).ToList();
            var off = statuses.Where(s => s.Member.Required && s.State == MemberState.SwitchedOff).Select(s => s.Member.Title).ToList();
            var restart = statuses.Where(s => s.Member.Required && s.State == MemberState.NotLoaded).Select(s => s.Member.Title).ToList();
            var failed = statuses.Where(s => s.Member.Required && s.State == MemberState.Failed).Select(s => s.Member.Title).ToList();
            var untested = statuses.Where(s => s.State == MemberState.On && s.Verdict == VersionVerdict.Untested && s.Member.Required).Select(s => s.Member.Title + " " + s.Version).ToList();
            var twice = statuses.Where(s => s.State == MemberState.On && s.LoadedTwice).Select(s => s.Member.Title).ToList();

            var sentences = new List<string>();
            if (missing.Count > 0) sentences.Add("Missing: " + Join(missing) + ".");
            if (off.Count > 0) sentences.Add("Switched off: " + Join(off) + ". Turn them on in Main menu > Mods.");
            if (restart.Count > 0) sentences.Add("Restart the game to load: " + Join(restart) + ".");
            if (failed.Count > 0) sentences.Add("Didn't load: " + Join(failed) + ". Player.log has the reason.");
            if (untested.Count > 0) sentences.Add("Not tested with this pack: " + Join(untested) + ".");
            if (twice.Count > 0) sentences.Add("Loaded from both the Workshop and ModsLocal: " + Join(twice) + ". Turn one copy off.");
            if (sentences.Count == 0) return new Notice();
            string body = string.Join(" ", sentences.ToArray());
            return new Notice
            {
                Text = "HART. Mod Pack: " + body + " Details in Settings > Mods > HART. Mod Pack. Click to hide this until something changes.",
                Fingerprint = Pack.Version + "|" + Hash(body)
            };
        }

        static string Join(List<string> items) { return string.Join(", ", items.ToArray()); }

        static string Shorten(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "See Player.log.";
            string text = reason.Trim();
            return text.Length > 160 ? text.Substring(0, 157) + "..." : text;
        }

        // FNV-1a over the UTF-8 bytes, so the stored fingerprint is short and stable across runs.
        public static string Hash(string text)
        {
            uint hash = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(text ?? ""))
            {
                hash ^= b;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }
    }
}
