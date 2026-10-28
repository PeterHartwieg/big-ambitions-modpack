using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;

// What HMP does with an RLO build it hasn't reviewed: every RLO feature stays off, one message says so, a save's
// diploma records are held. Run it with a copy of a released RealLifeOverhaul.dll whose bytes differ from every
// reviewed build (for example the release with one byte appended), so the member shapes match and only the review is
// missing. No game launched, engine methods called or save read; game objects are plain uninitialised containers.
if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: RloGateCheck <game Managed folder> <HARTModPack.dll> <unreviewed RealLifeOverhaul.dll>");
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
var rlo = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2]));
const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
int checks = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL " + name);
    checks++; Console.WriteLine("PASS " + name);
}
Type P(string type) => patch.GetType("HardModePatch." + type, true);
bool Patched(MethodBase m) => Harmony.GetPatchInfo(m) != null && Harmony.GetPatchInfo(m).Owners.Any(o => o.StartsWith("hartmodpack.gameplay"));
var harmony = new Harmony("rlogatecheck");
harmony.Patch(game.GetType("UI.Notification.Notifications", true).GetMethods().Single(m => m.Name == "Show" && m.GetParameters().Length == 8),
    prefix: new HarmonyMethod(typeof(NoticeSink).GetMethod("Capture")));

var gate = P("RloGate");
Check(!(bool)gate.GetMethod("Accepts").Invoke(null, new object[] { rlo }), "an RLO build with no reviewed hash is refused");
Check((bool)gate.GetProperty("Refused").GetValue(null), "the gate reports the refusal");
string message = (string)gate.GetProperty("Message").GetValue(null);
var version = rlo.GetName().Version!.ToString(3);
Check(message.Contains("Real Life Overhaul " + version) && message.Contains("hasn't reviewed") && message.Contains("Factory diplomas, running costs, owner job pay") && message.Contains("night chirping mute"), "one message names the version and every feature switched off");
Check(!(bool)gate.GetMethod("Accepts").Invoke(null, new object[] { rlo }) && ReferenceEquals(message, gate.GetProperty("Message").GetValue(null)), "decided once per assembly");

// Running costs and owner job pay: same shapes as a reviewed build, still not patched.
var costs = P("RunningCostsPatch");
var costHooks = (IList<MethodBase>)costs.GetMethod("ResolveHooks").Invoke(null, new object[] { rlo });
Check(costHooks.All(h => h != null), "the unreviewed build still has every member the running costs touch");
costs.GetMethod("TryPatch", Any).Invoke(null, new object[] { rlo });
Check(!(bool)costs.GetProperty("Available").GetValue(null) && !costHooks.Take(3).Any(Patched), "running costs leave RLO's bills unpatched");
var closure = P("UtilityClosureReasonPatch");
var closureHooks = (IList<MethodBase>)closure.GetMethod("ResolveHooks").Invoke(null, new object[] { rlo });
Check(closureHooks.All(h => h != null), "unreviewed RLO still has the utility closure field and method shapes");
closure.GetMethod("TryPatch", Any).Invoke(null, new object[] { rlo });
Check(!(bool)closure.GetProperty("Available").GetValue(null) && !Patched(closureHooks[0]), "unreviewed RLO leaves the game's closure description unchanged");
var pay = P("OwnerJobPay");
pay.GetMethod("TryPatch", Any).Invoke(null, new object[] { rlo });
var ownerFactor = rlo.GetType("RealLifeOverhaul.CareerCatalogue", true).GetMethod("OwnerFactor", new[] { typeof(int) })!;
Check(!(bool)pay.GetProperty("Available").GetValue(null) && !Patched(ownerFactor), "owner job pay leaves RLO's cut unpatched");

var night = P("NightChirpingPatch");
var nightHooks = (IList<MethodBase>)night.GetMethod("ResolveHooks")!.Invoke(null, new object[] { rlo })!;
Check(nightHooks.All(h => h != null), "unreviewed RLO still has the exact cricket target");
night.GetMethod("TryPatch", Any)!.Invoke(null, new object[] { rlo });
Check(!(bool)night.GetProperty("Available")!.GetValue(null)! && !Patched(nightHooks[0]), "unknown hash leaves RLO crickets unpatched");

