using System.Collections.Generic;
using System.Linq;

namespace HardModePatch.Core
{
    // Happiness rebalance (Peter, 18 October 2026; shows, the pier, park and beach walks +2 on 20 October). Home tops out around 60 (about 47 from these plus RLO's small
    // home moods); the other 40 or so come from outside activities, each lasting days from one visit, so only a mix of
    // them reaches 100. Home activities bank time: each hour adds the game's own hours per hour, up to four days, so
    // one long evening covers several days instead of a daily routine. Values are set, never multiplied, so a
    // second load of the modifiers can't compound them. Only the game's own types are listed; RLO's are untouched.
    public sealed class HappinessValue
    {
        public readonly string Type;
        public readonly int Amount;
        public readonly int Hours;      // the modifier's own hours; -1 keeps the game's (open-ended while doing it)
        public readonly int MaxHours;   // -1 keeps the game's
        public readonly bool Outside;   // one visit fills the whole duration

        public HappinessValue(string type, int amount, int hours, int maxHours, bool outside)
        {
            Type = Prefix + type;
            Amount = amount;
            Hours = hours;
            MaxHours = maxHours;
            Outside = outside;
        }

        public const string Prefix = "ba:happinessmodifier_";
    }

    public static class HappinessRules
    {
        public const string NoHome = HappinessValue.Prefix + "no_home";
        public const int NoHomeInHotel = -15;

        static HappinessValue Home(string type, int amount, int hours, int maxHours) { return new HappinessValue(type, amount, hours, maxHours, false); }
        static HappinessValue Out(string type, int amount, int hours) { return new HappinessValue(type, amount, hours, hours, true); }
        // While doing it: same amount as the result, the game's own duration.
        static HappinessValue Doing(string type, int amount) { return new HappinessValue(type, amount, -1, -1, false); }

        public static readonly IList<HappinessValue> All = new[]
        {
            // Home, banked: 15 + 4 × 8 = 47.
            Home("hygiene", 15, 24, 48),
            Home("watched_tv", 8, 2, 96), Doing("watching_tv", 8),
            Home("played_videogames", 8, 2, 96), Doing("playing_videogames", 8),
            Home("read", 8, 48, 96), Doing("reading", 8),
            Home("exercised", 8, 2, 96), Doing("exercising", 8),

            // Outside, one visit fills the duration.
            Out("watched_show", 12, 120), Doing("watching_show", 12),
            Out("played_golf", 10, 168), Doing("golf", 10),
            Out("played_tennis", 10, 168), Doing("tennis", 10),
            Out("gambled", 10, 168),
            Out("went_swimming", 8, 120), Doing("swimming", 8),
            Out("went_to_nightclub", 8, 120), Doing("in_a_nightclub", 8),
            Out("partied_at_the_pier", 10, 120), Doing("partying_at_the_pier", 10),
            Out("djed", 8, 120), Doing("djing", 8),
            Out("danced_in_own_nightclub", 8, 120), Doing("dancing_in_own_nightclub", 8),
            Out("walked_in_the_park", 7, 96), Doing("walking_in_the_park", 7),
            Out("walked_in_the_beach", 7, 96), Doing("walking_in_the_beach", 7),
            Out("walked_in_the_cemetery", 5, 96), Doing("walking_in_the_cemetery", 5),
            Out("rode_pier_rides", 7, 96), Doing("riding_pier_rides", 7),
            Out("rested_on_a_luxury_yacht", 10, 120),
            Out("rested_on_a_boat", 8, 72),

            // Small ones.
            Home("slept_in_luxury_car", 5, 4, 24),
            Home("donated", 5, 2, 24)
        };

        static readonly Dictionary<string, HappinessValue> byType = All.ToDictionary(v => v.Type);

        public static HappinessValue Find(string type)
        {
            HappinessValue value;
            return type != null && byType.TryGetValue(type, out value) ? value : null;
        }

        // The hours an outside activity's result gets, whatever the time spent: the whole duration. Zero stays zero
        // (the game adds nothing), and home activities keep the hours the game worked out.
        public static int GrantedHours(string type, int requestedHours)
        {
            var value = Find(type);
            if (value == null || !value.Outside || requestedHours == 0) return requestedHours;
            return value.MaxHours;
        }

        public static int NoHomeAmount(int gameAmount, bool hotelBooked)
        {
            return hotelBooked ? NoHomeInHotel : gameAmount;
        }
    }
}
