using System;
using System.Collections.Generic;
using System.Linq;

namespace HardModePatch.Core
{
    public static class RloLine
    {
        public static readonly string[] ReviewedVersions = { "1.0.5" };

        public const string DiplomasOff = "Factory diplomas: off on Real Life Overhaul 1.0, which uses course ids 18, 19 and 34 for courses of its own. Machine sales stay unrestricted.";
        public const string SeriesDiplomasOff = "Series settings: factory_diplomas left as it is; factory diplomas are off on Real Life Overhaul 1.0.";
        public const string Accepted = "a Real Life Overhaul 1.0.x release, accepted by version. Each feature checks its own hooks and stays off on its own if they changed.";

        public static readonly string[] Offences = { "offence_red_light", "offence_speeding", "offence_pavement", "offence_wrong_way", "offence_drink", "offence_jaywalk", "offence_cross_red", "offence_horn" };

        public static readonly string[][] LivingRentReaders =
        {
            new[] { "RealLifeOverhaul.Population.PopulationGame", "Rent" },
            new[] { "RealLifeOverhaul.Roommates.RoommateGame", "RentPerDay" },
            new[] { "RealLifeOverhaul.Districts.DistrictGame", "Figures" },
            new[] { "RealLifeOverhaul.Construction.BuildModeGame", "DailyRent" },
            new[] { "RealLifeOverhaul.Construction.ConstructionGame", "Tenants" }
        };

        public static bool InLine(Version version)
        {
            return version != null && version.Major == 1 && version.Minor == 0;
        }

        public static bool Accepts(string hashPrefix, IEnumerable<string> verifiedHashes, Version version)
        {
            if (TargetGate.Reviewed(hashPrefix, verifiedHashes)) return true;
            if (hashPrefix == null || !InLine(version)) return false;
            string three = version.Major + "." + version.Minor + "." + Math.Max(version.Build, 0);
            return Array.IndexOf(ReviewedVersions, three) < 0;
        }

        public static bool AllLivingReadersNeeded(Version version)
        {
            return version != null && version.Major >= 1;
        }

        public static IList<SeriesOption> SeriesOptions(IEnumerable<FoundOption> registered, bool diplomasOff)
        {
            bool split = registered.Any(o => o.Owner == SeriesSettings.Rlo && o.Id == Offences[0]);
            var options = new List<SeriesOption>();
            foreach (var option in SeriesSettings.Options)
            {
                if (diplomasOff && option.Owner == SeriesSettings.Self && option.Id == Rules.FactoryDiplomas.Id) continue;
                if (split && option.Owner == SeriesSettings.Rlo && option.Id == "player_offences")
                {
                    options.AddRange(Offences.Select(id => new SeriesOption(SeriesSettings.Rlo, "Real Life Overhaul", id, OptionKind.Toggle, 0, "triage: RLO 1.0 splits player_offences")));
                    continue;
                }
                options.Add(option);
            }
            return options;
        }
    }
}
