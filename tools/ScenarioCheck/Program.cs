using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using HarmonyLib;

// No game launched, engine methods called, files installed or save written. Game objects are plain
// uninitialised data containers. RLO is optional; its LoanBook and LoanHolds are game-free code.
if (args.Length < 2 || args.Length > 3)
{
    Console.Error.WriteLine("Usage: ScenarioCheck <game Managed folder> <HARTModPack.dll> [RealLifeOverhaul.dll]");
    return 2;
}
string managed = Path.GetFullPath(args[0]), patchPath = Path.GetFullPath(args[1]);
string rloPath = args.Length == 3 ? Path.GetFullPath(args[2]) : null;
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    if (new[] { "mscorlib", "System", "System.Core", "netstandard" }.Contains(name.Name)) return null;
    string path = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
var patch = AssemblyLoadContext.Default.LoadFromAssemblyPath(patchPath);
var gameAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "BigAmbitions.dll"));
Type scenario = patch.GetType("HardModePatch.IndebtedScenario", true);
Type spec = patch.GetType("HardModePatch.Core.IndebtedStart", true);
Type gameType = gameAssembly.GetType("GameInstance", true), loanType = gameAssembly.GetType("Loan", true);
Type addressType = loanType.GetField("bankAddress").FieldType, variablesType = gameType.GetField("gameVariables").FieldType;
Type bankType = gameAssembly.GetType("SpecialServices.Bank.BankSettings", true), helper = gameAssembly.GetType("Helpers.LoanHelper", true);
float cash = (float)spec.GetField("Cash").GetRawConstantValue(), debt = (float)spec.GetField("Debt").GetRawConstantValue();
string key = (string)spec.GetField("StateKey").GetRawConstantValue();
MethodInfo arm = scenario.GetMethod("ArmNewGame", BindingFlags.Static | BindingFlags.NonPublic);
MethodInfo complete = scenario.GetMethod("CompletePending", BindingFlags.Static | BindingFlags.NonPublic);
int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception("FAIL " + message);
    Console.WriteLine("PASS " + message);
}
object Empty(Type type) => RuntimeHelpers.GetUninitializedObject(type);
object Get(object obj, string field) => obj.GetType().GetField(field).GetValue(obj);
void Set(object obj, string field, object value) => obj.GetType().GetField(field).SetValue(obj, value);
IList ListOf(Type type) => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
IList Loans(object game) => (IList)Get(game, "Loans");
Dictionary<string, string> Data(object game) => (Dictionary<string, string>)Get(game, "modData");
object Game()
{
    object game = Empty(gameType), variables = Empty(variablesType);
    Set(variables, "daysPerYear", 60);
    Set(variables, "bankInterestMultiplier", 1.3f);
    Set(variables, "taxPercentage", 51);
    Set(game, "gameVariables", variables);
    Set(game, "Day", 1);
    Set(game, "Money", 12345f);
    Set(game, "Loans", ListOf(loanType));
    Set(game, "modData", new Dictionary<string, string>());
    return game;
}
bool Arm(object game, bool selected = true, bool hardcore = false) => (bool)arm.Invoke(null, new[] { game, (object)selected, hardcore });
bool Complete(object game, object bank, int interest, int payment) => (bool)complete.Invoke(null, new[] { game, bank, interest, payment });
object address = Empty(addressType);
Set(address, "streetName", "ba:street_secondavenue"); Set(address, "streetNumber", 6);
object bankSettings = Empty(bankType);
Set(bankSettings, "yearsToPayLoan", 8); Set(bankSettings, "annualInterestRate", 6);
int interest = (int)helper.GetMethod("CalculateDailyInterestPayment", new[] { typeof(float), bankType, typeof(int), typeof(float) })
    .Invoke(null, new[] { (object)debt, bankSettings, 60, 1.3f });
int payment = (int)helper.GetMethod("CalculateDailyPayment", new[] { typeof(float), bankType, typeof(int) })
    .Invoke(null, new[] { (object)debt, bankSettings, 60 });
Console.WriteLine($"Illustrative contract using actual game formulas (not a discovered bank asset): {interest}/day interest, {payment}/day principal.");

var hooks = (IList<MethodBase>)scenario.GetMethod("ResolveHooks").Invoke(null, null);
Check(hooks.Count == 2 && hooks.All(h => h != null), "both new-game/game-loaded hooks resolve");
var harmony = new Harmony("scenariocheck");
try
{
    scenario.GetMethod("ApplyHooks").Invoke(null, new object[] { harmony, hooks });
    Check(hooks.All(h => Harmony.GetPatchInfo(h).Owners.Contains(harmony.Id)), "both scenario hooks apply with Harmony");
}
finally { harmony.UnpatchAll(harmony.Id); }

