using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

// Loads build/HARTModPack/HARTModPack.dll next to the given game assemblies and resolves every type and member
// reference in it to a game assembly. A reference the game no longer has fails here instead of in the game.
// Exit code 1 on any failure.
string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
string modDll = Path.Combine(repo, "build/HARTModPack/HARTModPack.dll");
if (args.Length != 1 || !File.Exists(Path.Combine(args[0], "BigAmbitions.dll"))) { Console.Error.WriteLine("Usage: ApiCheck <folder with BigAmbitions.dll>"); return 2; }
string game = Path.GetFullPath(args[0]);
// The runtime's own core library stands in for the game's Mono mscorlib.
var skip = new HashSet<string> { "mscorlib", "System", "System.Core", "netstandard" };
var context = new AssemblyLoadContext("ApiCheck");
context.Resolving += (ctx, name) =>
{
    if (skip.Contains(name.Name)) return null;
    string path = name.Name == "0Harmony" ? Path.Combine(repo, "build/HARTModPack/Dependencies/0Harmony.dll") : Path.Combine(game, name.Name + ".dll");
    return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
};
var mod = context.LoadFromAssemblyPath(modDll);
var module = mod.ManifestModule;

using var stream = File.OpenRead(modDll);
using var pe = new PEReader(stream);
var md = pe.GetMetadataReader();
var gameAssemblies = new HashSet<string>(md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name)).Where(n => !skip.Contains(n)));

string Scope(EntityHandle parent)
{
    while (true)
    {
        if (parent.Kind != HandleKind.TypeReference) return null;
        var tr = md.GetTypeReference((TypeReferenceHandle)parent);
        if (tr.ResolutionScope.Kind == HandleKind.AssemblyReference) return md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name);
        parent = tr.ResolutionScope;
    }
}
string TypeName(EntityHandle h)
{
    var tr = md.GetTypeReference((TypeReferenceHandle)h);
    return md.GetString(tr.Namespace) + "." + md.GetString(tr.Name);
}

int failures = 0, checkedCount = 0;
foreach (var h in md.TypeReferences)
{
    string scope = Scope(h);
    if (scope == null || !gameAssemblies.Contains(scope)) continue;
    checkedCount++;
    try { var t = module.ResolveType(MetadataTokens.GetToken(h)); Console.WriteLine($"ok   type {scope}: {t.FullName}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL type {scope}: {TypeName(h)}: {ex.GetType().Name} {ex.Message}"); }
}
foreach (var h in md.MemberReferences)
{
    var mr = md.GetMemberReference(h);
    string scope = Scope(mr.Parent);
    if (scope == null || !gameAssemblies.Contains(scope)) continue;
    checkedCount++;
    string label = TypeName(mr.Parent) + "::" + md.GetString(mr.Name);
    try { var m = module.ResolveMember(MetadataTokens.GetToken(h)); Console.WriteLine($"ok   member {scope}: {label} ({m.MemberType})"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL member {scope}: {label}: {ex.GetType().Name} {ex.Message}"); }
}
// Members the mod reads by reflection, because Steam's types live in a platform-specific assembly
// (Facepunch.Steamworks.Win64 or .Posix): see src/WorkshopMembers.cs.
checkedCount++;
try
{
    var helper = context.Assemblies.First(a => a.GetName().Name == "HGPlugins").GetType("Helpers.SteamHelper", true);
    var query = helper.GetMethod("GetUserSubscribedItems", BindingFlags.Public | BindingFlags.Static) ?? throw new MissingMethodException("GetUserSubscribedItems");
    var p = query.GetParameters();
    if (p.Length != 4 || p[0].ParameterType != typeof(int) || p[1].ParameterType != typeof(string) || p[2].ParameterType != typeof(string) || !p[3].ParameterType.IsValueType)
        throw new MissingMethodException("GetUserSubscribedItems(int, string, string, SteamId) has another signature: " + query);
    var list = query.ReturnType.GetGenericArguments().Single();          // List<Item>
    var item = list.GetGenericArguments().Single();                      // Steamworks.Ugc.Item
    var id = item.GetProperty("Id") ?? throw new MissingMemberException(item.FullName, "Id");
    var directory = item.GetProperty("Directory") ?? throw new MissingMemberException(item.FullName, "Directory");
    var value = id.PropertyType.GetField("Value") ?? throw new MissingMemberException(id.PropertyType.FullName, "Value");
    if (directory.PropertyType != typeof(string) || value.FieldType != typeof(ulong)) throw new MissingMemberException("Item.Directory or PublishedFileId.Value changed type");
    Console.WriteLine($"ok   reflection: {helper.FullName}.GetUserSubscribedItems -> {item.Assembly.GetName().Name}: {item.FullName}.Id.Value, .Directory");
}
catch (Exception ex) { failures++; Console.WriteLine($"FAIL reflection: {ex.GetType().Name} {ex.Message}"); }
checkedCount++;
try
{
    var itemAssembly = context.LoadFromAssemblyPath(Path.Combine(game, "BigAmbitions.Items.dll"));
    var instance = itemAssembly.GetType("BigAmbitions.Items.ItemInstance", true)!;
    var field = instance.GetField("_itemCached", BindingFlags.NonPublic | BindingFlags.Instance);
    if (field == null || field.FieldType.FullName != "BigAmbitions.Items.Item") throw new MissingFieldException("ItemInstance._itemCached");
    Console.WriteLine("ok   reflection: nonmutating ItemInstance._itemCached read");
}
catch (Exception ex) { failures++; Console.WriteLine($"FAIL reflection: {ex.GetType().Name} {ex.Message}"); }
Console.WriteLine($"{checkedCount - failures}/{checkedCount} game references resolve against {game}");
return failures == 0 ? 0 : 1;
