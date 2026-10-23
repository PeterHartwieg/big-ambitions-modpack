using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;

// The running-cost rules against a released RLO: Harmony applies the real patches to RLO's own game-free billing
// methods, which then run on fixture accounts. No game launched, engine methods called or save read; game objects are
// plain uninitialised data containers.
if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: CostCheck <game Managed folder> <HARTModPack.dll> <RealLifeOverhaul.dll>");
    return 2;
}
string managed = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    if (new[] { "mscorlib", "System", "System.Core", "netstandard" }.Contains(name.Name)) return null;
    string path = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
var patch = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
var game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "BigAmbitions.dll"));
var items = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "BigAmbitions.Items.dll"));
var rlo = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2]));
const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
int checks = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL " + name);
    checks++; Console.WriteLine("PASS " + name);
}
object Empty(Type type) => RuntimeHelpers.GetUninitializedObject(type);
void Set(object o, string field, object value)
{
    var f = o.GetType().GetField(field, Any);
    f.SetValue(o, f.FieldType.IsEnum ? Enum.ToObject(f.FieldType, value) : value);
}
object Get(object o, string field) => o.GetType().GetField(field, Any).GetValue(o);
IList ListOf(Type type) => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
Type U(string type) => rlo.GetType("RealLifeOverhaul.Utilities." + type, true);
Type costs = patch.GetType("HardModePatch.RunningCostsPatch", true);
Type core = patch.GetType("HardModePatch.Core.RunningCosts", true);
object Rule(string name) => patch.GetType("HardModePatch.Core.Rules", true).GetField(name).GetValue(null);
void SetRule(string name, bool on) { var r = Rule(name); r.GetType().GetMethod("Set").Invoke(r, new object[] { on }); }

// Hooks.
var hooks = (IList<MethodBase>)costs.GetMethod("ResolveHooks").Invoke(null, new object[] { rlo });
Check(hooks.Count == 4 && hooks.All(h => h != null), "every RLO member the rules touch resolves");
Check((bool)costs.GetMethod("NeedsNetReadsList").Invoke(null, new object[] { hooks[2] }), "NeedsNet reads RLO's list exactly once");
costs.GetMethod("TryPatch", Any).Invoke(null, new object[] { rlo });
Check((bool)costs.GetProperty("Available").GetValue(null), "patched once RLO's assembly is seen");
Check(hooks.Take(3).All(h => Harmony.GetPatchInfo(h)?.Owners.Contains("hartmodpack.gameplay.runningcosts") == true), "Harmony applies the prefix, postfix and transpiler");

// A save with one shop and one office the player rents, each open 12 hours every day.
Type gameType = game.GetType("GameInstance", true), registrationType = game.GetType("BuildingRegistration", true);
object save = Empty(gameType);
Set(save, "Day", 9);
Set(save, "realEstate", ListOf(gameType.GetField("realEstate").FieldType.GetGenericArguments()[0]));
var regs = ListOf(registrationType);
Set(save, "BuildingRegistrations", regs);
game.GetType("SaveGameManager", true).GetField("_current", Any).SetValue(null, save);
Type scheduleType = game.GetType("ScheduleDay", true), slotType = game.GetType("OpeningHourSlot", true);
Type itemInstanceType = items.GetType("BigAmbitions.Items.ItemInstance", true), itemType = items.GetType("BigAmbitions.Items.Item", true);
Type weekType = scheduleType.GetField("day").FieldType;
int hoursOpen = 12;
object Registration(string street, int number, string type, params (string name, int flags)[] placed)
{
    object reg = Empty(registrationType);
    Set(reg, "StreetName", street); Set(reg, "StreetNumber", number); Set(reg, "RentedByPlayer", true); Set(reg, "businessTypeName", type);
    var days = ListOf(scheduleType);
    foreach (var week in Enum.GetValues(weekType))
    {
        object day = Activator.CreateInstance(scheduleType);
        Set(day, "day", week); Set(day, "isOpen", true);
        var slots = ListOf(slotType);
        slots.Add(Activator.CreateInstance(slotType, 8, 8 + hoursOpen));
        Set(day, "openingHourSlots", slots);
        days.Add(day);
    }
    Set(reg, "scheduleDays", days);
    var dict = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), itemInstanceType));
    int i = 0;
    foreach (var (name, flags) in placed)
    {
        object data = Empty(itemType);
        Set(data, "type", flags);
        // A live engine object, so ItemCached keeps the fixture instead of asking the game's catalogue.
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "UnityEngine.CoreModule.dll")).GetType("UnityEngine.Object", true).GetField("m_CachedPtr", Any).SetValue(data, new IntPtr(1));
        object item = Empty(itemInstanceType);
        Set(item, "itemName", name); Set(item, "_itemCached", data);
        dict["i" + i++] = item;
    }
    Set(reg, "itemInstances", dict);
    regs.Add(reg);
    return reg;
}
object shop = Registration("5th Avenue", 61, "ba:businesstype_fastfoodrestaurant",
    ("ba:itemname_industrialfryermachine", 0x40), ("ba:itemname_industrialfridge", 0x40), ("ba:itemname_countersink", 0x1000001),
    ("ba:itemname_ceilinglampwarehouseforsale", 0x2000));
