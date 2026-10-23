using System;
using System.Collections.Generic;

namespace HardModePatch.Core
{
    // A rule is one switch on the options page. Each patch reads its rule every time it runs, so a change
    // takes effect without a restart. The default is the hard value.
    public sealed class Rule
    {
        public readonly string Id;
        public readonly TargetInfo Target;
        public readonly bool Default;
        public bool Enabled;
        // Raised when the player switches the rule on or off, for patches that act on the change itself.
        public event Action<bool> Changed;

        public Rule(string id, TargetInfo target, bool defaultValue)
        {
            Id = id;
            Target = target;
            Default = defaultValue;
            Enabled = defaultValue;
        }

        public void Set(bool value)
        {
            if (Enabled == value) return;
            Enabled = value;
            var changed = Changed;
            if (changed != null) changed(value);
        }

        // The options page label is a key in Locales/en.json.
        public string LabelKey { get { return "hardmode_" + Id; } }
    }

    public static class Rules
    {
        // Null target: our own optional new-game scenario, not a patch to another author's mod.
        // Off by default so an existing installation keeps its patch-only behavior. The pack selects it explicitly.
        public static readonly Rule IndebtedScenario = new Rule("scenario_indebted_start", null, false);

        // Null target: our own rent rebalance (Core/RentRules). Off by default; the HART pack turns it on.
        public static readonly Rule RentRebalance = new Rule("rent_rebalance", null, false);

        // Null target: our own happiness rebalance (Core/HappinessRules). Off by default; the HART pack turns it on.
        public static readonly Rule HappinessRebalance = new Rule("happiness_rebalance", null, false);
        // Ours: RLO's jobs and gigs pay in full to a player who runs businesses (RLO cuts 15% per business).
        public static readonly Rule OwnerJobFullPay = new Rule("owner_job_full_pay", null, false);

        // Null target: our own running costs on RLO's utilities (Core/RunningCosts). Off by default; the HART pack turns them on.
        public static readonly Rule EquipmentUtilities = new Rule("equipment_utilities", null, false);
        public static readonly Rule OfficeIt = new Rule("office_it", null, false);
        // Presentation only, on by default. It never changes the closure decision.
        public static readonly Rule UtilityClosureReasons = new Rule("utility_closure_reasons", null, true);

        // Null target: the custom game screen opens with the HART series values (Core/SeriesSettings.NewGame). On by
        // default: it only fills in the screen, and the player can change every value before starting.
        public static readonly Rule SeriesPrefill = new Rule("series_prefill_custom_game", null, true);

        // The series opts into RLO school diplomas for advanced factory machine purchases.
        public static readonly Rule FactoryDiplomas = new Rule("factory_diplomas", Targets.RealLifeOverhaul, false);

        // Hardcore Ambitions: keep the indebted start, drop what fights a custom difficulty.
        public static readonly Rule HardcoreKeepDifficulty = new Rule("hardcore_keep_difficulty", Targets.HardcoreAmbitions, true);
        public static readonly Rule HardcoreNoFunds = new Rule("hardcore_no_funds", Targets.HardcoreAmbitions, true);
        public static readonly Rule HardcoreVehiclePricesPerSave = new Rule("hardcore_vehicle_prices_per_save", Targets.HardcoreAmbitions, true);

        // Meaningful Ambience: decor scales the customers the game sends instead of adding a share of capacity.
        public static readonly Rule AmbienceScalesWithDemand = new Rule("ambience_scales_with_demand", Targets.MeaningfulAmbience, true);

        // Bakery: vanilla bakery margins. Prices are read when the items register, so a change needs a restart.
        public static readonly Rule BakeryVanillaMargins = new Rule("bakery_vanilla_margins", Targets.Bakery, true);
        public static readonly Rule HyperrealMapReturnFix = new Rule("hyperreal_map_return_fix", Targets.Hyperreal, true);

        // Internet Cafe: off-screen customers limited by the PCs, two 30-minute sessions per PC per hour.
        public static readonly Rule CafePcCapacity = new Rule("cafe_pc_capacity", Targets.InternetCafe, true);

        // Vending Machines: holding a machine in a rival's building doesn't open their furniture to you.
        public static readonly Rule VendingProtectRivalFurniture = new Rule("vending_protect_rival_furniture", Targets.VendingMachines, true);

        // Empire Bank.
        public static readonly Rule BankTaxWithdrawals = new Rule("bank_tax_withdrawals", Targets.EmpireBank, true);
        public static readonly Rule BankNoEmergencyBorrowing = new Rule("bank_no_emergency_borrowing", Targets.EmpireBank, true);
        public static readonly Rule BankHonestCreditScores = new Rule("bank_honest_credit_scores", Targets.EmpireBank, true);
        public static readonly Rule BankProductLimits = new Rule("bank_product_limits", Targets.EmpireBank, true);
        public static readonly Rule BankNoRemoteRequests = new Rule("bank_no_remote_requests", Targets.EmpireBank, true);

        public static readonly IList<Rule> All = new[]
        {
            SeriesPrefill,
            IndebtedScenario,
            RentRebalance,
            HappinessRebalance,
            OwnerJobFullPay, EquipmentUtilities, OfficeIt, UtilityClosureReasons, FactoryDiplomas,
            HardcoreKeepDifficulty, HardcoreNoFunds, HardcoreVehiclePricesPerSave,
            AmbienceScalesWithDemand,
            BakeryVanillaMargins,
            HyperrealMapReturnFix,
            CafePcCapacity,
            VendingProtectRivalFurniture,
            BankTaxWithdrawals, BankNoEmergencyBorrowing, BankHonestCreditScores, BankProductLimits, BankNoRemoteRequests
        };
    }
}