object off = Game();
Check(!Arm(off, false) && Data(off).Count == 0, "default-off choice leaves new game untouched");
object conflict = Game();
Check(!Arm(conflict, true, true) && Data(conflict).Count == 0, "Hardcore Ambitions conflict prevents a second scenario writer");
object oldDayOne = Game();
Check(!Complete(oldDayOne, address, interest, payment) && (float)Get(oldDayOne, "Money") == 12345f && Loans(oldDayOne).Count == 0,
    "untagged existing Day 1 save is never converted");
object game = Game(); object variablesBefore = Get(game, "gameVariables");
Check(Arm(game) && !Arm(game), "actual new-game choice latches once");
Check(!Complete(game, null, interest, payment) && Loans(game).Count == 0 && (float)Get(game, "Money") == 12345f,
    "missing bank leaves pending scenario without a partial cash/debt write");
Check(Complete(game, address, interest, payment), "pending scenario creates its native loan");
object loan = Loans(game)[0];
Check(Loans(game).Count == 1 && (float)Get(game, "Money") == cash && (float)Get(loan, "totalAmount") == debt
    && (float)Get(loan, "remainingAmount") == debt, "one starting debt and $551.37 cash; no loan payout");
Check((int)Get(loan, "dailyInterest") == interest && (int)Get(loan, "dailyPayment") == payment,
    "contract uses supplied native-bank terms rather than historical fixed terms");
Check(ReferenceEquals(Get(game, "gameVariables"), variablesBefore) && (int)Get(variablesBefore, "taxPercentage") == 51,
    "custom difficulty object and values remain unchanged");
Check(!Complete(game, address, 16, 158) && Loans(game).Count == 1 && (int)Get(loan, "dailyInterest") == interest,
    "repeated load callback neither duplicates debt nor resets terms");

object pendingWithDebt = Game(); Arm(pendingWithDebt);
Loans(pendingWithDebt).Add(Activator.CreateInstance(loanType, debt, interest, payment, address));
Check(!Complete(pendingWithDebt, address, interest, payment) && Loans(pendingWithDebt).Count == 1
    && (float)Get(pendingWithDebt, "Money") == 12345f, "pending scenario refuses to overwrite another mod's debt");

object reloaded = Game(); Set(reloaded, "modData", new Dictionary<string, string>(Data(game))); Set(reloaded, "Money", 2345f);
object reloadedLoan = Activator.CreateInstance(loanType, debt, interest, payment, address); Loans(reloaded).Add(reloadedLoan);
Set(reloadedLoan, "remainingAmount", debt - payment); Set(reloadedLoan, "dailyPayment", payment + 7);
Check(!Complete(reloaded, address, 16, 158) && (float)Get(reloaded, "Money") == 2345f
    && (int)Get(reloadedLoan, "dailyPayment") == payment + 7, "save/reload preserves cash, paid principal and changed repayment");
Loans(reloaded).Clear();
Check(!Complete(reloaded, address, interest, payment) && Loans(reloaded).Count == 0, "payoff does not resurrect the loan on reload");