object office = Registration("Madison Avenue", 12, "ba:businesstype_lawfirm",
    ("ba:itemname_computer", 0x800010), ("ba:itemname_laptop", 0x800010), ("ba:itemname_desktopcomputer", 0x800010));
string Key(object reg) => (string)costs.GetMethod("Key").Invoke(null, new[] { Get(reg, "StreetName"), Get(reg, "StreetNumber") });

object Account(object reg, int service, int kind)
{
    object a = Activator.CreateInstance(U("Account"));
    Set(a, "Place", Key(reg)); Set(a, "Service", service); Set(a, "Kind", kind); Set(a, "State", 1);
    return a;
}
var addDay = U("UtilitiesRules").GetMethod("AddDay");
var dayCents = U("UtilitiesRules").GetMethod("DayCents");
int Cents(int service, long use) => (int)dayCents.Invoke(null, new[] { Enum.ToObject(U("Service"), service), (object)use, 1000, false });
int Bill(object a, long use, int day)
{
    Set(a, "Accrued", 0);
    addDay.Invoke(null, new[] { a, (object)use, 1000, day });
    return (int)Get(a, "Accrued");
}
const int FoodAndDrink = 3, HomeKind = 0, Office = 1;
// RLO's own open-day power for 50 m2 of food and drink, at its 900 Wh a square metre (spring).
long rloPower = 45000, rloWater = 1500;

SetRule("EquipmentUtilities", false);
var power = Account(shop, 0, FoodAndDrink);
Check(Bill(power, rloPower, 9) == Cents(0, rloPower), "rule off: RLO's own power bill");
SetRule("EquipmentUtilities", true);
Check(Bill(power, rloPower, 9) == Cents(0, 500 * 24 + rloPower * 3 / 4 + 5000 * 12), "12 hours: the fridge replaces RLO's smaller standby, RLO's open floor, the fryer on top");
var water = Account(shop, 1, FoodAndDrink);
Check(Bill(water, rloWater, 9) == Cents(1, rloWater), "a counter sink within RLO's basin figure adds nothing");
hoursOpen = 24; regs.Clear(); shop = Registration("5th Avenue", 61, "ba:businesstype_fastfoodrestaurant",
    ("ba:itemname_industrialfryermachine", 0x40), ("ba:itemname_industrialfridge", 0x40)); Set(save, "Day", 10);
Check(Bill(power, rloPower, 10) == Cents(0, 500 * 24 + rloPower * 3 / 2 + 5000 * 24), "24 hours: RLO's open floor twice over and the fryer all day");
hoursOpen = 0; regs.Clear(); shop = Registration("5th Avenue", 61, "ba:businesstype_fastfoodrestaurant",
    ("ba:itemname_industrialfryermachine", 0x40), ("ba:itemname_industrialfridge", 0x40)); Set(save, "Day", 11);
Check(Bill(power, rloPower / 4, 11) == Cents(0, 500 * 24), "a shut day bills the fridge in place of RLO's smaller standby");
var home = Account(shop, 0, HomeKind);
Check(Bill(home, 7000, 11) == Cents(0, 7000), "homes keep RLO's bill");
var elsewhere = Activator.CreateInstance(U("Account"));
Set(elsewhere, "Place", "Nowhere~1"); Set(elsewhere, "Service", 0); Set(elsewhere, "Kind", FoodAndDrink); Set(elsewhere, "State", 1);
Check(Bill(elsewhere, rloPower, 11) == Cents(0, rloPower), "a place the player doesn't hold keeps RLO's bill");

