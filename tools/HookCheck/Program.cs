using System.Reflection;
using System.Runtime.Loader;

// Loads build/HARTModPack/HARTModPack.dll, the game's assemblies from build/game-dlls and the target
// mods' DLLs from the folder given (searched recursively), then asks each patch to resolve its hooks.
// Exit code 1 if any target present in the folder has a missing hook.
string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
string gameDlls = Environment.GetEnvironmentVariable("BA_GAME_DLLS") ?? Path.GetFullPath(Path.Combine(repo, "../big-ambitions-hard-mode-patch/build/game-dlls"));
string patchDll = Path.Combine(repo, "build/HARTModPack/HARTModPack.dll");
if (args.Length != 1 || !Directory.Exists(args[0])) { Console.Error.WriteLine("Usage: HookCheck <folder with target mod DLLs>"); return 2; }
var targetDlls = Directory.GetFiles(Path.GetFullPath(args[0]), "*.dll", SearchOption.AllDirectories)
    .GroupBy(Path.GetFileNameWithoutExtension).ToDictionary(g => g.Key, g => g.First());
// The game's own runtime assemblies stay out: .NET maps mscorlib references to its own core library.
var skip = new HashSet<string> { "mscorlib", "System", "System.Core", "netstandard" };

AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    if (skip.Contains(name.Name)) return null;
    string game = Path.Combine(gameDlls, name.Name + ".dll");
    if (File.Exists(game)) return context.LoadFromAssemblyPath(game);
    if (targetDlls.TryGetValue(name.Name, out var target)) return context.LoadFromAssemblyPath(target);
    return null;
};

var patch = AssemblyLoadContext.Default.LoadFromAssemblyPath(patchDll);
var patchInterface = patch.GetType("HardModePatch.ITargetPatch", true);
int failures = 0;
foreach (var type in patch.GetTypes().Where(t => patchInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).OrderBy(t => t.Name))
{
    var instance = Activator.CreateInstance(type);
    var info = type.GetProperty("Target").GetValue(instance);
    string assemblyName = (string)info.GetType().GetField("AssemblyName").GetValue(info);
    if (!targetDlls.TryGetValue(assemblyName, out var path)) { Console.WriteLine($"{type.Name}: {assemblyName}.dll not in the folder, skipped"); continue; }
    var target = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    System.Collections.IList hooks;
    try { hooks = (System.Collections.IList)type.GetMethod("ResolveHooks").Invoke(instance, new object[] { target }); }
    catch (TargetInvocationException ex) { Console.WriteLine($"{type.Name}: FAILED {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"); failures++; continue; }
    var missing = Enumerable.Range(0, hooks.Count).Where(i => hooks[i] == null).ToList();
    Console.WriteLine($"{type.Name}: {hooks.Count - missing.Count}/{hooks.Count} hooks" + (missing.Count > 0 ? " MISSING " + string.Join(",", missing) : ""));
    foreach (MethodBase hook in hooks) if (hook != null) Console.WriteLine($"    {hook.DeclaringType.FullName}.{hook.Name}({string.Join(", ", hook.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    if (missing.Count > 0) { failures++; continue; }
    // The bodies the patch relies on: the fingerprint read here must equal the reviewed one in the source.
    var ilText = patch.GetType("HardModePatch.IlText", true).GetMethod("Fingerprint");
    foreach (var check in (System.Collections.IEnumerable)type.GetMethod("Contracts").Invoke(instance, new object[] { target, hooks }))
    {
        var method = (MethodBase)check.GetType().GetField("Method").GetValue(check);
        var contract = check.GetType().GetField("Contract").GetValue(check);
        string name = (string)contract.GetType().GetField("Name").GetValue(contract);
        string expected = (string)contract.GetType().GetField("Expected").GetValue(contract);
        string actual = method == null ? "missing" : (string)ilText.Invoke(null, new object[] { method });
        Console.WriteLine($"    contract {name}: {actual}" + (actual == expected ? " ok" : $" EXPECTED {expected}"));
        if (actual != expected) failures++;
    }
    // Applying checks what only Harmony checks: injected parameter names, return types and transpiler output.
    var harmony = new HarmonyLib.Harmony("hookcheck." + type.Name);
    try
    {
        type.GetMethod("Apply").Invoke(instance, new object[] { harmony, target, hooks });
        Console.WriteLine("    applied");
    }
    catch (TargetInvocationException ex) { Console.WriteLine($"    APPLY FAILED {ex.InnerException}"); failures++; }
    finally { harmony.UnpatchAll(harmony.Id); }
}
return failures == 0 ? 0 : 1;
