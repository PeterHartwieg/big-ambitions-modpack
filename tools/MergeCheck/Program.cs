using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;

// Separate process for each order: Mono, like this process, retains loaded assemblies until exit.
if (args.Length != 4 || !new[] { "legacy-first", "pack-first", "pack-only" }.Contains(args[3]))
{
    Console.Error.WriteLine("MergeCheck <game DLL folder> <HARTModPack.dll> <HardModePatch.dll> <legacy-first|pack-first|pack-only>");
    return 2;
}
const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
string managed = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    if (new[] { "mscorlib", "System", "System.Core", "netstandard" }.Contains(name.Name)) return null;
    var path = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
};
int checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception("FAIL " + label); checks++; Console.WriteLine("PASS " + label); }
Assembly Load(string path) => AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
var pack = Load(args[1]);
Type T(string name) => pack.GetType(name, true)!;
void Call(string type, string method, params object[] values) => T(type).GetMethod(method, Any)!.Invoke(null, values);
bool Flag(string name) => (bool)T("HardModePatch.GameplayFeatures").GetProperty(name, Any)!.GetValue(null)!;
const string features = "HardModePatch.GameplayFeatures";
const string prefix = "hartmodpack.gameplay";
int OwnedHooks() => Harmony.GetAllPatchedMethods().Count(m => Harmony.GetPatchInfo(m)!.Owners.Any(o => o.StartsWith(prefix, StringComparison.Ordinal)));

Check(pack.GetName().Name == "HARTModPack" && pack.GetName().Version!.ToString() == "1.1.0.0", "merged assembly identity and version");
Check(!pack.GetReferencedAssemblies().Any(a => a.Name == "HardModePatch"), "no runtime assembly dependency on HardModePatch");
Check(pack.GetCustomAttributesData().Count(a => a.AttributeType.Name == "RegisterModClassAttribute") == 1, "one registered mod entry");
// Stub only preference reads and engine logging for a .NET harness; the real lifecycle, hooks, options and cleanup run.
var harness = new Harmony("mergecheck.engine-stubs");
var skip = new HarmonyMethod(typeof(Stubs).GetMethod(nameof(Stubs.Skip))!);
harness.Patch(T("HardModePatch.Options").GetMethod("Load", Any)!, prefix: skip);
var api = Load(Path.Combine(managed, "BigAmbitions.ModAPI.dll"));
var lifecycle = api.GetType("BAModAPI.Services.UnityLifecycleProvider", true)!;
harness.Patch(lifecycle.GetMethod("EnsureInstance", Any)!, prefix: skip);
foreach (var log in new[] { "HardModePatch.Log", "HARTModPack.Log" })
    foreach (var method in T(log).GetMethods(Any).Where(m => m.DeclaringType == T(log))) harness.Patch(method, prefix: skip);

// An unrelated owner's prefix, using the OLD Harmony id, must survive pack cleanup.
var legacyOwner = new Harmony("hardmodepatch");
var sentinel = typeof(Stubs).GetMethod(nameof(Stubs.Sentinel))!;
legacyOwner.Patch(sentinel, prefix: skip);

