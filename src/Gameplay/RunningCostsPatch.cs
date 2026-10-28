using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HardModePatch.Core;
using HarmonyLib;
using Helpers;

namespace HardModePatch
{
    // Our own balance rules on Real Life Overhaul's utilities (Core/RunningCosts). RLO adds each place's day of power
    // and water in UtilitiesRules.AddDay and each connection's day in InternetRules.AddDay, both game-free statics
    // called from its nightly books; UtilitiesDirector.NeedsNet decides which businesses close without a connection.
    // - Equipment: a prefix on UtilitiesRules.AddDay rescales a business's floor use by its hours open and adds its
    //   appliances' draw (Core/RunningCosts.DayUse), so the bill, its due day, late fee and cut stay RLO's.
    // - Offices: NeedsNet reads every office type instead of RLO's three, and a postfix on InternetRules.AddDay adds the
    //   desks' IT to the office's connection, billed and cut with it.
    // RLO is patched by name once its assembly loads, before its books first run, and only in a reviewed RLO build
    // (RloGate). If a member's name or signature is not as reviewed, or NeedsNet no longer reads the list once, nothing
    // is patched and RLO's own bills stand.
    public static class RunningCostsPatch
    {
        const string Utilities = "RealLifeOverhaul.Utilities.";
        const int Power = 0, Water = 1, Internet = 2, Home = 0, Active = 1;
        static readonly Harmony harmony = new Harmony(GameplayFeatures.HarmonyId + ".runningcosts");
        static FieldInfo placeField, serviceField, kindField, stateField, accruedField, needsField;
        static Func<int> priceLevel;
        static string[] officeNeeds;
        static bool patched;
        static readonly HashSet<string> warnings = new HashSet<string>();

        // Cached per save and day: the player's places by RLO's address key and each one's equipment.
        static object stampSave;
        static int stampDay = int.MinValue;
        static readonly Dictionary<string, BuildingRegistration> places = new Dictionary<string, BuildingRegistration>(StringComparer.Ordinal);
        static readonly Dictionary<BuildingRegistration, Load> loads = new Dictionary<BuildingRegistration, Load>();

        public static bool Available { get; private set; }

        internal struct Load
        {
            public RunningCosts.Appliance Total;
            public int Seats;
        }

