using System;
using System.Collections.Generic;
using System.Linq;

namespace HardModePatch.Core
{
    public enum OptionKind { Toggle, Slider, Dropdown }

    // One Mod Options value the HART series plays with. Owner is the assembly name of the mod that registers the option:
    // the button finds the option by that and its id, never by a mod id, which differs between Workshop and local copies.
    public sealed class SeriesOption
    {
        public readonly string Owner, Mod, Id, Why;
        public readonly OptionKind Kind;
        public readonly int Value;

        public SeriesOption(string owner, string mod, string id, OptionKind kind, int value, string why)
        {
            Owner = owner; Mod = mod; Id = id; Kind = kind; Value = value; Why = why;
        }

        public string Label { get { return Mod + " " + Id; } }
    }

    // A mod option as registered with the game, seen by the button.
    public sealed class FoundOption
    {
        public readonly string Owner, ModId, Id;
        public readonly OptionKind Kind;
        public readonly int Min, Max;

        public FoundOption(string owner, string modId, string id, OptionKind kind, int min, int max)
        {
            Owner = owner; ModId = modId; Id = id; Kind = kind; Min = min; Max = max;
        }
    }

    // A custom game screen value: a GameVariables field and what the series sets it to.
    public sealed class NewGameValue
    {
        public readonly string Field;
        public readonly object Value;
        public NewGameValue(string field, object value) { Field = field; Value = value; }
    }

    // The HART series 2 settings (docs/series-2/modpack-release-plan.md in the HART repo). Every value the "Apply HART
    // series settings" button writes is in Options; anything not listed keeps the player's value.
    public static class SeriesSettings
    {
        public const string Self = "HARTModPack", Rlo = "RealLifeOverhaul", RivalBalance = "HARTRivalBalance", DynamicRivals = "DynamicRivals";
        const string HmpTitle = "HART. Mod Pack", RloTitle = "Real Life Overhaul", RbTitle = "HART. Thriving Rivals", DrTitle = "Dynamic Rivals";

        static SeriesOption Hmp(string id, string why) { return new SeriesOption(Self, HmpTitle, id, OptionKind.Toggle, 1, why); }
        static SeriesOption On(string id, string why) { return new SeriesOption(Rlo, RloTitle, id, OptionKind.Toggle, 1, why); }
        static SeriesOption Off(string id, string why) { return new SeriesOption(Rlo, RloTitle, id, OptionKind.Toggle, 0, why); }
        static SeriesOption Slider(string id, int value, string why) { return new SeriesOption(Rlo, RloTitle, id, OptionKind.Slider, value, why); }
        static SeriesOption Dr(string id, int index, string why) { return new SeriesOption(DynamicRivals, DrTitle, id, OptionKind.Dropdown, index, why); }

        public static readonly SeriesOption[] Options =
        {
            // Our own rules: the pack's mechanics.
            Hmp("scenario_indebted_start", "series start: $551.37 cash and the $75,454.35 loan"),
            new SeriesOption(Self, HmpTitle, "rent_rebalance", OptionKind.Toggle, 0, "Economy Expansion running costs replace the flat rent multiplier"),
            Hmp("happiness_rebalance", "series rule"),
            Hmp("owner_job_full_pay", "series rule"),
            Hmp("factory_diplomas", "series rule"),
            Hmp("equipment_utilities", "Economy Expansion running costs on RLO"),
            Hmp("office_it", "Economy Expansion running costs on RLO"),

            new SeriesOption(RivalBalance, RbTitle, "rival_preset", OptionKind.Dropdown, 1, "preset Normal (0 Easy, 1 Normal, 2 Hard)"),

            // Dynamic Rivals, as homepc logged on 22 October. Choice lists from its 19 October DLL.
            Dr("wholesale_rival_count", 0, "7 (choices 7, 8, 10, 15, ...)"),
            Dr("import_rival_count", 1, "8"),
            Dr("retail_occupancy", 0, "Vanilla"),
            Dr("office_occupancy", 0, "Vanilla"),
            Dr("warehouse_occupancy", 0, "Vanilla"),
            Dr("residential_occupancy", 0, "Vanilla"),
            Dr("event_frequency", 2, "Balanced (Very Frequent, Frequent, Balanced, Rare, Extremely Rare)"),

            // Real Life Overhaul. Presentation: Peter turned RLO's camera off for good (17 Oct).
            Off("camera_enabled", "RLO camera off for the series"),
            Off("camera_mouselook", "RLO camera off for the series"),
            Off("car_camera", "RLO camera off for the series"),
            Off("social_media", "Flare off while Persistent City is parked (21 Oct)"),
            // Options triage, applied on homepc 20 October.
            Slider("social_texts_max", 1, "triage: the slider's minimum"),
            Off("life_events", "triage"),
            Slider("accident_frequency", 1, "triage: the minimum"),
            Slider("crime_frequency", 1, "triage: the minimum"),
            Off("roadside_checks", "triage"),
            Off("player_offences", "triage"),
            Slider("street_event_frequency", 1, "triage: one day in twelve"),
            Slider("fire_frequency", 1, "triage: the minimum"),
            Off("social_media_notices", "triage"),
            Off("accident_marker", "triage"),
            Off("accident_notices", "triage"),
            Off("ledger_online_notes", "triage: system not in use yet"),
            Off("markets_dividend_notes", "triage: system not in use yet"),
            Off("careers_agency_note", "triage: system not in use yet"),
            Slider("business_frequency", 2, "triage"),
            // Triage decisions, 20 October.
            On("social_relationships", "triage decision: stays on"),
            On("clothes_must_be_bought", "triage decision: on for the series save (kept per save)"),
            On("driving_licence", "triage decision"),
            On("driving_school", "triage decision"),
            // RLO-led profile (modpack audit, 14 October): the systems the series is built on.
            On("economy", "audit profile"), On("economy_city", "audit profile"), On("economy_company", "audit profile"), On("utilities", "audit profile"),
            On("economy_company_staff", "audit profile"), On("economy_company_cover", "audit profile"), On("economy_company_marketing", "audit profile"),
            On("economy_company_tax", "audit profile"), On("economy_company_licences", "audit profile"), On("economy_rota", "audit profile"),
            On("interiors", "audit profile"), On("business_pressure", "audit profile"),
            On("family", "audit profile"), On("home_life", "audit profile"), On("social", "audit profile"), On("leisure", "audit profile"),
            On("civic", "audit profile"), On("civic_elections", "audit profile"), On("civic_office", "audit profile"),
            On("city_life", "audit profile"), On("ambient_sound", "audit profile"), On("night_life", "audit profile"),
            On("transit_buses", "audit profile"), On("transit_subway", "audit profile"), On("transit_taxi_call", "audit profile"),
            On("nav_minimap", "audit profile"), On("nav_gps", "audit profile"), On("planner", "audit profile"),
            On("jobs", "audit profile"), On("careers", "audit profile; factory diplomas need it"), On("careers_study", "audit profile; factory diplomas need it"),
            On("careers_jobs", "audit profile"), On("careers_life", "audit profile"), On("careers_degrees", "audit profile"),
            On("economy_products", "audit profile"), On("economy_consequences", "audit profile")
        };

