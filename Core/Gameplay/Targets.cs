using System;
using System.Collections.Generic;

namespace HardModePatch.Core
{
    // One Workshop mod the patch rebalances. VerifiedHashes are the first 16 hex digits of the SHA-256 of
    // each DLL version whose code was read; mods/modpack/modpack.json in the HART repo records the same.
    public sealed class TargetInfo
    {
        public readonly string Key;
        public readonly string Title;
        public readonly long WorkshopId;
        public readonly string AssemblyName;
        public readonly string[] VerifiedHashes;

        public TargetInfo(string key, string title, long workshopId, string assemblyName, params string[] verifiedHashes)
        {
            Key = key;
            Title = title;
            WorkshopId = workshopId;
            AssemblyName = assemblyName;
            VerifiedHashes = verifiedHashes;
        }
    }

    public static class Targets
    {
        public static readonly TargetInfo RealLifeOverhaul = new TargetInfo("rlo", "Real Life Overhaul", 3816536560, "RealLifeOverhaul", "2f360a365cf7915f", "4f82b4f88f919178");
        public static readonly TargetInfo HardcoreAmbitions = new TargetInfo("hardcore", "Hardcore Ambitions", 3809287926, "HardcoreAmbitions", "62ba60cb66dc226c");
        public static readonly TargetInfo MeaningfulAmbience = new TargetInfo("ambience", "Meaningful Ambience", 3810641423, "MeaningfulAmbience", "5564717279c75596");
        public static readonly TargetInfo Bakery = new TargetInfo("bakery", "Bakery", 3810927743, "Bakery", "6d68edab0f5600f5");
        public static readonly TargetInfo Hyperreal = new TargetInfo("hyperreal", "Hyperreal", 3803756901, "PolyModsHyperreal", "7d1bd589947558e2");
        public static readonly TargetInfo InternetCafe = new TargetInfo("cafe", "Internet Cafe", 3796860957, "InternetCafe", "4fc4ea9bdc455d86");
        public static readonly TargetInfo VendingMachines = new TargetInfo("vending", "Vending Machines", 3812054455, "VendingMachines", "e46197e7ba3933eb");
        public static readonly TargetInfo EmpireBank = new TargetInfo("bank", "Empire Bank", 3781110629, "EmpireBank", "98be6844bcfd5793");

        public static readonly IList<TargetInfo> All = new[] { RealLifeOverhaul, HardcoreAmbitions, MeaningfulAmbience, Bakery, Hyperreal, InternetCafe, VendingMachines, EmpireBank };
    }

    public enum TargetState
    {
        // The mod isn't loaded; nothing to patch.
        Missing,
        // The DLL is a version whose code was reviewed.
        Verified,
        // A new version, but every hooked method is still there with the same signature: patched, with a warning.
        Unverified,
        // A hooked method is gone or changed: left unpatched.
        Incompatible,
        // A target patched only in reviewed versions (Real Life Overhaul) at a version not reviewed yet: left unpatched.
        Refused
    }

    public static class TargetGate
    {
        public const int HashPrefixLength = 16;

        public static string HashPrefix(string sha256Hex)
        {
            if (string.IsNullOrEmpty(sha256Hex) || sha256Hex.Length < HashPrefixLength) return null;
            return sha256Hex.Substring(0, HashPrefixLength).ToLowerInvariant();
        }

        // For targets patched only in reviewed versions: an unreadable file (null) is never reviewed.
        public static bool Reviewed(string hashPrefix, IEnumerable<string> verifiedHashes)
        {
            if (hashPrefix == null) return false;
            foreach (var verified in verifiedHashes)
                if (string.Equals(verified, hashPrefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // hashPrefix may be null when the DLL file couldn't be read; the hooks then decide alone.
        public static TargetState Classify(bool loaded, string hashPrefix, IEnumerable<string> verifiedHashes, int hooksExpected, int hooksFound)
        {
            if (!loaded) return TargetState.Missing;
            if (hooksExpected <= 0 || hooksFound < hooksExpected) return TargetState.Incompatible;
            if (hashPrefix != null)
                foreach (var verified in verifiedHashes)
                    if (string.Equals(verified, hashPrefix, StringComparison.OrdinalIgnoreCase)) return TargetState.Verified;
            return TargetState.Unverified;
        }
    }
}
