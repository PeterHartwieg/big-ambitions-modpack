using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Snapshot the RLO boundary without executing RLO or Unity. Run each DLL in a separate process, then diff JSON.
// Includes indirect save/school/billing consumers, so a matching hook alone is never a compatibility review.
if (args.Length != 4) { Console.Error.WriteLine("RloAudit <Managed> <HARTModPack.dll> <RLO.dll> <output.json>"); return 2; }
AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    if (new[] { "mscorlib", "System", "System.Core", "netstandard" }.Contains(name.Name)) return null;
    string path = Path.Combine(Path.GetFullPath(args[0]), name.Name + ".dll");
    return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
};
var pack = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
var rlo = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2]));
var fingerprint = pack.GetType("HardModePatch.IlText", true)!.GetMethod("Fingerprint")!;
var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
void Snapshot(string shortName, params string[] names)
{
    var type = rlo.GetType("RealLifeOverhaul." + shortName, true)!;
    rows[type.FullName!] = "type " + type.Attributes + " : " + type.BaseType;
    var members = type.GetMembers(Any).Where(m => names.Length == 0 || names.Contains(m.Name)).ToList();
    foreach (var name in names) if (!members.Any(m => m.Name == name)) throw new Exception("Missing " + shortName + "." + name);
    foreach (var member in members)
    {
        string key = type.FullName + "::" + member;
        if (member is FieldInfo f)
            rows[key] = f.Attributes + (f.IsLiteral ? " = " + Convert.ToString(f.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture) : "");
        if (member is MethodBase m)
            rows[key] = m.Attributes + " | parameters " + string.Join(",", m.GetParameters().Select(p => p.Name))
                + " | IL " + (m.GetMethodBody() == null ? "none" : fingerprint.Invoke(null, new object[] { m is MethodInfo generic && generic.IsGenericMethodDefinition ? generic.MakeGenericMethod(generic.GetGenericArguments().Select(_ => typeof(object)).ToArray()) : m }));
        if (member is PropertyInfo p) rows[key] = p.Attributes.ToString();
    }
}
Snapshot("Settings", "Careers", "CareersStudy", "Utilities", "AmbientNight", "AmbientNightId", "AmbientSound", "Leisure");
foreach (var type in new[] {
    "Careers.StudyCatalogue", "Careers.CourseDef", "Careers.CourseKind", "Careers.Campus", "Careers.Skills",
    "Careers.StudyFile", "Careers.StudyRules", "Careers.CampusRules", "Careers.CareersStore", "Careers.Enrolment",
    "Careers.ClassPlayRules", "Utilities.UtilitiesRules", "Utilities.InternetRules",
    "Utilities.Account", "Utilities.PlaceInfo", "Utilities.InternetCatalogue", "Utilities.UtilitiesGame",
    "Utilities.Service", "Utilities.PlaceKind", "Utilities.SupplyState", "Tech.TechWriter", "Tech.TechReader", "Tech.TechLines", "Tech.Fields",
    "Closure", "ClosureSource", "Leisure.LeisureSound" }) Snapshot(type);
Snapshot("Tech.TalentCatalogue", "Diploma", "Visual", "Troubleshooting");
Snapshot("Careers.CareersDirector", "Offered", "get_Instance", "get_Study", "_broken", "AtTheSchool", "CanAttend", "CanSit", "DeskOfferOf", "DeskOfferNow", "ToSchoolDesk");
Snapshot("Utilities.UtilitiesDirector", "NeedsNet", "get_Instance", "_save", "_loaded", "_failed", "_toldClosed", "TellClosed", "Day");
Snapshot("Prices", "get_Level");
Snapshot("BusinessState", "Closures", "PlaceKey");
Snapshot("BusinessDirector", "CloseFor");
Snapshot("CareerCatalogue", "OwnerFactor");
Snapshot("TravelState", "get_HasRoom");
var result = new { version = rlo.GetName().Version!.ToString(), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[2]))).ToLowerInvariant(), members = rows };
File.WriteAllText(args[3], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"Audited {rows.Count} type/member signatures, constants and normalized IL bodies: {result.version} {result.sha256}");
return 0;
