using System;
using System.Collections.Generic;
using System.Linq;

namespace HARTModPack.Core
{
    public enum ModSource { Workshop, Local }

    public enum SteamState { NotConnected, ListingFailed, Listed }

    // A mod the game discovered and accepted this launch.
    public sealed class LoadedCopy
    {
        public string AssemblyName;
        public ModSource Source;
        public string ModId;
        public string InformationalVersion;
        public Version AssemblyVersion;
    }

    // A Workshop item from the game's subscribed-mods listing.
    public sealed class SubscribedItem
    {
        public ulong Id;
        public string AssemblyName; // null when its folder has no single root DLL
        public bool Enabled;        // listed in steam_mod_manifest.txt
    }

    // A mod the game refused to load, with the game's reason.
    public sealed class FailedMod
    {
        public string AssemblyName;
        public string Reason;
    }

    public sealed class PackSnapshot
    {
        public SteamState Steam = SteamState.NotConnected;
        public bool SteamChecked { get { return Steam == SteamState.Listed; } }
        public List<LoadedCopy> Loaded = new List<LoadedCopy>();
        public List<SubscribedItem> Subscribed = new List<SubscribedItem>();
        public List<FailedMod> Failed = new List<FailedMod>();
        public List<string> AwaitingRestart = new List<string>(); // discovered after startup, starts after a restart
        public HashSet<ulong> TurnedOnThisLaunch = new HashSet<ulong>();
    }

    // NotLoaded: on in steam_mod_manifest.txt but not discovered and not failed, which means it was switched on
    // after the game's discovery ran (by the game's own download step, or in the Mods menu); a restart loads it.
    public enum MemberState { On, TurnedOn, SwitchedOff, Failed, NotLoaded, Missing }

    public sealed class MemberStatus
    {
        public PackMember Member;
        public MemberState State;
        public string Version;
        public VersionVerdict Verdict = VersionVerdict.NotExposed;
        public bool FromWorkshop;
        public bool FromLocal;
        public string Reason;

        public bool LoadedTwice { get { return FromWorkshop && FromLocal; } }
    }

    public static class StatusEvaluator
    {
        public static List<MemberStatus> Evaluate(PackSnapshot snapshot)
        {
            return Pack.Members.Select(m => Evaluate(m, snapshot)).ToList();
        }

        static MemberStatus Evaluate(PackMember member, PackSnapshot snapshot)
        {
            var status = new MemberStatus { Member = member };
            var copies = snapshot.Loaded.Where(c => Same(c.AssemblyName, member.AssemblyName)).ToList();
            if (copies.Count > 0)
            {
                status.State = MemberState.On;
                status.FromWorkshop = copies.Any(c => c.Source == ModSource.Workshop);
                status.FromLocal = copies.Any(c => c.Source == ModSource.Local);
                // With both copies on, the game loads one DLL for the name, the first copy it discovered (the Workshop one).
                var first = copies.FirstOrDefault(c => c.Source == ModSource.Workshop) ?? copies[0];
                status.Version = TestedVersions.Exposed(first.InformationalVersion, first.AssemblyVersion);
                status.Verdict = TestedVersions.Check(member.AssemblyName, status.Version);
                return status;
            }
            var failed = snapshot.Failed.FirstOrDefault(f => Same(f.AssemblyName, member.AssemblyName));
            if (failed != null)
            {
                status.State = MemberState.Failed;
                status.Reason = failed.Reason;
                return status;
            }
            var subscribed = snapshot.Subscribed.Where(s => Same(s.AssemblyName, member.AssemblyName)).ToList();
            if (subscribed.Any(s => snapshot.TurnedOnThisLaunch.Contains(s.Id))) status.State = MemberState.TurnedOn;
            else if (snapshot.AwaitingRestart.Any(a => Same(a, member.AssemblyName))) status.State = MemberState.NotLoaded;
            else if (subscribed.Any(s => s.Enabled)) status.State = MemberState.NotLoaded;
            else if (subscribed.Count > 0) status.State = MemberState.SwitchedOff;
            else status.State = MemberState.Missing;
            return status;
        }

        // Excluded mods (Persistent City) that are on anyway: the pack mentions them and leaves them alone.
        public static List<string> ExcludedOn(PackSnapshot snapshot)
        {
            return Pack.Excluded.Where(e => snapshot.Loaded.Any(c => Same(c.AssemblyName, e))).ToList();
        }

        internal static bool Same(string a, string b)
        {
            return a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
