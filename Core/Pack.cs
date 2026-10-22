using System;
using System.Collections.Generic;
using System.Linq;

namespace HARTModPack.Core
{
    public sealed class PackMember
    {
        public PackMember(string assemblyName, string title, bool required)
        {
            AssemblyName = assemblyName;
            Title = title;
            Required = required;
        }

        // The game identifies a mod by its root DLL's assembly simple name, so the pack does too.
        // Workshop IDs differ between uploads and forks and are never used to find a member.
        public string AssemblyName { get; }
        public string Title { get; }
        public bool Required { get; }
    }

    public static class Pack
    {
        public const string Version = "1.0.0";
        public const string SettingsModTitle = "HART. Mode Patch";
        public const string SettingsButton = "Apply HART series settings";

        // Assembly simple names confirmed from the installed copies on 22 October 2026
        // (agent rig ModsLocal and the Mac Steam Workshop content folder).
        public static readonly IReadOnlyList<PackMember> Members = new[]
        {
            new PackMember("RealLifeOverhaul", "Real Life Overhaul", true),
            new PackMember("DynamicRivals", "Dynamic Rivals", true),
            new PackMember("HARTRivalBalance", "HART Rival Balance", true),
            new PackMember("HardModePatch", "HART. Mode Patch", true),
            new PackMember("CityPerformance", "City Performance", true),
            new PackMember("FoodTrucks", "HART. Street Food", true),
            new PackMember("DrivenRides", "HART. Self Driving", true),
            new PackMember("Roadster", "HART. RDSTR", true),
            new PackMember("AlcwareSeasons", "Alcware - Seasons", true),
            new PackMember("RetailCapacityUpgrades", "Alcware - Retail Expansion", true),
            new PackMember("InternetCafe", "Internet Cafe", true),
            new PackMember("Bakery", "Bakery - V1.2", true),
            new PackMember("PolyModsHyperreal", "POLYMODS HYPERREAL", true),
            new PackMember("CampaignObjectives", "Campaign Objectives", true),
            new PackMember("CameraTools", "Camera Tools", false),
            new PackMember("HARTFreeCamera", "HART Free Camera", false),
            new PackMember("BigCopilotLink", "Big Copilot Link", false),
        };

        // Mods the pack never turns on, even when subscribed. Persistent City is parked (21 October 2026).
        public static readonly IReadOnlyList<string> Excluded = new[] { "PersistentCity" };

        public static PackMember Find(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return null;
            return Members.FirstOrDefault(m => string.Equals(m.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsExcluded(string assemblyName)
        {
            return !string.IsNullOrEmpty(assemblyName) && Excluded.Any(e => string.Equals(e, assemblyName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