// Office IT on the connection.
hoursOpen = 12; regs.Clear();
shop = Registration("5th Avenue", 61, "ba:businesstype_fastfoodrestaurant", ("ba:itemname_computer", 0x800010));
office = Registration("Madison Avenue", 12, "ba:businesstype_lawfirm",
    ("ba:itemname_computer", 0x800010), ("ba:itemname_laptop", 0x800010), ("ba:itemname_desktopcomputer", 0x800010));
object studio = Registration("Broadway", 400, "reallife:businesstype_softwarestudio", ("ba:itemname_computer", 0x800010));
Set(save, "Day", 12);
var netDay = U("InternetRules").GetMethod("AddDay");
int Net(object a, int monthDays, bool outage)
{
    Set(a, "Accrued", 0);
    netDay.Invoke(null, new[] { a, (object)monthDays, outage });
    return (int)Get(a, "Accrued");
}
var officeNet = Account(office, 2, Office);
Set(officeNet, "PriceCents", 14580);
var shopNet = Account(shop, 2, FoodAndDrink);
Set(shopNet, "PriceCents", 14580);
SetRule("OfficeIt", false);
Check(Net(officeNet, 5, false) == 2916, "rule off: RLO's plan only");
SetRule("OfficeIt", true);
Check(Net(officeNet, 5, false) == 2916 + 3 * 15000 / 5, "three desks add $150 a month each, a fifth a day in a 5-day month");
Check(Net(officeNet, 5, true) == 0, "an outage day charges nothing, as RLO credits it");
Check(Net(shopNet, 5, false) == 2916, "a shop's computer is not an office desk");
var studioNet = Account(studio, 2, Office);
Set(studioNet, "PriceCents", 14580);
Check(Net(studioNet, 5, false) == 2916, "RLO's tech firms pay RLO's own software by the seat, not desk IT");
Set(officeNet, "State", 0);
Check(Net(officeNet, 5, false) == 0, "a connection still being installed charges nothing");

// Which businesses need a connection.
Type placeType = U("PlaceInfo");
bool Needs(string type)
{
    object reg = Empty(registrationType);
    Set(reg, "businessTypeName", type);
    object place = Activator.CreateInstance(placeType);
    placeType.GetField("Reg").SetValue(place, reg);
    placeType.GetField("Kind").SetValue(place, Enum.ToObject(U("PlaceKind"), Office));
    return (bool)hooks[2].Invoke(null, new[] { place });
}
SetRule("OfficeIt", false);
Check(!Needs("ba:businesstype_lawfirm") && Needs("ba:businesstype_webdevelopmentagency"), "rule off: RLO's own list");
SetRule("OfficeIt", true);
Check(Needs("ba:businesstype_lawfirm") && Needs("ba:businesstype_importexport") && Needs("ba:businesstype_headquarters"), "rule on: every office type needs a connection");
Check(Needs("reallife:businesstype_softwarestudio") && !Needs("ba:businesstype_fastfoodrestaurant"), "RLO's tech firms still need one, shops still don't");

