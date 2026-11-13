using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;

// What HMP does with an RLO build it hasn't reviewed: every RLO feature stays off, one message says so, a save's
// diploma records are held. Run it with a copy of a released RealLifeOverhaul.dll whose bytes differ from every
// reviewed build (for example the release with one byte appended), so the member shapes match and only the review is
// missing. No game launched, engine methods called or save read; game objects are plain uninitialised containers.
if (args.Length < 3 || args.Length > 5 || (args.Length > 3 && args[3] != "--as-hotfix"))
{
    Console.Error.WriteLine("Usage: RloGateCheck <game Managed folder> <HARTModPack.dll> <unreviewed RealLifeOverhaul.dll> [--as-hotfix [member to break]]");
    return 2;
}
string rloPath = Path.GetFullPath(args[2]);
bool hotfix = args.Length > 3;
string broken = args.Length > 4 ? args[4] : null;
if (hotfix)
{
    var bytes = File.ReadAllBytes(rloPath);
    using (var pe = new System.Reflection.PortableExecutable.PEReader(new MemoryStream(bytes)))
    {
        var md = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        int start = pe.PEHeaders.MetadataStartOffset;
        int row = start + System.Reflection.Metadata.Ecma335.MetadataReaderExtensions.GetTableMetadataOffset(md, System.Reflection.Metadata.Ecma335.TableIndex.Assembly);
        int build = BitConverter.ToUInt16(bytes, row + 8);
        BitConverter.GetBytes((ushort)(build + 1)).CopyTo(bytes, row + 8);
        if (broken != null)
        {
            int heap = start + System.Reflection.Metadata.Ecma335.MetadataReaderExtensions.GetHeapMetadataOffset(md, System.Reflection.Metadata.Ecma335.HeapIndex.String);
            int size = System.Reflection.Metadata.Ecma335.MetadataReaderExtensions.GetHeapSize(md, System.Reflection.Metadata.Ecma335.HeapIndex.String);
            var name = System.Text.Encoding.UTF8.GetBytes("\0" + broken + "\0");
            int at = bytes.AsSpan(heap, size).IndexOf(name);
            if (at < 0) throw new Exception("FAIL " + broken + " is not a member name in this RLO");
            bytes[heap + at + name.Length - 2] = (byte)'X';
        }
    }
    rloPath = Path.Combine(Path.GetTempPath(), "rlogatecheck-" + Environment.ProcessId, "RealLifeOverhaul.dll");
    Directory.CreateDirectory(Path.GetDirectoryName(rloPath)!);
    File.WriteAllBytes(rloPath, bytes);
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
var rlo = AssemblyLoadContext.Default.LoadFromAssemblyPath(rloPath);
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
if (hotfix)
{
    try { return Hotfix(); }
    finally { Directory.Delete(Path.GetDirectoryName(rloPath)!, true); }
}
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
// The rent rule on a refused RLO 1.0 or later: RLO's living reads are not separated, so the rule leaves every rent alone.
var rentRule = P("RentRebalance");
rentRule.GetMethod("Start")!.Invoke(null, null);
bool rloLine = rlo.GetName().Version!.Major >= 1;
Check((bool)rentRule.GetProperty("Available")!.GetValue(null)! != rloLine,
    rloLine ? "the rent rule reports unavailable on a refused RLO 1.0 or later" : "the rent rule stays available on a refused RLO before 1.0");
rentRule.GetMethod("Stop")!.Invoke(null, null);

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
// RLO 1.0 uses ids 18, 19 and 34 for its own building-trade courses: its reader keeps those records as its own.
var rloIds = before.Cast<object>().Select(c => (int)c.GetType().GetField("Id")!.GetValue(c)!).ToArray();
bool taken = rloIds.Intersect(new[] { 18, 19, 34 }).Any();
if (taken)
    Check(((IList)read.GetType().GetField("Courses")!.GetValue(read)!).Count == 2 && ((IList)read.GetType().GetField("Results")!.GetValue(read)!).Count == 1,
        "this RLO has courses of its own under ids 18, 19 and 34 and its reader keeps those records");
else
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
if (taken)
{
    // The save above is a 0.50.0 save with HMP diplomas (24-field records). RLO 1.0 reads them as its own courses,
    // so HMP keeps a copy of the originals; RLO's line is left as it is.
    Check(data.TryGetValue("hardmodepatch:factory_diplomas_held", out var kept) && (int)hold.GetMethod("Count")!.Invoke(null, new object[] { kept })! == 2
        && !kept.Contains("5|0|2|1") && kept.Contains("31#19|") && kept.Contains("31#18|"), "HMP's records from a 0.50.0 save are held with the day, nothing else");
    Check(NoticeSink.Keys.SequenceEqual(new[] { "hardmode_rlo_unreviewed" }), "one in-game notice says so");
    // RLO 1.0.5 reads and saves the line: the records come back with 25 fields, as its own courses.
    var store = rlo.GetType("RealLifeOverhaul.Careers.CareersStore", true)!;
    string resaved = (string)store.GetMethod("Write")!.Invoke(null, new[] { read })!;
    Check(resaved.Contains("~c:19|") && resaved.Contains("~r:18|"), "RLO 1.0.5 keeps and rewrites those records as its own courses");
    data["reallife:careers"] = resaved;
    gameType.GetField("Day")!.SetValue(save, 32);
    Check(Loaded() && data["reallife:careers"] == resaved && data["hardmodepatch:factory_diplomas_held"] == kept && NoticeSink.Keys.Count == 1,
        "after RLO 1.0.5 resaves, the hold keeps the originals unchanged and the notice isn't repeated");
    // A save started under RLO 1.0 with its own bricklaying course and crane ticket.
    var own = new Dictionary<string, string> { { "reallife:careers", "v=1~h:7|1|9~c:18|0|2|3|3|8|1|2400|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|-1|0"
        + "~r:19|0|1|3|3|7|4|650|0|0|0|0|2|20|23|0|71|2|23|0|0|0|0|-1|0~w:~q:~t:-1|0" } };
    gameType.GetField("modData")!.SetValue(save, own);
    Check(Loaded() && own.Count == 1, "RLO 1.0's own records under ids 18, 19 and 34 are never held");
    Console.WriteLine($"ALL {checks} RLO GATE CHECKS PASSED (offline; the in-game notice and a real save remain unverified)");
    return 0;
}
Check(data.TryGetValue("hardmodepatch:factory_diplomas_held", out var held) && (int)hold.GetMethod("Count")!.Invoke(null, new object[] { held })! == 2
    && !held.Contains("5|0|2|1") && held.Contains("31#19|"), "both diploma records are held in HMP's own save line with the day, nothing else");
Check(NoticeSink.Keys.SequenceEqual(new[] { "hardmode_rlo_unreviewed" }), "one in-game notice says so");
// RLO saves without them; the next load keeps the hold and shows no second notice.
data["reallife:careers"] = "v=1~h:7|1|10~s:3|5000|5000|8~c:5|0|2|1|1|8~r:~w:~q:~t:-1|0";
gameType.GetField("Day")!.SetValue(save, 32);
Check(Loaded() && data["hardmodepatch:factory_diplomas_held"] == held && NoticeSink.Keys.Count == 1, "after RLO resaves, the hold stays as it was and the notice isn't repeated");
Console.WriteLine($"ALL {checks} RLO GATE CHECKS PASSED (offline; the in-game notice and a real save remain unverified)");
return 0;

int Hotfix()
{
    var version = rlo.GetName().Version!;
    Check(version.Major == 1 && version.Minor == 0 && version.Build != 5, "the copy reports Real Life Overhaul " + version.ToString(3) + ", a 1.0.x release other than the reviewed 1.0.5");
    Check((bool)gate.GetMethod("Accepts")!.Invoke(null, new object[] { rlo })! && !(bool)gate.GetProperty("Refused")!.GetValue(null)! && gate.GetProperty("Message")!.GetValue(null) == null,
        "a 1.0.x release with an unknown hash is accepted by version, with no refusal message");
    var off = new List<string>();
    void Feature(string name, bool available, bool hooksHold)
    {
        Check(available == hooksHold, name + (hooksHold ? " checks its own hooks and runs" : " finds its hooks changed and stays off on its own"));
        if (!available) off.Add(name);
    }
    var costs = P("RunningCostsPatch");
    var costHooks = (IList<MethodBase>)costs.GetMethod("ResolveHooks")!.Invoke(null, new object[] { rlo })!;
    costs.GetMethod("TryPatch", Any)!.Invoke(null, new object[] { rlo });
    Feature("running costs", (bool)costs.GetProperty("Available")!.GetValue(null)!, costHooks.All(h => h != null) && (bool)costs.GetMethod("NeedsNetReadsList")!.Invoke(null, new object[] { costHooks[2] })!);
    var closure = P("UtilityClosureReasonPatch");
    var closureHooks = (IList<MethodBase>)closure.GetMethod("ResolveHooks")!.Invoke(null, new object[] { rlo })!;
    closure.GetMethod("TryPatch", Any)!.Invoke(null, new object[] { rlo });
    Feature("utility closure reasons", (bool)closure.GetProperty("Available")!.GetValue(null)!, closureHooks.All(h => h != null));
    var pay = P("OwnerJobPay");
    pay.GetMethod("TryPatch", Any)!.Invoke(null, new object[] { rlo });
    var factor = rlo.GetType("RealLifeOverhaul.CareerCatalogue", true)!.GetMethod("OwnerFactor", new[] { typeof(int) });
    Feature("owner job pay", (bool)pay.GetProperty("Available")!.GetValue(null)!, factor != null);
    var night = P("NightChirpingPatch");
    var nightHooks = (IList<MethodBase>)night.GetMethod("ResolveHooks")!.Invoke(null, new object[] { rlo })!;
    night.GetMethod("TryPatch", Any)!.Invoke(null, new object[] { rlo });
    Feature("night chirping mute", (bool)night.GetProperty("Available")!.GetValue(null)!, nightHooks.All(h => h != null));
    var readers = (IList<MethodBase>)P("RentRebalance").GetMethod("ResolveLivingReaders")!.Invoke(null, new object[] { rlo })!;
    Check(readers.Count == 5 && readers.All(r => r != null) || broken != null, "the rent rule finds RLO's five living economy rent reads on this 1.0.x release");
    var rentRule = P("RentRebalance");
    rentRule.GetMethod("Start")!.Invoke(null, null);
    Feature("rent rule", (bool)rentRule.GetProperty("Available")!.GetValue(null)!, readers.All(r => r != null));
    rentRule.GetMethod("Stop")!.Invoke(null, null);

    var careers = rlo.GetType("RealLifeOverhaul.Careers.StudyCatalogue", true)!.GetField("All")!;
    var before = careers.GetValue(null);
    var bridge = Activator.CreateInstance(P("RealLifeOverhaulPatch"))!;
    var patchArray = Array.CreateInstance(P("ITargetPatch"), 1); patchArray.SetValue(bridge, 0);
    var patcher = Activator.CreateInstance(P("Patcher"), new Harmony("hartmodpack.gameplay"), patchArray)!;
    var logs = typeof(LogSink).GetMethod("Capture")!;
    var logHarmony = new Harmony("rlogatecheck.log");
    foreach (var level in new[] { "Info", "Warn", "Error" }) logHarmony.Patch(patch.GetType("HardModePatch.Log", true)!.GetMethod(level, Any)!, prefix: new HarmonyMethod(logs));
    P("Patcher").GetMethod("Install", Any)!.Invoke(patcher, new[] { bridge, rlo });
    var target = P("Core.Targets").GetField("RealLifeOverhaul")!.GetValue(null);
    var grab = game.GetType("Controllers.PlayerItemPurchaser", true)!.GetMethod("GrabItem")!;
    string diplomasOff = (string)P("Core.RloLine").GetField("DiplomasOff")!.GetRawConstantValue()!;
    Check(P("Patcher").GetMethod("StateOf")!.Invoke(patcher, new[] { target })!.ToString() == "OffOnRlo1" && (bool)P("RealLifeOverhaulPatch").GetProperty("OffOnRlo1")!.GetValue(null)!,
        "factory diplomas: the Mods page shows 'off on RLO 1.0'");
    string companionState = (string)P("CompanionGame").GetMethod("TargetName", Any)!.Invoke(null, new[] { P("Patcher").GetMethod("StateOf")!.Invoke(patcher, new[] { target }) })!;
    Check(new[] { "missing", "verified", "compatible", "refused", "incompatible" }.Contains(companionState) && companionState == "incompatible",
        "companion v1: the stood-down RLO target reports 'incompatible', inside the contract's states");
    Check(LogSink.Lines.Count(l => l == diplomasOff) == 1 && !LogSink.Lines.Any(l => l.Contains("patching failed") || l.Contains("   at ")), "one 'off on RLO 1.0' log line, no stack trace");
    Check(!Patched(grab) && ReferenceEquals(before, careers.GetValue(null)) && !(bool)P("RealLifeOverhaulPatch").GetProperty("CoursesRegistered")!.GetValue(null)!, "factory diplomas stay off: no hook, RLO's catalogue unchanged");
    logHarmony.UnpatchAll(logHarmony.Id);
    if (broken != null) Check(off.Count == 1, "only " + string.Join(", ", off) + " is off after breaking " + broken + "; the rest of the RLO integration runs");
    else Check(off.Count == 0, "every feature runs on this 1.0.x release");
    Console.WriteLine($"ALL {checks} RLO 1.0.x HOTFIX CHECKS PASSED (offline; a real hotfix in the game remains unverified)");
    return 0;
}

public static class LogSink
{
    public static readonly List<string> Lines = new();
    public static bool Capture(string message) { Lines.Add(message); return false; }
}

public static class NoticeSink
{
    public static readonly List<string> Keys = new();
    public static bool Capture(string __1) { Keys.Add(__1); return false; }
}