        // The custom game screen, from the series-2 campaign's recommended settings (Peter, 20 October). Field names
        // are GameVariables' in the game's 1.0 build. Difficulty and the tutorial are left to SetPreset, which makes
        // the preset custom and switches the tutorial off itself.
        public static readonly NewGameValue[] NewGame =
        {
            new NewGameValue("daysPerYear", 60),
            new NewGameValue("startingMoney", 0),
            new NewGameValue("rivalsDifficultyMultiplier", 1.5f),
            new NewGameValue("startingAge", 18),
            new NewGameValue("disableAging", false),
            new NewGameValue("disableEnergy", false),
            new NewGameValue("disableHappiness", false),
            new NewGameValue("allCoursesUnlocked", false),
            new NewGameValue("allContactsUnlocked", false),
            new NewGameValue("disableVehicleDamage", false),
            new NewGameValue("disableVehicleFuel", false),
            new NewGameValue("tutorialEnabled", false),
            new NewGameValue("taxPercentage", 51),
            new NewGameValue("employeeHourlySalaryMultiplier", 1.3f),
            new NewGameValue("marketPriceMultiplier", 1.3f),
            new NewGameValue("bankInterestMultiplier", 1.3f),
            new NewGameValue("baseCustomerPromotionMultiplier", 0.5f),
            new NewGameValue("wholesaleUrgentFeeMultiplier", 2f),
            new NewGameValue("importerUrgentFeeMultiplier", 2f),
            new NewGameValue("disableWholesaleAndImportLimits", false),
            new NewGameValue("allProductsAvailableFromImporters", false),
            new NewGameValue("exportMultiplier", 0.1f),
            new NewGameValue("sellingMultiplier", 0.5f)
        };

        // The registered option a series value goes to, or why there is none. Options of the owner's assembly only;
        // an id the owner registers twice is ambiguous and skipped rather than guessed.
        public static FoundOption Pick(SeriesOption wanted, IEnumerable<FoundOption> registered, out string reason)
        {
            var owners = registered.Where(o => string.Equals(o.Owner, wanted.Owner, StringComparison.Ordinal)).ToList();
            if (owners.Count == 0) { reason = wanted.Mod + " isn't loaded or has no options"; return null; }
            var matches = owners.Where(o => string.Equals(o.Id, wanted.Id, StringComparison.Ordinal)).ToList();
            if (matches.Count == 0) { reason = "no option '" + wanted.Id + "' in " + wanted.Mod; return null; }
            if (matches.Count > 1) { reason = "'" + wanted.Id + "' is registered " + matches.Count + " times by " + wanted.Mod; return null; }
            var found = matches[0];
            if (found.Kind != wanted.Kind) { reason = "'" + wanted.Id + "' is a " + found.Kind.ToString().ToLowerInvariant() + ", not a " + wanted.Kind.ToString().ToLowerInvariant(); return null; }
            if (wanted.Value < found.Min || wanted.Value > found.Max) { reason = "'" + wanted.Id + "' takes " + found.Min + " to " + found.Max + ", not " + wanted.Value; return null; }
            reason = null;
            return found;
        }

        // The game's PlayerPrefs key for a mod option (ModOptionPrefs.BuildKey); null for an empty id, as the game's.
        public static string PrefKey(string modId, string optionId)
        {
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(optionId)) return null;
            return "m:" + modId + ":" + optionId;
        }
    }
}
