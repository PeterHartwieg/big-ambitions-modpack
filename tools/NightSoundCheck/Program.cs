using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;

if (args.Length != 3) { Console.Error.WriteLine("NightSoundCheck <Managed> <HARTModPack.dll> <reviewed RLO.dll>"); return 2; }
AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    if (new[] { "mscorlib", "System", "System.Core", "netstandard" }.Contains(name.Name)) return null;
    string path = Path.Combine(Path.GetFullPath(args[0]), name.Name + ".dll");
    return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
};
var pack = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
var rlo = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2]));
const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
Type P(string name) => pack.GetType("HardModePatch." + name, true)!;
var patch = P("NightChirpingPatch");
object Call(string name, params object[] values) => patch.GetMethod(name, Any)!.Invoke(null, values)!;
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
Check((bool)P("RloGate").GetMethod("Accepts")!.Invoke(null, new object[] { rlo })!, "runtime RloGate accepts the official reviewed DLL " + rlo.GetName().Version);
var sound = rlo.GetType("RealLifeOverhaul.Leisure.LeisureSound", true)!;
var hooks = (IList<MethodBase>)Call("ResolveHooks", rlo);
Check(hooks.Count == 1 && hooks[0] != null && hooks[0].Name == "Tick", "exact Tick(float), AudioSource[] layer, singleton and cricket/layer constants resolve");
var fingerprint = P("IlText").GetMethod("Fingerprint")!;
Console.WriteLine("Tick IL " + fingerprint.Invoke(null, new object[] { hooks[0] }));
var original = PatchProcessor.GetOriginalInstructions(sound.GetMethod("CricketsLoop", Any)!);
Check(original.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "RealLifeOverhaul Ambience Crickets")), "the reviewed generator names the exact cricket clip");
Check(original.Count(i => i.operand is MethodInfo m && m.Name == "Chirp") == 2, "the cricket generator calls its two chirp carriers");

// Reject shape drift independently of the hash gate. These fixtures never instantiate a Unity object.
var audio = sound.GetField("_layer", Any)!.FieldType;
Type Fixture(string fault)
{
    var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("SoundFixture" + fault), AssemblyBuilderAccess.Run).DefineDynamicModule("main");
    var t = module.DefineType("Sound" + fault, TypeAttributes.Public);
    t.DefineField("_layer", fault == "layer" ? typeof(object[]) : audio, FieldAttributes.Private);
    t.DefineField("_instance", t, FieldAttributes.Private | FieldAttributes.Static);
    t.DefineField("Crickets", typeof(int), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.Literal).SetConstant(fault == "index" ? 0 : 1);
    t.DefineField("Layers", typeof(int), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.Literal).SetConstant(fault == "count" ? 5 : 4);
    var m = t.DefineMethod(fault == "name" ? "Other" : "Tick", MethodAttributes.Private | (fault == "static" ? MethodAttributes.Static : 0), typeof(void), new[] { fault == "parameter" ? typeof(int) : typeof(float) });
    m.GetILGenerator().Emit(OpCodes.Ret);
    return t.CreateType()!;
}
Check(((IList<MethodBase>)Call("ResolveType", Fixture("valid"))).All(m => m != null), "matching shape fixture accepted");
foreach (var fault in new[] { "layer", "index", "count", "name", "static", "parameter" })
    Check(((IList<MethodBase>)Call("ResolveType", Fixture(fault))).All(m => m == null), "changed " + fault + " stands down");
Check(((IList<MethodBase>)Call("ResolveType", (object)null!)).All(m => m == null), "missing sound class stands down");

Call("Start");
Check((bool)patch.GetProperty("Available")!.GetValue(null)!, "reviewed sound patch installs through the real startup path");
Check(Harmony.GetPatchInfo(hooks[0])!.Postfixes.Count(p => p.owner == "hartmodpack.gameplay.nightchirping") == 1, "exactly one cricket Tick postfix installed");
var owned = Harmony.GetAllPatchedMethods().Where(m => Harmony.GetPatchInfo(m)!.Owners.Contains("hartmodpack.gameplay.nightchirping")).ToArray();
Check(owned.Length == 1 && owned[0] == hooks[0], "no game, mixer, bird, traffic, crowd or engine hook");
Call("Stop");
Check(!Harmony.GetAllPatchedMethods().Any(m => Harmony.GetPatchInfo(m)!.Owners.Contains("hartmodpack.gameplay.nightchirping")), "unload removes only the sound owner");
Console.WriteLine($"ALL {checks} NIGHT SOUND CHECKS PASSED (offline; no listening test)");
return 0;