        public static void Start()
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) TryPatch(assembly);
        }

        public static void Stop()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            harmony.UnpatchAll(harmony.Id);
            patched = false;
            Available = false;
            Forget();
        }

        static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            TryPatch(args.LoadedAssembly);
        }

        static FieldInfo Field(Type type, string name, Type value)
        {
            var field = type == null ? null : AccessTools.DeclaredField(type, name);
            return field != null && !field.IsStatic && (value == null ? field.FieldType.IsEnum : field.FieldType == value) ? field : null;
        }

        static MethodInfo Method(Type type, string name, Type result, params Type[] parameters)
        {
            var method = type == null || parameters.Any(p => p == null) ? null : AccessTools.DeclaredMethod(type, name, parameters);
            return method != null && method.IsStatic && method.ReturnType == result ? method : null;
        }

        // Every RLO member the rules touch, null where one is missing or changed. Also used by the offline check.
        public static IList<MethodBase> ResolveHooks(Assembly target)
        {
            var account = target.GetType(Utilities + "Account", false);
            var place = target.GetType(Utilities + "PlaceInfo", false);
            var prices = target.GetType("RealLifeOverhaul.Prices", false);
            var catalogue = target.GetType(Utilities + "InternetCatalogue", false);
            placeField = Field(account, "Place", typeof(string));
            serviceField = Field(account, "Service", null);
            kindField = Field(account, "Kind", null);
            stateField = Field(account, "State", null);
            accruedField = Field(account, "Accrued", typeof(int));
            needsField = catalogue == null ? null : AccessTools.DeclaredField(catalogue, "Needs");
            if (needsField != null && (!needsField.IsStatic || needsField.FieldType != typeof(string[]))) needsField = null;
            var level = prices == null ? null : AccessTools.DeclaredProperty(prices, "Level");
            var getLevel = level == null ? null : level.GetGetMethod();
            bool shape = placeField != null && serviceField != null && kindField != null && stateField != null && accruedField != null
                && needsField != null && getLevel != null && getLevel.IsStatic && getLevel.ReturnType == typeof(int);
            return new MethodBase[]
            {
                shape ? Method(target.GetType(Utilities + "UtilitiesRules", false), "AddDay", typeof(void), account, typeof(long), typeof(int), typeof(int)) : null,
                shape ? Method(target.GetType(Utilities + "InternetRules", false), "AddDay", typeof(void), account, typeof(int), typeof(bool)) : null,
                shape ? Method(target.GetType(Utilities + "UtilitiesDirector", false), "NeedsNet", typeof(bool), place) : null,
                getLevel
            };
        }

        // NeedsNet must read RLO's list exactly once, or the office rule can't swap it.
        public static bool NeedsNetReadsList(MethodBase needsNet)
        {
            return needsNet != null && needsField != null
                && PatchProcessor.GetOriginalInstructions(needsNet).Count(i => i.opcode == OpCodes.Ldsfld && Equals(i.operand, needsField)) == 1;
        }

        static void TryPatch(Assembly assembly)
        {
            if (patched || assembly == null || assembly.GetName().Name != Targets.RealLifeOverhaul.AssemblyName) return;
            if (!RloGate.Accepts(assembly)) { patched = true; return; }
            try
            {
                var hooks = ResolveHooks(assembly);
                if (hooks.Any(h => h == null) || !NeedsNetReadsList(hooks[2]))
                {
                    Log.Warn("Running costs: RLO's utilities have changed since the reviewed version; RLO's own power, water and internet bills stand.");
                    return;
                }
                priceLevel = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), (MethodInfo)hooks[3]);
                officeNeeds = ((string[])needsField.GetValue(null)).Concat(RunningCosts.OfficeTypes).Distinct().ToArray();
                harmony.Patch(hooks[0], prefix: new HarmonyMethod(typeof(RunningCostsPatch), "UsePrefix"));
                harmony.Patch(hooks[1], postfix: new HarmonyMethod(typeof(RunningCostsPatch), "SeatPostfix"));
                harmony.Patch(hooks[2], transpiler: new HarmonyMethod(typeof(RunningCostsPatch), "NeedsTranspiler"));
                patched = true;
                Available = true;
                Log.Info("Running costs: RLO's utilities patched; equipment " + (Rules.EquipmentUtilities.Enabled ? "on" : "off")
                    + ", office IT " + (Rules.OfficeIt.Enabled ? "on" : "off") + ".");
            }
            catch (Exception ex)
            {
                harmony.UnpatchAll(harmony.Id);
                Available = false;
                Log.Error("Running costs unavailable; RLO's own bills stand: " + ex.Message);
            }
        }

        static int Int(FieldInfo field, object instance)
        {
            return Convert.ToInt32(field.GetValue(instance), CultureInfo.InvariantCulture);
        }

        static void Warn(string key, Exception ex)
        {
            if (warnings.Add(key)) Log.Error("Running costs (" + key + "): " + ex.Message);
        }

        // The game day and save the caches were built for; rebuilt when either moves.
        static void Refresh()
        {
            var save = SaveGameManager.Current;
            int day = save == null ? int.MinValue : save.Day;
            if (ReferenceEquals(save, stampSave) && day == stampDay) return;
            Forget();
            stampSave = save;
            stampDay = day;
            if (save == null || save.BuildingRegistrations == null) return;
            foreach (var reg in save.BuildingRegistrations)
            {
                if (reg == null || !reg.HasValidAddress || !(reg.RentedByPlayer || reg.BuildingOwnedByPlayer)) continue;
                places[Key(reg.StreetName, reg.StreetNumber)] = reg;
            }
        }

        static void Forget()
        {
            places.Clear();
            loads.Clear();
            stampSave = null;
            stampDay = int.MinValue;
        }

        // RLO's address key for a place (UtilitiesGame.Place).
        public static string Key(string street, int number)
        {
            return Uri.EscapeDataString(street ?? string.Empty) + "~" + number.ToString(CultureInfo.InvariantCulture);
        }

        static BuildingRegistration Find(object account)
        {
            Refresh();
            var key = placeField.GetValue(account) as string;
            BuildingRegistration reg;
            return key != null && places.TryGetValue(key, out reg) ? reg : null;
        }

        static Load LoadOf(BuildingRegistration reg)
        {
            Load load;
            if (loads.TryGetValue(reg, out load)) return load;
            load = ProjectLoad(reg, false);
            loads[reg] = load;
            return load;
        }

        internal static Load ProjectLoad(BuildingRegistration reg, bool observe)
        {
            int open = 0, always = 0, litres = 0, seats = 0;
            if (reg.itemInstances != null)
                foreach (var item in reg.itemInstances.Values)
                {
                    if (item == null) continue;
                    var data = observe ? CompanionItems.Data(item) : item.ItemCached;
                    int flags = data == null ? 0 : (int)data.type;
                    var a = RunningCosts.Of(item.itemName, flags);
                    open += a.OpenWatts;
                    always += a.AlwaysWatts;
                    litres += a.LitresPerHour;
                    if (RunningCosts.IsSeat(flags)) seats++;
                }
            return new Load { Total = new RunningCosts.Appliance(open, always, litres), Seats = seats };
        }

        // A virtual read of Find/LoadOf. It never changes their once-per-save/day cache timing.
        internal static Load PeekLoad(GameInstance game, BuildingRegistration reg, out bool applies, out string basis)
        {
            basis = "current-items";
            bool current = ReferenceEquals(game, stampSave) && game.Day == stampDay;
            BuildingRegistration effective = reg;
            applies = reg != null && reg.HasValidAddress && (reg.RentedByPlayer || reg.BuildingOwnedByPlayer);
            if (current) applies = places.TryGetValue(Key(reg.StreetName, reg.StreetNumber), out effective);
            if (!applies) return default(Load);
            Load cached;
            if (current && loads.TryGetValue(effective, out cached)) { basis = "billing-cache"; return cached; }
            return ProjectLoad(effective, true);
        }

        static int HoursOn(BuildingRegistration reg, int day)
        {
            if (reg.scheduleDays == null) return 0;
            var weekday = TimeHelper.GetDayOfWeek(day);
            foreach (var schedule in reg.scheduleDays)
                if (schedule != null && schedule.day == weekday) return RunningCosts.Hours(schedule.isOpen, schedule.GetHoursOpen);
            return 0;
        }

        // UtilitiesRules.AddDay(Account a, long use, int level, int day): a business's power or water for the day.
        static void UsePrefix(object a, ref long use, int day)
        {
            if (!Rules.EquipmentUtilities.Enabled || a == null) return;
            try
            {
                int service = Int(serviceField, a);
                if ((service != Power && service != Water) || Int(kindField, a) == Home) return;
                var reg = Find(a);
                if (reg == null) return;
                int hours = HoursOn(reg, day);
                use = RunningCosts.DayUse(service == Power, use, hours, LoadOf(reg).Total);
            }
            catch (Exception ex) { Warn("equipment", ex); }
        }

        // InternetRules.AddDay(Account a, int monthDays, bool outage): an office's desks billed with its connection on
        // each day RLO charges the connection itself.
        static void SeatPostfix(object a, int monthDays, bool outage)
        {
            if (!Rules.OfficeIt.Enabled || a == null || outage) return;
            try
            {
                if (Int(serviceField, a) != Internet || Int(stateField, a) != Active) return;
                var reg = Find(a);
                if (reg == null || !RunningCosts.IsOffice(reg.businessTypeName)) return;
                int cents = RunningCosts.SeatDayCents(LoadOf(reg).Seats, priceLevel(), monthDays);
                if (cents <= 0) return;
                long accrued = (int)accruedField.GetValue(a) + (long)cents;
                accruedField.SetValue(a, (int)Math.Min(accrued, int.MaxValue / 4));
            }
            catch (Exception ex) { Warn("office IT", ex); }
        }

        // The list NeedsNet reads: RLO's own, or with the office rule on every office type as well.
        public static string[] Needs()
        {
            return Rules.OfficeIt.Enabled && officeNeeds != null ? officeNeeds : (string[])needsField.GetValue(null);
        }

        static IEnumerable<CodeInstruction> NeedsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var needs = AccessTools.Method(typeof(RunningCostsPatch), "Needs");
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld && Equals(instruction.operand, needsField))
                {
                    var call = new CodeInstruction(OpCodes.Call, needs);
                    call.labels.AddRange(instruction.labels);
                    call.blocks.AddRange(instruction.blocks);
                    yield return call;
                }
                else yield return instruction;
            }
        }
    }
}