if (rloPath != null)
{
    var rlo = AssemblyLoadContext.Default.LoadFromAssemblyPath(rloPath);
    Console.WriteLine("RLO binary " + rlo.GetName().Version + ", SHA-256 " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(rloPath))).ToLowerInvariant());
    Type viewType = rlo.GetType("RealLifeOverhaul.Economy.LoanView", true), markType = rlo.GetType("RealLifeOverhaul.Economy.LoanMark", true);
    Type findingType = rlo.GetType("RealLifeOverhaul.Economy.LoanFinding", true), book = rlo.GetType("RealLifeOverhaul.Economy.LoanBook", true);
    IList marks = ListOf(markType), live = ListOf(viewType), findings = ListOf(findingType);
    void Read(object source)
    {
        live.Clear();
        object view = Activator.CreateInstance(viewType);
        Set(view, "Ref", source); Set(view, "Street", "ba:street_secondavenue"); Set(view, "Number", 6);
        Set(view, "Total", Get(source, "totalAmount")); Set(view, "Remaining", Get(source, "remainingAmount"));
        Set(view, "Payment", Get(source, "dailyPayment")); Set(view, "Interest", Get(source, "dailyInterest"));
        live.Add(view);
    }
    Read(loan); book.GetMethod("Sync").Invoke(null, new object[] { marks, live, 1, findings, true });
    Read(loan); book.GetMethod("Sync").Invoke(null, new object[] { marks, live, 1, findings, false });
    Check(marks.Count == 1 && findings.Count == 0 && ReferenceEquals(Get(marks[0], "Ref"), loan),
        "RLO adopts starting native debt once; it is not new borrowing at initial sync");
    object mark = marks[0];
    Type holds = rlo.GetType("RealLifeOverhaul.Economy.LoanHolds", true), hold = rlo.GetType("RealLifeOverhaul.Economy.LoanHold", true);
    int penalty = Convert.ToInt32(hold.GetField("Penalty").GetRawConstantValue()), frozen = Convert.ToInt32(hold.GetField("Frozen").GetRawConstantValue());
    object[] values = { mark, penalty, interest, payment, 1.3f, 60 };
    holds.GetMethod("Begin").Invoke(null, values);
    object[] apply = { mark, interest, payment, 1.3f, 60 };
    holds.GetMethod("Apply").Invoke(null, apply);
    Check((int)apply[1] > interest && (int)apply[2] == payment, "RLO penalty changes interest without duplicating principal repayment");
    values = new object[] { mark, frozen, apply[1], apply[2], 1.3f, 60 }; holds.GetMethod("Begin").Invoke(null, values);
    apply = new object[] { mark, values[2], values[3], 1.3f, 60 }; holds.GetMethod("Apply").Invoke(null, apply);
    Set(loan, "dailyInterest", apply[1]); Set(loan, "dailyPayment", apply[2]);
    Check((int)apply[1] == 0 && (int)apply[2] == 0, "RLO arrangement freezes native interest and payment");
    patch.GetType("HardModePatch.HardcoreAmbitionsPatch").GetMethod("RecalculatePostfix", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new[] { game });
    Check((int)Get(loan, "dailyInterest") == 0 && (int)Get(loan, "dailyPayment") == 0,
        "legacy Hardcore postfix does not overwrite RLO holds on the standalone scenario");
    object[] restore = { mark, 0, 0, 1.3f, 60 }; holds.GetMethod("Restore").Invoke(null, restore);
    Check((int)restore[1] == interest && (int)restore[2] == payment, "RLO save restore recovers the contract terms exactly");
    apply = new object[] { mark, restore[1], restore[2], 1.3f, 60 }; holds.GetMethod("Apply").Invoke(null, apply);
    Check((int)apply[1] == 0 && (int)apply[2] == 0, "RLO reapplies arrangement after save");
    values = new object[] { mark, frozen, apply[1], apply[2], 1.3f, 60 }; holds.GetMethod("End").Invoke(null, values);
    values = new object[] { mark, penalty, values[2], values[3], 1.3f, 60 }; holds.GetMethod("End").Invoke(null, values);
    Set(loan, "dailyInterest", values[2]); Set(loan, "dailyPayment", values[3]);
    Check((int)values[2] == interest && (int)values[3] == payment, "ending both RLO holds restores the original contract");
    Set(loan, "remainingAmount", debt - payment); Read(loan);
    book.GetMethod("Settle").Invoke(null, new object[] { marks, live, findings });
    Check(findings.Count == 1 && Get(findings[0], "Note").ToString() == "Paid", "RLO reads one native principal repayment as one paid instalment");
    findings.Clear(); object afterReload = Activator.CreateInstance(loanType, debt, interest, payment, address);
    Set(afterReload, "remainingAmount", debt - payment); Read(afterReload);
    book.GetMethod("Sync").Invoke(null, new object[] { marks, live, 2, findings, true });
    Check(marks.Count == 1 && ReferenceEquals(Get(marks[0], "Ref"), afterReload), "RLO matches reloaded debt without a duplicate mark");
}
// Rent rebalance: hooks resolve and apply, the scaling, and rivals' estimates keep the game's rent.
{
    Type rent = patch.GetType("HardModePatch.RentRebalance", true);
    object rule = patch.GetType("HardModePatch.Core.Rules", true).GetField("RentRebalance").GetValue(null);
    void SetRule(bool on) => rule.GetType().GetMethod("Set").Invoke(rule, new object[] { on });
    var rentHooks = (IList)rent.GetMethod("ResolveHooks").Invoke(null, null);
    Check(rentHooks.Count == 2 && rentHooks.Cast<object>().All(h => h != null), "rent hooks resolve against the game's methods");
    var rentHarmony = new Harmony("scenariocheck.rent");
    rent.GetMethod("ApplyHooks").Invoke(null, new object[] { rentHarmony, rentHooks });
    Check(Harmony.GetPatchInfo((MethodBase)rentHooks[0])?.Postfixes.Count == 1 && Harmony.GetPatchInfo((MethodBase)rentHooks[1])?.Finalizers.Count == 1, "rent hooks apply with Harmony");
    rentHarmony.UnpatchAll(rentHarmony.Id);
    MethodInfo postfix = rent.GetMethod("RentPostfix", BindingFlags.Static | BindingFlags.NonPublic);
    MethodInfo prefix = rent.GetMethod("EstimatePrefix", BindingFlags.Static | BindingFlags.NonPublic);
    MethodInfo finalizer = rent.GetMethod("EstimateFinalizer", BindingFlags.Static | BindingFlags.NonPublic);
    Type buildingType = gameAssembly.GetType("Buildings.Building", true), registrationType = gameAssembly.GetType("BuildingRegistration", true);
    int Rent(string type, int market)
    {
        object building = Empty(buildingType);
        Set(building, "BuildingType", type);
        var args = new object[] { building, market };
        postfix.Invoke(null, args);
        return (int)args[1];
    }
    SetRule(false);
    Check(Rent("ba:buildingtype_residential", 39) == 39, "rent unchanged while the rule is off");
    SetRule(true);
    Check(Rent("ba:buildingtype_residential", 39) == 78 && Rent("ba:buildingtype_retail", 34) == 102, "flats at twice and shops at three times the market rent");
    // BuildingOwnedByPlayer reads the current save's real estate: give it an empty list.
    object current = Game();
    Set(current, "realEstate", ListOf(gameType.GetField("realEstate").FieldType.GetGenericArguments()[0]));
    gameAssembly.GetType("SaveGameManager", true).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, current);
    object rival = Empty(registrationType);
    Set(rival, "RentedByPlayer", false);
    var state = new object[] { rival, false };
    prefix.Invoke(null, state);
    Check((bool)state[1] && Rent("ba:buildingtype_retail", 34) == 34, "a rival's weekly estimate keeps the game's rent");
    finalizer.Invoke(null, new object[] { null, true });
    Check(Rent("ba:buildingtype_retail", 34) == 102, "the scaled rent returns after the rival estimate");
    SetRule(false);
}
// Happiness rebalance: hooks resolve and apply, values set and restored, outside grants, no home in a hotel.
{
    Type hap = patch.GetType("HardModePatch.HappinessRebalance", true);
    object rule = patch.GetType("HardModePatch.Core.Rules", true).GetField("HappinessRebalance").GetValue(null);
    void SetHapRule(bool on) => rule.GetType().GetMethod("Set").Invoke(rule, new object[] { on });
    var hapHooks = (IList)hap.GetMethod("ResolveHooks").Invoke(null, null);
    Check(hapHooks.Count == 3 && hapHooks.Cast<object>().All(h => h != null), "happiness hooks resolve against the game's methods");
    var hapHarmony = new Harmony("scenariocheck.happiness");
    hap.GetMethod("ApplyHooks").Invoke(null, new object[] { hapHarmony, hapHooks });
    Check(hapHooks.Cast<MethodBase>().All(h => Harmony.GetPatchInfo(h) != null), "happiness hooks apply with Harmony");
    hapHarmony.UnpatchAll(hapHarmony.Id);
    Type modType = gameAssembly.GetType("Helpers.HappinessModifier", true);
    object Mod(string type, int amount, int hours, int max)
    {
        object m = Empty(modType);
        Set(m, "type", "ba:happinessmodifier_" + type); Set(m, "amount", amount); Set(m, "hoursDuration", hours); Set(m, "maxHoursDuration", max);
        return m;
    }
    object show = Mod("watched_show", 25, 2, 72), noHome = Mod("no_home", -50, -1, -1), first = Mod("first_apartment", 60, 120, -1);
    IList list = ListOf(modType); list.Add(show); list.Add(noHome); list.Add(first);
    MethodInfo loaded = hap.GetMethod("LoadedPostfix", BindingFlags.Static | BindingFlags.NonPublic);
    SetHapRule(true);
    loaded.Invoke(null, new object[] { list });
    Check((int)Get(show, "amount") == 12 && (int)Get(show, "maxHoursDuration") == 120 && (int)Get(first, "amount") == 60, "listed modifiers set, others untouched");
    loaded.Invoke(null, new object[] { list });
    Check((int)Get(show, "amount") == 12, "a second load doesn't compound the values");
    MethodInfo add = hap.GetMethod("AddPrefix", BindingFlags.Static | BindingFlags.NonPublic);
    var grant = new object[] { "ba:happinessmodifier_watched_show", 6, true };
    add.Invoke(null, grant);
    Check((int)grant[1] == 120 && !(bool)grant[2], "one outside visit fills the whole duration");
    grant = new object[] { "ba:happinessmodifier_read", 36, true };
    add.Invoke(null, grant);
    Check((int)grant[1] == 36 && (bool)grant[2], "home activities keep banking the game's hours");
    if (rloPath != null)
    {
        // The game's registry holds the modifiers; RLO's TravelState says whether a room is booked.
        Type helperType = gameAssembly.GetType("Helpers.HappinessHelper", true);
        var registryDict = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modType));
        registryDict["ba:happinessmodifier_no_home"] = noHome;
        helperType.GetField("Modifiers", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, registryDict);
        hap.GetField("registry", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, helperType.GetField("Modifiers", BindingFlags.Static | BindingFlags.NonPublic));
        Type travel = AssemblyLoadContext.Default.Assemblies.First(x => x.GetName().Name == "RealLifeOverhaul").GetType("RealLifeOverhaul.TravelState", true);
        MethodInfo update = hap.GetMethod("UpdatePrefix", BindingFlags.Static | BindingFlags.NonPublic);
        travel.GetField("HotelId").SetValue(null, "garment_hostel");
        update.Invoke(null, null);
        Check((int)Get(noHome, "amount") == -15, "'no home' is -15 while RLO has a hotel room booked");
        travel.GetField("HotelId").SetValue(null, null);
        update.Invoke(null, null);
        Check((int)Get(noHome, "amount") == -50, "'no home' is back to -50 without a booking");
    }
    SetHapRule(false);
    var currentField = gameAssembly.GetType("SaveGameManager", true).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic);
    var previousSave = currentField.GetValue(null);
    var refresh = (MethodBase)hapHooks[2];
    var refreshGuard = new Harmony("scenariocheck.menu-happiness");
    refreshGuard.Patch(refresh, prefix: new HarmonyMethod(typeof(MenuHappinessProbe).GetMethod("Prefix")));
    try
    {
        currentField.SetValue(null, null);
        MenuHappinessProbe.Calls = 0;
        hap.GetMethod("Reapply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Check(MenuHappinessProbe.Calls == 0, "applying the rule without a loaded save never calls the player's happiness update");
        // Back at the main menu after a game: the last save is still current and the game's own singleton
        // lifecycle has the menu controller registered, as MainMenuController.Awake does.
        Type menuType = gameAssembly.GetType("MainMenuController", true);
        var lifecycleCall = PatchProcessor.GetOriginalInstructions(menuType.BaseType.GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Select(i => i.operand).OfType<MethodInfo>().Last(m => m.Name == "UnregisterSingleton");
        Type lifecycle = lifecycleCall.DeclaringType;
        var menu = RuntimeHelpers.GetUninitializedObject(menuType);
        AssemblyLoadContext.Default.Assemblies.First(x => x.GetName().Name == "UnityEngine.CoreModule").GetType("UnityEngine.Object", true)
            .GetField("m_CachedPtr", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, new IntPtr(1));
        var menuOpen = menuType.GetProperty("IsInitialized", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        currentField.SetValue(null, Game());
        lifecycle.GetMethod("RegisterSingleton").Invoke(null, new[] { menuType, menu });
        try
        {
            Check((bool)menuOpen.GetValue(null), "the registered menu controller reads as the open main menu");
            hap.GetMethod("Reapply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Check(MenuHappinessProbe.Calls == 0, "a save kept from the last game at the main menu never calls the player's happiness update");
        }
        finally { lifecycleCall.Invoke(null, new[] { menuType, menu }); }
        Check(!(bool)menuOpen.GetValue(null), "the menu controller is unregistered again");
        hap.GetMethod("Reapply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Check(MenuHappinessProbe.Calls == 1, "in a loaded city the rule change still refreshes the player's happiness once");
    }
    finally { currentField.SetValue(null, previousSave); refreshGuard.UnpatchAll(refreshGuard.Id); }
}
Console.WriteLine($"ALL {checks} SCENARIO CHECKS PASSED (offline; engine lifecycle, bank assets and real settlement remain unverified)");
return 0;

public static class MenuHappinessProbe
{
    public static int Calls;
    public static bool Prefix() { Calls++; return false; }
}
