using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;

// Uses the exact local game/target DLLs without starting Unity, touching a save or installing a mod.
// Non-null engine handles below are inert data only: no native method may be called on them.
if (args.Length != 3) { Console.Error.WriteLine("Usage: CompatibilityCheck <Managed folder> <HARTModPack.dll> <target DLL folder>"); return 2; }
string managed = Path.GetFullPath(args[0]), patchPath = Path.GetFullPath(args[1]), targets = Path.GetFullPath(args[2]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    if (new[] { "mscorlib", "System", "System.Core", "netstandard" }.Contains(name.Name)) return null;
    foreach (string dir in new[] { managed, targets })
    {
        string path = Path.Combine(dir, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
    }
    return null;
};
var patch = AssemblyLoadContext.Default.LoadFromAssemblyPath(patchPath);
var game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "BigAmbitions.dll"));
var hdrp = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "Unity.RenderPipelines.HighDefinition.Runtime.dll"));
var bakery = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(targets, "Bakery.dll"));
var hyperreal = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(targets, "PolyModsHyperreal.dll"));
Type rules = patch.GetType("HardModePatch.Core.Rules", true);
int checks = 0;
void Check(bool ok, string name) { checks++; if (!ok) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
object Invoke(Type type, string method, params object[] values) => AccessTools.Method(type, method).Invoke(null, values);
object Rule(string name) => rules.GetField(name).GetValue(null);
void Toggle(string name, bool on) { object r = Rule(name); r.GetType().GetMethod("Set").Invoke(r, new object[] { on }); }
object InertEngineObject(Type type, long handle)
{
    object value = RuntimeHelpers.GetUninitializedObject(type);
    AccessTools.Field(type, "m_CachedPtr").SetValue(value, new IntPtr(handle));
    return value;
}

Type compatibility = patch.GetType("HardModePatch.BinaryCompatibility", true);
Check((bool)Invoke(compatibility, "Reviewed", game, compatibility.GetField("GameHash").GetRawConstantValue()), "audited game binary accepted");
Check(!(bool)Invoke(compatibility, "Reviewed", game, "0000000000000000"), "changed binary rejected");
Check(!(bool)Invoke(compatibility, "Reviewed", null, "0000000000000000"), "unreadable binary rejected");

foreach (var entry in new[] { ("BakeryPatch", bakery), ("HyperrealPatch", hyperreal) })
{
    Type type = patch.GetType("HardModePatch." + entry.Item1, true);
    object instance = Activator.CreateInstance(type);
    var hooks = (IList<MethodBase>)type.GetMethod("ResolveHooks").Invoke(instance, new object[] { entry.Item2 });
    Check(hooks.All(h => h != null), entry.Item1 + " all compatibility/gameplay hooks resolve");
    Check(hooks.All(h => h.Name != "Scan" && h.Name != "OnEnable" && h.Name != "OnDestroy" && h.Name != "TryApply" && h.Name != "RefreshGeneratedNavigation" && h.Name != "LoadCategories"),
        entry.Item1 + " owns no discovery or light lifecycle hooks");
    var harmony = new Harmony("compatibilitycheck." + entry.Item1);
    try
    {
        type.GetMethod("Apply").Invoke(instance, new object[] { harmony, entry.Item2, hooks });
        var patched = entry.Item1 == "BakeryPatch" ? new[] { hooks[0] } : new[] { hooks[0], hooks[1], hooks[2], hooks[4] };
        Check(patched.All(h => Harmony.GetPatchInfo(h)?.Owners.Contains(harmony.Id) == true), entry.Item1 + " intended hooks apply");
        if (entry.Item1 == "BakeryPatch")
        {
            Type itemType = hooks[0].GetParameters()[0].ParameterType;
            object item = InertEngineObject(itemType, 99);
            AccessTools.Field(itemType, "itemName").SetValue(item, "bakeryxx:itemname_bread");
            AccessTools.Field(itemType, "wholesalePrice").SetValue(item, 12f);
            AccessTools.Field(itemType, "defaultMarketPrice").SetValue(item, 45f);
            Toggle("BakeryVanillaMargins", false);
            Invoke(type, "RegisterPrefix", item);
            Check((float)AccessTools.Field(itemType, "wholesalePrice").GetValue(item) == 12f,
                "price option off preserves author prices without CP");
            Toggle("BakeryVanillaMargins", true);
            Invoke(type, "RegisterPrefix", item);
            Invoke(type, "RegisterPrefix", item);
            Check((float)AccessTools.Field(itemType, "wholesalePrice").GetValue(item) == 3f &&
                  (float)AccessTools.Field(itemType, "defaultMarketPrice").GetValue(item) == 7.5f,
                "price-only HMP applies intended margins without compounding or Help");
            type.GetMethod("Revert").Invoke(instance, null);
            Check((float)AccessTools.Field(itemType, "wholesalePrice").GetValue(item) == 12f &&
                  (float)AccessTools.Field(itemType, "defaultMarketPrice").GetValue(item) == 45f,
                "price-only revert restores the original author prices");
        }
        if (entry.Item1 == "HyperrealPatch")
        {
            // Exercise the actual Harmony postfix with the author's engine work stubbed out.
            // Only managed test objects and HDRP's reset flag are used; this cannot test GPU recovery.
            var probeHarmony = new Harmony("compatibilitycheck.map-probes");
            try
            {
                probeHarmony.Patch(hooks[0], prefix: new HarmonyMethod(typeof(Probes), nameof(Probes.SkipOriginal)));
                probeHarmony.Patch(hooks[2], prefix: new HarmonyMethod(typeof(Probes), nameof(Probes.SkipOriginal)));
                probeHarmony.Patch(hooks[3], prefix: new HarmonyMethod(typeof(Probes), nameof(Probes.HdCameraPrefix)));
                Type mapType = hyperreal.GetType("PolyModsHyperreal.MapMode", true);
                Type envType = hyperreal.GetType("PolyModsHyperreal.GameEnvironment", true);
                Type cfgType = hyperreal.GetType("PolyModsHyperreal.HRConfig", true);
                object map = RuntimeHelpers.GetUninitializedObject(mapType);
                object env = RuntimeHelpers.GetUninitializedObject(envType);
                object cfg = RuntimeHelpers.GetUninitializedObject(cfgType);
                object volume = RuntimeHelpers.GetUninitializedObject(hooks[0].DeclaringType);
                Type exposureType = hdrp.GetType("UnityEngine.Rendering.HighDefinition.Exposure", true);
                object exposure = InertEngineObject(exposureType, 11);
                FieldInfo mode = AccessTools.Field(exposureType, "mode");
                object modeParameter = RuntimeHelpers.GetUninitializedObject(mode.FieldType);
                FieldInfo value = AccessTools.Field(mode.FieldType, "m_Value");
                mode.SetValue(exposure, modeParameter);
                AccessTools.Field(volume.GetType(), "_exposure").SetValue(volume, exposure);
                object camera = InertEngineObject(hooks[2].GetParameters()[0].ParameterType, 12);
                Probes.HdCamera = RuntimeHelpers.GetUninitializedObject(hooks[3].DeclaringType);
                var reset = AccessTools.Field(hooks[3].DeclaringType, "resetPostProcessingHistory");
                var colorHistory = AccessTools.Field(hooks[3].DeclaringType, "colorPyramidHistoryIsValid");
                colorHistory.SetValue(Probes.HdCamera, true);
                FieldInfo closed = AccessTools.Field(game.GetType("GlobalEvents", true), "onCityMapClosed");
                void CloseCamera() => ((Action)closed.GetValue(null))();
                void Frame(bool open, bool engaged, float blend, bool on = true)
                {
                    AccessTools.Field(mapType, "<Open>k__BackingField").SetValue(map, open);
                    AccessTools.Field(mapType, "<Engaged>k__BackingField").SetValue(map, engaged);
                    AccessTools.Field(mapType, "<Blend>k__BackingField").SetValue(map, blend);
                    hooks[2].Invoke(volume, new object[] { camera, null });
                    hooks[0].Invoke(volume, new object[] { on, env, cfg, map });
                }
                void Mode(int selected) => value.SetValue(modeParameter, Enum.ToObject(value.FieldType, selected));
                bool WasReset() => (bool)reset.GetValue(Probes.HdCamera);
                void Arm() { reset.SetValue(Probes.HdCamera, false); Frame(true, true, 1f, false); }
                Mode(4);
                hooks[4].Invoke(null, null);
                hooks[4].Invoke(null, null);
                Check(((Delegate)closed.GetValue(null)).GetInvocationList().Count(d => d.Method.DeclaringType == type) == 1,
                    "game event initialization retains exactly one map-close handler across reloads");
                Arm();
                Frame(false, false, 0f);
                Check(!WasReset(), "actual postfix waits for camera completion even after zero blend");
                CloseCamera();
                Frame(false, false, 0.1f);
                Check(!WasReset(), "actual postfix waits for fade after camera completion");
                Frame(false, false, 0f);
                Check(WasReset() && ReferenceEquals(Probes.RequestedCamera, camera) && (bool)colorHistory.GetValue(Probes.HdCamera),
                    "automatic return sets only exposure/postprocessing reset flag on Hyperreal's supplied camera");
                reset.SetValue(Probes.HdCamera, false);
                Frame(false, false, 0f);
                Check(!WasReset(), "actual postfix resets only once per return");
                Mode(0); Arm(); CloseCamera(); Frame(false, false, 0f);
                Check(!WasReset(), "fixed exposure is unchanged");
                Mode(4); Frame(false, false, 0f);
                Check(!WasReset(), "ineligible fixed return does not leave a delayed reset armed");
                Arm(); CloseCamera(); AccessTools.Field(envType, "Indoors").SetValue(env, true); Frame(false, false, 0f);
                Check(!WasReset(), "indoor return is unchanged");
                AccessTools.Field(envType, "Indoors").SetValue(env, false);
                Arm(); CloseCamera(); Toggle("HyperrealMapReturnFix", false); Frame(false, false, 0f);
                Check(!WasReset(), "option off cancels pending exposure reset");
                Toggle("HyperrealMapReturnFix", true); Frame(false, false, 0f);
                Check(!WasReset(), "enabling does not reset unrelated gameplay");
                Arm(); CloseCamera(); hooks[1].Invoke(map, null); Frame(false, false, 0f);
                Check(!WasReset(), "author's Map.Reset cancels pending return");
                Arm(); CloseCamera(); hooks[4].Invoke(null, null); Frame(false, false, 0f);
                Check(!WasReset(), "game event initialization cancels old return state");
                foreach (string callback in new[] { "SceneLoaded", "SceneUnloaded" })
                {
                    Arm(); CloseCamera();
                    var method = AccessTools.Method(type, callback);
                    method.Invoke(null, method.GetParameters().Select(p => Activator.CreateInstance(p.ParameterType)).ToArray());
                    Frame(false, false, 0f);
                    Check(!WasReset(), callback + " cancels pending map return");
                }
                Arm(); CloseCamera(); Frame(false, false, 0f);
                Check(WasReset(), "map return still works after game event initialization");
            }
            finally { probeHarmony.UnpatchAll(probeHarmony.Id); }
        }
    }
    finally
    {
        harmony.UnpatchAll(harmony.Id);
        type.GetMethod("Revert").Invoke(instance, null);
        if (entry.Item1 == "HyperrealPatch")
        {
            var closed = AccessTools.Field(game.GetType("GlobalEvents", true), "onCityMapClosed").GetValue(null) as Delegate;
            Check(closed == null || !closed.GetInvocationList().Any(d => d.Method.DeclaringType == type), "revert removes map-close handler");
        }
    }
}
// The same original Bakery bytes without a readable file identity are deliberately unverified.
// Pricing remains available by its existing contracts; there are no optional discovery hooks.
{
    Type type = patch.GetType("HardModePatch.BakeryPatch", true);
    object instance = Activator.CreateInstance(type);
    var unverified = Assembly.Load(File.ReadAllBytes(bakery.Location));
    var hooks = (IList<MethodBase>)type.GetMethod("ResolveHooks").Invoke(instance, new object[] { unverified });
    var contracts = (IList)type.GetMethod("Contracts").Invoke(instance, new object[] { unverified, hooks });
    Check(hooks.Count == 2 && hooks.All(h => h != null) && contracts.Count == 1,
        "unverified Bakery retains its independent price contracts");
}
{
    Type type = patch.GetType("HardModePatch.HyperrealPatch", true);
    var unverified = Assembly.Load(File.ReadAllBytes(hyperreal.Location));
    var hooks = (IList<MethodBase>)type.GetMethod("ResolveHooks").Invoke(Activator.CreateInstance(type), new object[] { unverified });
    Check(hooks[0] == null,
        "unreadable Hyperreal identity rejects map workaround before installation");
}
Console.WriteLine($"ALL {checks} COMPATIBILITY CHECKS PASSED (offline; live lighting still requires a playtest)");
return 0;

public static class Probes
{
    public static object HdCamera, RequestedCamera;
    public static bool SkipOriginal() => false;
    public static bool HdCameraPrefix(object camera, ref object __result)
    {
        RequestedCamera = camera;
        __result = HdCamera;
        return false;
    }
}
