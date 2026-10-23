using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BigAmbitions.Items;
using Helpers;
using HardModePatch.Core;
using HARTModPack.Core;
using static HARTModPack.Core.CompanionSnapshot;

namespace HardModePatch
{
    internal static class CompanionGame
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static string Capture(GameInstance game)
        {
            if (game == null || !ReferenceEquals(game, SaveGameManager.Current) || string.IsNullOrEmpty(game.characterId)
                || game.Day < 0 || game.Hour < 0 || game.Hour > 23 || float.IsNaN(game.Minute) || game.Minute < 0 || game.Minute >= 60) return null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (assemblies.Count(a => a.GetName().Name == "HARTModPack") != 1) return null;
            var rlos = assemblies.Where(a => a.GetName().Name == "RealLifeOverhaul").ToArray();
            var rlo = rlos.Length == 1 ? rlos[0] : null;
            var readers = new Dictionary<string, Func<Dictionary<string, object>>>
            {
                { "runtime", () => Runtime(rlos.Length) },
                { "rent", Rent },
                { "runningCosts", () => Costs(game, rlo) },
                { "factoryPurchases", () => Factory(game) },
                { "retailDemand", () => Retail(game) }
            };
            return CompanionSnapshot.Capture(Pack.Version, Row("character", game.characterId, "day", game.Day, "hour", game.Hour, "minute", (int)game.Minute), readers);
        }
        internal static Dictionary<string, object> State(Rule rule)
        {
            if (!GameplayFeatures.Active) return Feature(rule.Enabled, false, GameplayFeatures.Blocked ? "blocked" : "inactive", GameplayFeatures.Blocked ? "legacy-loaded" : "gameplay-inactive");
            if (!rule.Enabled) return Feature(false, false, "off");
            bool available;
            if (rule == Rules.FactoryDiplomas)
            {
                string observed = RealLifeOverhaulPatch.ObserveGate();
                return Feature(true, observed == "observation-failed" ? (bool?)null : observed == "active", observed == "active" ? "active" : "unavailable", observed == "active" ? null : observed);
            }
            if (rule.Target != null)
            {
                var target = GameplayFeatures.TargetState(rule.Target);
                available = target == TargetState.Verified || target == TargetState.Unverified;
                return Feature(true, available, available ? "active" : TargetName(target), available ? null : "target-" + TargetName(target));
            }
            if (rule == Rules.EquipmentUtilities || rule == Rules.OfficeIt) available = RunningCostsPatch.Available;
            else if (rule == Rules.RentRebalance) available = RentRebalance.Available;
            else if (rule == Rules.HappinessRebalance) available = HappinessRebalance.Available;
            else if (rule == Rules.OwnerJobFullPay) available = OwnerJobPay.Available;
            else if (rule == Rules.UtilityClosureReasons) available = UtilityClosureReasonPatch.Available;
            else if (rule == Rules.MuteRloNightChirping) available = NightChirpingPatch.Available;
            else if (rule == Rules.IndebtedScenario) available = IndebtedScenario.Available;
            else if (rule == Rules.SeriesPrefill) available = CustomGamePrefill.Available;
            else return Feature(true, null, "unknown", "rule-unmodelled");
            return Feature(true, available, available ? "active" : "unavailable", available ? null : "hooks-unavailable");
        }
        static bool Effective(Rule rule) { return Equals(State(rule)["effective"], true); }
        static string TargetName(TargetState state) { return state == TargetState.Unverified ? "compatible" : state.ToString().ToLowerInvariant(); }
        static Dictionary<string, object> Runtime(int rloCount)
        {
            var rules = Row();
            foreach (var rule in Rules.All) rules.Add(rule.Id, State(rule));
            return Module("ready", Row("gameplayActive", GameplayFeatures.Active, "blockedByLegacy", GameplayFeatures.Blocked,
                "rloReview", rloCount > 1 ? "undecided" : RloGate.ReviewState(rloCount == 1), "rules", rules,
                "targets", Targets.All.Select(t => Row("id", t.Key, "assemblyName", t.AssemblyName, "state", TargetName(GameplayFeatures.TargetState(t)))).ToArray()));
        }
        static Dictionary<string, object> Rent()
        {
            bool active = Effective(Rules.RentRebalance);
            return Module("ready", Row("model", "hart-rent-v1", "state", State(Rules.RentRebalance), "rounding", "ceil-whole-money",
                "residentialTypeId", RentRules.ResidentialType, "residentialMultiplierThousandths", active ? 2000 : 1000,
                "otherMultiplierThousandths", active ? 3000 : 1000, "appliesTo", "new-lease-market-rent", "signedLeases", "unchanged"));
        }
        static object Address(BuildingRegistration reg) { return Row("street", reg.StreetName, "number", reg.StreetNumber); }
        static IEnumerable<BuildingRegistration> Sites(GameInstance game)
        {
            return (game.BuildingRegistrations ?? new List<BuildingRegistration>()).Where(r => r != null && r.HasValidAddress && (r.RentedByPlayer || r.BuildingOwnedByPlayer))
                .OrderBy(r => r.StreetName, StringComparer.Ordinal).ThenBy(r => r.StreetNumber);
        }
        static Dictionary<string, object> Costs(GameInstance game, Assembly rlo)
        {
            var items = ItemsGetter.AllItems;
            if (items == null) return Module("unavailable", null, "item-catalogue-unavailable");
            var rows = new List<object>();
            foreach (var item in items.OrderBy(i => i.itemName, StringComparer.Ordinal))
            {
                var a = RunningCosts.Of(item.itemName, (int)item.type);
                rows.Add(Row("itemId", item.itemName, "openWatts", a.OpenWatts, "alwaysWatts", a.AlwaysWatts,
                    "litresPerOpenHour", a.LitresPerHour, "officeSeat", RunningCosts.IsSeat((int)item.type)));
            }
            var sites = new List<object>();
            bool office = Effective(Rules.OfficeIt), equipment = Effective(Rules.EquipmentUtilities);
            var net = office ? InternetState(game, rlo) : null;
            foreach (var reg in Sites(game))
            {
                bool applies; string basis;
                var load = RunningCostsPatch.PeekLoad(game, reg, out applies, out basis);
                object seatCost = 0; string itStatus = "ready";
                if (office && applies && RunningCosts.IsOffice(reg.businessTypeName))
                {
                    if (net == null) { seatCost = null; itStatus = "unavailable"; }
                    else
                    {
                        object account;
                        seatCost = net.Accounts.TryGetValue(RunningCostsPatch.Key(reg.StreetName, reg.StreetNumber), out account)
                            && Int(account, "State") == 1 && !(Int(account, "OutageUntil") > Int(account, "OutageFrom") && Int(account, "OutageFrom") / 24 == game.Day)
                            ? RunningCosts.SeatDayCents(load.Seats, net.Level, net.MonthDays) : 0;
                    }
                }
                sites.Add(Row("address", Address(reg), "equipmentApplies", equipment && applies && BuildingHelper.GetBuilding(new Address(reg.StreetName, reg.StreetNumber))?.BuildingType != "ba:buildingtype_residential", "inputBasis", basis,
                    "openWatts", load.Total.OpenWatts, "alwaysWatts", load.Total.AlwaysWatts, "litresPerOpenHour", load.Total.LitresPerHour,
                    "officeSeats", load.Seats, "itSeatDayCents", seatCost, "itStatus", itStatus, "itDay", game.Day,
                    "itBasis", "included-in-rlo-internet-accrual"));
            }
            return Module("ready", Row("model", "hart-running-costs-v1", "equipment", State(Rules.EquipmentUtilities), "officeIt", State(Rules.OfficeIt),
                "referenceHours", RunningCosts.ReferenceHours, "closedShareThousandths", RunningCosts.ClosedShare,
                "priceLevelMin", RunningCosts.LevelLeast, "priceLevelMax", RunningCosts.LevelMost,
                "seatMonthCentsAtBasePrice", RunningCosts.SeatMonthCents, "officeTypeIds", RunningCosts.OfficeTypes.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                "items", rows, "currentSites", sites));
        }
        sealed class Net
        {
            internal int Level, MonthDays;
            internal readonly Dictionary<string, object> Accounts = new Dictionary<string, object>(StringComparer.Ordinal);
        }
        static object Property(Type type, string name, object owner = null) { return type.GetProperty(name, Any).GetValue(owner, null); }
        static object Field(object owner, string name) { return owner.GetType().GetField(name, Any).GetValue(owner); }
        static int Int(object owner, string name) { return Convert.ToInt32(Field(owner, name)); }
        static Net InternetState(GameInstance game, Assembly rlo)
        {
            // Only the already reviewed RLO shape; no private save decoder, account indexing or director refresh.
            if (rlo == null || RloGate.ReviewState(true) != "accepted") return null;
            try
            {
                if (!(bool)rlo.GetType("RealLifeOverhaul.Settings", true).GetField("Utilities", Any).GetValue(null)) return new Net();
                var type = rlo.GetType("RealLifeOverhaul.Utilities.UtilitiesDirector", true);
                var owner = Property(type, "Instance");
                if (owner == null || (bool)Property(type, "Failed") || (bool)Field(owner, "_gaveUp") || !ReferenceEquals(Field(owner, "_save"), game)) return null;
                var state = Property(type, "State", owner);
                if (state == null) return null;
                var result = new Net { Level = (int)Property(rlo.GetType("RealLifeOverhaul.Prices", true), "Level"),
                    MonthDays = (int)Property(rlo.GetType("RealLifeOverhaul.GameCalendar", true), "MonthDays") };
                foreach (var account in (IEnumerable)Field(state, "Accounts"))
                    if (Int(account, "Service") == 2)
                    {
                        string key = (string)Field(account, "Place");
                        if (result.Accounts.ContainsKey(key)) return null;
                        result.Accounts.Add(key, account);
                    }
                return result;
            }
            catch (Exception) { return null; }
        }
        static Dictionary<string, object> Factory(GameInstance game)
        {
            string held = null; game.modData?.TryGetValue(DiplomaHold.HoldKey, out held);
            var courses = FactoryDiplomas.All.Select(c => Row("courseId", c.Id, "courseKey", c.Key, "itemIds", new[] { c.Item },
                "baseFeeCents", c.Fee * 100, "sessions", FactoryDiplomas.Sessions, "homeworkPieces", FactoryDiplomas.Homework,
                "hoursPerSession", FactoryDiplomas.Hours, "fullTimeWeekdayMask", c.Days, "fullTimeStartHour", c.Hour,
                "campus", "school", "needsAnyCourseIds", new int[0], "needsDegree", false, "distanceAllowed", false)).ToArray();
            return Module("ready", Row("model", "hart-factory-purchase-gate-v1", "restriction", "purchase-only", "gate", State(Rules.FactoryDiplomas),
                "education", Row("provider", "rlo", "module", "education", "minimumVersion", 1), "courses", courses, "heldRecords", DiplomaHold.Inspect(held)));
        }
        static Dictionary<string, object> Retail(GameInstance game)
        {
            object cafe;
            try
            {
                var sites = new List<object>();
                if (Effective(Rules.CafePcCapacity))
                    foreach (var reg in Sites(game))
                    {
                        try { sites.Add(Row("address", Address(reg), "status", "ready", "reason", null, "usableStations", InternetCafePatch.ObserveStations(reg))); }
                        catch (Exception) { sites.Add(Row("address", Address(reg), "status", "unavailable", "reason", "cafe-requirement-inputs-unavailable", "usableStations", null)); }
                    }
                cafe = Module("ready", Row("state", State(Rules.CafePcCapacity), "model", "hart-cafe-stations-v1",
                    "sessionsPerStationPerHour", CafeMath.SessionsPerPcPerHour, "currentSites", sites));
            }
            catch (Exception) { cafe = Module("unavailable", null, "cafe-requirement-inputs-unavailable"); }
            var ambience = Effective(Rules.AmbienceScalesWithDemand)
                ? Module("unavailable", null, "ambience-owner-inputs-unavailable")
                : Module("disabled", null, "ambience-rule-inactive");
            return Module("ready", Row("cafe", cafe, "ambience", ambience, "ambienceModel", "hart-ambience-demand-v1", "officeAmbience", "unchanged"));
        }
    }
}
