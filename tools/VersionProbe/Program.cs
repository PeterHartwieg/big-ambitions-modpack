// Usage: dotnet run --project tools/VersionProbe -- <ModsLocal or Workshop content folder> [...]
// Prints one line per mod folder: folder, assembly simple name, AssemblyVersion, AssemblyInformationalVersion.
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

foreach (var root in args)
{
    foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
    {
        var dlls = Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly);
        if (dlls.Length != 1) { Console.WriteLine($"{Path.GetFileName(dir)}\t(no single root DLL: {dlls.Length})"); continue; }
        using var stream = File.OpenRead(dlls[0]);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var def = md.GetAssemblyDefinition();
        string name = md.GetString(def.Name);
        string info = "";
        foreach (var handle in def.GetCustomAttributes())
        {
            var attr = md.GetCustomAttribute(handle);
            if (attr.Constructor.Kind != HandleKind.MemberReference) continue;
            var ctor = md.GetMemberReference((MemberReferenceHandle)attr.Constructor);
            if (ctor.Parent.Kind != HandleKind.TypeReference) continue;
            var type = md.GetTypeReference((TypeReferenceHandle)ctor.Parent);
            if (md.GetString(type.Name) != "AssemblyInformationalVersionAttribute") continue;
            var blob = md.GetBlobReader(attr.Value);
            blob.ReadUInt16();
            info = blob.ReadSerializedString() ?? "";
        }
        Console.WriteLine($"{Path.GetFileName(dir)}\t{name}\t{def.Version}\t{info}");
    }
}