// Factory diplomas through the patcher: refused before any hook, catalogue untouched.
var careers = rlo.GetType("RealLifeOverhaul.Careers.StudyCatalogue", true).GetField("All")!;
var before = (Array)careers.GetValue(null)!;
var bridge = Activator.CreateInstance(P("RealLifeOverhaulPatch"))!;
var patchArray = Array.CreateInstance(P("ITargetPatch"), 1); patchArray.SetValue(bridge, 0);
var patcher = Activator.CreateInstance(P("Patcher"), new Harmony("hartmodpack.gameplay"), patchArray)!;
P("Patcher").GetMethod("Install", Any)!.Invoke(patcher, new[] { bridge, rlo });
var target = P("Core.Targets").GetField("RealLifeOverhaul")!.GetValue(null);
Check(P("Patcher").GetMethod("StateOf")!.Invoke(patcher, new[] { target })!.ToString() == "Refused", "the options page shows Real Life Overhaul as not reviewed");
var grab = game.GetType("Controllers.PlayerItemPurchaser", true).GetMethod("GrabItem")!;
Check(!Patched(grab) && ReferenceEquals(before, careers.GetValue(null)) && !(bool)P("RealLifeOverhaulPatch").GetProperty("CoursesRegistered")!.GetValue(null),
    "factory diplomas: no hook, RLO's catalogue unchanged");
Check(P("RealLifeOverhaulPatch").GetMethod("Denied", Any)!.Invoke(null, new object[] { new[] { "ba:itemname_kilnmachine" } }) == null, "machine sales stay unrestricted");

// A save from a reviewed RLO with two diploma records and one ordinary course.
string line = "v=1~h:7|1|9|0|0|0|1|0|0|-1|0~s:3|5000|5000|8~c:19|0|2|3|3|8|1|3900|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|-1;5|0|2|1|1|8|0|900|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|-1"
    + "~r:18|0|1|8|8|7|4|3600|0|0|0|0|2|20|23|0|71|2|23|0|0|0|0|-1~w:~q:~t:-1|0";
object[] readArgs = { line, 0 };
var read = rlo.GetType("RealLifeOverhaul.Careers.CareersStore", true).GetMethod("Read")!.Invoke(null, readArgs)!;
Check(((IList)read.GetType().GetField("Courses")!.GetValue(read)!).Count == 1 && ((IList)read.GetType().GetField("Results")!.GetValue(read)!).Count == 0,
    "this RLO's reader drops the diploma records, so its next save would lose them");
Type gameType = game.GetType("GameInstance", true);
object save = RuntimeHelpers.GetUninitializedObject(gameType);
var regs = (IList)Activator.CreateInstance(gameType.GetField("BuildingRegistrations")!.FieldType)!;
gameType.GetField("BuildingRegistrations")!.SetValue(save, regs);
var data = new Dictionary<string, string> { { "reallife:careers", line } };
gameType.GetField("modData")!.SetValue(save, data);
bool Loaded() => (bool)gate.GetMethod("OnGameLoaded", Any)!.Invoke(null, new[] { save })!;
Check(!Loaded() && data.Count == 1 && NoticeSink.Keys.Count == 0, "character creation (no city yet): nothing read, no notice");
regs.Add(RuntimeHelpers.GetUninitializedObject(regs.GetType().GetGenericArguments()[0]));
gameType.GetField("Day")!.SetValue(save, 31);
var muteRule = P("Core.Rules").GetField("MuteRloNightChirping")!.GetValue(null)!;
muteRule.GetType().GetMethod("Set")!.Invoke(muteRule, new object[] { false });
// A save without diploma records, from a player who uses none of the RLO rules: the log line only.
var plain = new Dictionary<string, string> { { "reallife:careers", "v=1~h:7|1|30~c:5|0|2|1~r:" } };
gameType.GetField("modData")!.SetValue(save, plain);
Check(Loaded() && plain.Count == 1 && NoticeSink.Keys.Count == 0, "no diploma records and no RLO rule on: nothing held, no notice");
gameType.GetField("modData")!.SetValue(save, data);
Check(Loaded() && data["reallife:careers"] == line, "city loaded: RLO's careers record is left as it is");
var hold = P("Core.DiplomaHold");
Check(data.TryGetValue("hardmodepatch:factory_diplomas_held", out var held) && (int)hold.GetMethod("Count")!.Invoke(null, new object[] { held })! == 2
    && !held.Contains("5|0|2|1") && held.Contains("31#19|"), "both diploma records are held in HMP's own save line with the day, nothing else");
Check(NoticeSink.Keys.SequenceEqual(new[] { "hardmode_rlo_unreviewed" }), "one in-game notice says so");
// RLO saves without them; the next load keeps the hold and shows no second notice.
data["reallife:careers"] = "v=1~h:7|1|10~s:3|5000|5000|8~c:5|0|2|1|1|8~r:~w:~q:~t:-1|0";
gameType.GetField("Day")!.SetValue(save, 32);
Check(Loaded() && data["hardmodepatch:factory_diplomas_held"] == held && NoticeSink.Keys.Count == 1, "after RLO resaves, the hold stays as it was and the notice isn't repeated");
Console.WriteLine($"ALL {checks} RLO GATE CHECKS PASSED (offline; the in-game notice and a real save remain unverified)");
return 0;

public static class NoticeSink
{
    public static readonly List<string> Keys = new();
    public static bool Capture(string __1) { Keys.Add(__1); return false; }
}