// Read-only utility closure presentation, using native data shapes and the real Harmony hook.
new Harmony("costcheck.logs").Patch(patch.GetType("HardModePatch.Log", true).GetMethod("Warn", Any), prefix: new HarmonyMethod(typeof(CheckLog), nameof(CheckLog.Prefix)));
var closurePatch = patch.GetType("HardModePatch.UtilityClosureReasonPatch", true);
var closureHooks = (IList<MethodBase>)closurePatch.GetMethod("ResolveHooks").Invoke(null, new object[] { rlo });
Check(closureHooks.Count == 2 && closureHooks.All(h => h != null), "closure presentation resolves game method, RLO getter and field contracts");
closurePatch.GetMethod("TryPatch", Any).Invoke(null, new object[] { rlo });
Check((bool)closurePatch.GetProperty("Available").GetValue(null) && Harmony.GetPatchInfo(closureHooks[0]).Owners.Contains("hartmodpack.gameplay.closurereason"), "closure description postfix installs on reviewed RLO");
var director = Empty(U("UtilitiesDirector"));
Set(director, "_save", save); Set(director, "_loaded", true); Set(director, "_failed", false);
var reasonBook = new Dictionary<string, string>();
string officeKey = Key(office);
reasonBook[officeKey] = "reallife_util_closed_net_install";
Set(director, "_toldClosed", reasonBook);
var instanceField = U("UtilitiesDirector").GetField("<Instance>k__BackingField", Any);
instanceField.SetValue(null, director);
var utilEnabled = rlo.GetType("RealLifeOverhaul.Settings", true).GetField("Utilities", Any);
utilEnabled.SetValue(null, true);
var closureList = (IList)rlo.GetType("RealLifeOverhaul.BusinessState", true).GetField("Closures").GetValue(null);
var closureType = rlo.GetType("RealLifeOverhaul.Closure", true);
object closure = Activator.CreateInstance(closureType);
Set(closure, "Place", officeKey); Set(closure, "Day", 9); Set(closure, "Hour", 2); Set(closure, "Source", 1);
closureList.Clear(); closureList.Add(closure);
Set(save, "Day", 9); Set(save, "Hour", 0); Set(office, "temporarilyClosed", true);
var taskType = game.GetType("Entities.TodoTask", true);
object task = Activator.CreateInstance(taskType);
Set(task, "type", Enum.Parse(taskType.GetField("type").FieldType, "BusinessTemporarilyClosed"));
object address = Empty(taskType.GetField("address").FieldType);
Set(address, "streetName", Get(office, "StreetName")); Set(address, "streetNumber", Get(office, "StreetNumber"));
Set(task, "address", address);
var holderType = ((MethodInfo)closureHooks[0]).ReturnType;
var postfix = closurePatch.GetMethod("Postfix", Any);
string Description(string original = "alert_business_temporarily_closed")
{
    object holder = Activator.CreateInstance(holderType);
    Set(holder, "Key", original); Set(holder, "Prefix", "Office: ");
    object[] call = { task, holder };
    postfix.Invoke(null, call);
    Check((string)Get(call[1], "Prefix") == "Office: ", "description preserves native business prefix");
    return (string)Get(call[1], "Key");
}
const string generic = "alert_business_temporarily_closed";
Check(Description() == "hardmode_closed_net_install", "owned pending-internet closure gets specific label");
SetRule("UtilityClosureReasons", false);
Check(Description() == generic, "closure option off preserves original");
SetRule("UtilityClosureReasons", true);
Check(Description("another_mod_reason") == "another_mod_reason", "another mod's specific description is not overwritten");
Set(closure, "Source", 0); closureList[0] = closure;
Check(Description() == generic, "business issue without utility ownership is not relabelled");
Set(closure, "Source", 1); Set(closure, "Hour", 0); closureList[0] = closure;
Check(Description() == generic, "expired utility closure does not leak cached reason");
Set(closure, "Hour", 2); closureList[0] = closure;
Set(director, "_save", Empty(gameType));
Check(Description() == generic, "previous save's director is ignored");
Set(director, "_save", save); Set(director, "_failed", true);
Check(Description() == generic, "failed utility director is ignored");
Set(director, "_failed", false); utilEnabled.SetValue(null, false);
Check(Description() == generic, "disabled RLO utilities retain generic message");
utilEnabled.SetValue(null, true); Set(office, "temporarilyClosed", false);
Check(Description() == generic, "reopened business does not retain a reason");
Set(office, "temporarilyClosed", true); reasonBook[officeKey] = "unknown_future_reason";
Check(Description() == generic, "unknown reason keeps the original description");
closureList.Clear(); instanceField.SetValue(null, null);
closurePatch.GetMethod("Stop").Invoke(null, null);
Check(Harmony.GetPatchInfo(closureHooks[0]) == null || !Harmony.GetPatchInfo(closureHooks[0]).Owners.Contains("hartmodpack.gameplay.closurereason"), "Stop removes closure presentation hook");

costs.GetMethod("Stop").Invoke(null, null);
Check(hooks.Take(3).All(h => Harmony.GetPatchInfo(h) == null || !Harmony.GetPatchInfo(h).Owners.Contains("hartmodpack.gameplay.runningcosts")), "Stop removes every patch");
Console.WriteLine($"ALL {checks} COST CHECKS PASSED (offline; RLO's nightly books, the Utilities app and real saves remain unverified)");
return 0;

static class CheckLog
{
    public static bool Prefix(string message) { Console.WriteLine("ADAPTER: " + message); return false; }
}
