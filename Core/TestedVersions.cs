using System;
using System.Collections.Generic;
using System.Linq;

namespace HARTModPack.Core
{
    public enum VersionVerdict { Tested, Untested, NotExposed }

    // The member versions this pack release targets: the builds it was put together with and released alongside.
    // The README's evidence section says which of them have been seen running together in the game. A member that exposes no version
    // (assembly version 0.0.0.0 and no informational version) can't be checked and is never reported as untested.
    // Update this table, and the test that pins it, for every pack release.
    public static class TestedVersions
    {
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Historical 0.45.0/0.49.0 retained; 0.50.0 reviewed offline on 23 October 2026.
            { "RealLifeOverhaul", new[] { "0.45.0", "0.49.0", "0.50.0" } },
            { "HARTRivalBalance", new[] { "0.2.0" } },
            { "CityPerformance", new[] { "0.5.0" } },
            { "CampaignObjectives", new[] { "1.2.0", "1.2.1" } },
            // Optional: 1.1.0 is the Workshop build, 1.1.1 the local build, both seen 22 October 2026.
            { "HARTFreeCamera", new[] { "1.1.0", "1.1.1" } },
        };

        public static IReadOnlyList<string> For(string assemblyName)
        {
            string[] versions;
            return assemblyName != null && Table.TryGetValue(assemblyName, out versions) ? versions : new string[0];
        }

        public static IEnumerable<string> Members { get { return Table.Keys; } }

        // The version a mod exposes: its informational version without build metadata ("0.45.0+b12d358" is "0.45.0"),
        // else its assembly version without a zero revision. Null when it exposes neither.
        public static string Exposed(string informationalVersion, Version assemblyVersion)
        {
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                string text = informationalVersion.Trim();
                int plus = text.IndexOf('+');
                if (plus > 0) text = text.Substring(0, plus);
                if (text.Length > 0) return text;
            }
            if (assemblyVersion == null) return null;
            int build = Math.Max(assemblyVersion.Build, 0), revision = Math.Max(assemblyVersion.Revision, 0);
            if (assemblyVersion.Major == 0 && assemblyVersion.Minor == 0 && build == 0 && revision == 0) return null;
            string three = assemblyVersion.Major + "." + assemblyVersion.Minor + "." + build;
            return revision > 0 ? three + "." + revision : three;
        }

        public static VersionVerdict Check(string assemblyName, string exposedVersion)
        {
            if (string.IsNullOrEmpty(exposedVersion)) return VersionVerdict.NotExposed;
            return For(assemblyName).Any(v => string.Equals(v, exposedVersion, StringComparison.OrdinalIgnoreCase))
                ? VersionVerdict.Tested
                : VersionVerdict.Untested;
        }
    }
}