object? legacyEntry = null;
void StartLegacy()
{
    var old = Load(args[2]);
    harness.Patch(old.GetType("HardModePatch.Options", true)!.GetMethod("Load", Any)!, prefix: skip);
    foreach (var method in old.GetType("HardModePatch.Log", true)!.GetMethods(Any).Where(m => m.DeclaringType!.Assembly == old))
        harness.Patch(method, prefix: skip);
    var contextType = api.GetType("BAModAPI.ModContext", true)!;
    var context = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(contextType);
    contextType.GetFields(Any).Single(f => f.Name.Contains("ModId", StringComparison.OrdinalIgnoreCase)).SetValue(context, "merge-check-legacy");
    legacyEntry = Activator.CreateInstance(old.GetType("HardModePatch.HardModePatchMod", true)!)!;
    ((Task)legacyEntry.GetType().GetMethod("OnLoadAsync")!.Invoke(legacyEntry, new[] { context })!).GetAwaiter().GetResult();
    Check(Harmony.GetAllPatchedMethods().Count(m => Harmony.GetPatchInfo(m)!.Owners.Any(o => o.StartsWith("hardmodepatch.", StringComparison.Ordinal))) > 5,
        "actual published legacy entry starts its gameplay hooks");
}
if (args[3] == "legacy-first") StartLegacy();
Call(features, "Begin", "merge-check-pack");
if (args[3] == "legacy-first")
{
    Check(Flag("Blocked") && !Flag("Active"), "legacy-first refuses gameplay before startup");
    Check(OwnedHooks() == 0, "legacy-first installs no pack hooks");
}
else
{
    Check(Flag("Active") && !Flag("Blocked"), "pack starts without legacy DLL");
    Check(OwnedHooks() > 5, "real standalone gameplay hooks installed");
    // Build the actual merged page without registering a discovery watcher or writing a locale table.
    T("HARTModPack.StatusPage").GetField("modId", Any)!.SetValue(null, "merge-check-pack");
    Call("HARTModPack.StatusPage", "RefreshGameplay");
    var modApi = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "BigAmbitions.ModAPI");
    var service = modApi.GetType("BigAmbitions.Mods.OptionsService", true)!;
    var entries = (System.Collections.IDictionary)service.GetField("Entries", Any)!.GetValue(null)!;
    Check(entries.Count == 1 && entries.Contains("merge-check-pack"), "one options page for gameplay and pack status");
    var page = entries["merge-check-pack"]!;
    var controls = ((System.Collections.IEnumerable)page.GetType().GetProperty("Options")!.GetValue(page)!).Cast<object>().ToArray();
    var ruleIds = ((System.Collections.IEnumerable)T("HardModePatch.Core.Rules").GetField("All")!.GetValue(null)!).Cast<object>().Select(r => (string)r.GetType().GetField("Id")!.GetValue(r)!).ToArray();
    var ids = controls.Select(o => (string?)o.GetType().GetProperty("Id")!.GetValue(o)).ToArray();
    Check(ruleIds.Length == 22 && ruleIds.All(id => ids.Count(i => i == id) == 1), "all 22 rule toggles occur once on the merged page");
    var buttonPreviews = ((IEnumerable<string>)T("HardModePatch.SeriesSettingsButton").GetMethod("Preview")!.Invoke(null, null)!).ToArray();
    Check(buttonPreviews.Count(p => p.Contains(" -> m:merge-check-pack:")) == 7, "preset resolves all seven own values to the Mod Pack page");
    if (args[3] == "pack-first")
    {
        StartLegacy();
        Check(Flag("Blocked") && !Flag("Active"), "late AssemblyLoad synchronously blocks gameplay");
        Check(OwnedHooks() == 0, "all pack hooks removed before legacy loader continues");
        Call("HARTModPack.StatusPage", "RefreshGameplay");
        page = entries["merge-check-pack"]!;
        controls = ((System.Collections.IEnumerable)page.GetType().GetProperty("Options")!.GetValue(page)!).Cast<object>().ToArray();
        Check(!controls.Any(o => o.GetType().Name == "ToggleOption"), "blocked page has no active gameplay controls");
    }
}
Call(features, "End");
Check(OwnedHooks() == 0, "unload removes every pack hook");
Check(lifecycle.GetFields(Any).Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType)).All(f =>
    f.GetValue(null) is not Delegate handlers || handlers.GetInvocationList().All(d => d.Method.DeclaringType?.Assembly != pack)), "unload removes pack lifecycle callbacks");
Check(Harmony.GetPatchInfo(sentinel)!.Owners.Contains("hardmodepatch"), "legacy Harmony owner untouched");
Call(features, "Begin", "second-copy");
if (args[3] != "pack-only") Check(Flag("Blocked") && OwnedHooks() == 0, "duplicate copy cannot resume after legacy DLL has loaded");
else Check(Flag("Active") && OwnedHooks() > 5, "second pack copy can take over without legacy DLL");
Call(features, "End");
Check(OwnedHooks() == 0, "second cleanup leaves no pack hooks");
if (legacyEntry != null)
    ((Task)legacyEntry.GetType().GetMethod("OnUnloadAsync")!.Invoke(legacyEntry, null)!).GetAwaiter().GetResult();
legacyOwner.UnpatchAll(legacyOwner.Id);
harness.UnpatchAll(harness.Id);
Console.WriteLine($"ALL {checks} MERGE CHECKS PASSED ({args[3]}, offline)");
return 0;

public static class Stubs
{
    public static bool Skip() => false;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void Sentinel() { }
}
