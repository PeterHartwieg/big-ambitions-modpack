using System.Collections;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;

// The series settings against the real mods: every option id the button writes is a string in its owner's DLL, the
// button finds Dynamic Rivals', Rival Balance's and our own options on the pages those mods really register, and the
// custom game prefill resolves and applies on the game's own screen. No game launched, PlayerPrefs written or save read.
if (args.Length != 6)
{
    Console.Error.WriteLine("Usage: SettingsCheck <game Managed folder> <HARTModPack.dll> <RealLifeOverhaul.dll> <DynamicRivals.dll> <HARTRivalBalance.dll> <export-series-settings.ps1>");
    return 2;
}
string managed = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    if (new[] { "mscorlib", "System", "System.Core", "netstandard" }.Contains(name.Name)) return null;
    string path = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
int checks = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL " + name);
    checks++; Console.WriteLine("PASS " + name);
}
var patch = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
var game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "BigAmbitions.dll"));
var modApi = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "BigAmbitions.ModAPI.dll"));
var dr = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[3]));
var rb = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[4]));
Type P(string type) => patch.GetType("HardModePatch." + type, true)!;
var table = (Array)P("Core.SeriesSettings").GetField("Options")!.GetValue(null)!;
object F(object o, string f) => o.GetType().GetField(f)!.GetValue(o)!;
var rows = table.Cast<object>().Select(o => (Owner: (string)F(o, "Owner"), Id: (string)F(o, "Id"), Value: (int)F(o, "Value"), Kind: F(o, "Kind").ToString())).ToList();

// 1. Every id is a string literal in its owner's DLL (RLO's ids are constants, inlined where they are registered).
HashSet<string> Strings(string path)
{
    using var pe = new PEReader(File.OpenRead(path));
    var md = pe.GetMetadataReader();
    var all = new HashSet<string>();
    var handle = MetadataTokens.UserStringHandle(1);
    while (!handle.IsNil) { all.Add(md.GetUserString(handle)); handle = MetadataReaderExtensions.GetNextHandle(md, handle); }
    return all;
}
var owners = new Dictionary<string, string> { { "RealLifeOverhaul", args[2] }, { "DynamicRivals", args[3] }, { "HARTRivalBalance", args[4] } };
foreach (var owner in owners)
{
    var strings = Strings(Path.GetFullPath(owner.Value));
    var missing = rows.Where(r => r.Owner == owner.Key && !strings.Contains(r.Id)).Select(r => r.Id).ToList();
    Check(missing.Count == 0, owner.Key + ": all " + rows.Count(r => r.Owner == owner.Key) + " option ids are in its DLL" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing) + ")" : ""));
}

// 2. The real option pages of Dynamic Rivals, Rival Balance and HMP, then what the button would write.
var service = modApi.GetType("BigAmbitions.Mods.OptionsService", true)!;
var entries = (IDictionary)service.GetField("Entries", Any)!.GetValue(null)!;
dr.GetType("DynamicRivals.DynamicRivalsMod", true)!.GetMethod("RegisterOptions", Any)!.Invoke(null, null);
rb.GetType("Hart.RivalBalance.PresetOptions", true)!.GetMethod("Register", Any)!.Invoke(null, new object[] { "C:/ModsLocal/HARTRivalBalance" });
var options = P("Options");
var pageType = modApi.GetType("BigAmbitions.Mods.ModOptions", true)!;
var ownPage = Activator.CreateInstance(pageType)!;
options.GetMethod("AppendTo", Any)!.Invoke(null, new[] { ownPage });
service.GetMethod("Register", Any)!.Invoke(null, new[] { (object)"3830000000", ownPage });
Check(entries.Contains("Dynamic Rivals") && entries.Contains("C:/ModsLocal/HARTRivalBalance") && entries.Contains("3830000000"), "the three mods register their real pages");
var preview = ((IEnumerable<string>)P("SeriesSettingsButton").GetMethod("Preview")!.Invoke(null, null)!).ToList();
foreach (var line in preview) Console.WriteLine("  " + line);
string Line(string owner, string id) => preview[rows.FindIndex(r => r.Owner == owner && r.Id == id)];
Check(Line("HARTRivalBalance", "rival_preset") == "HART. Thriving Rivals rival_preset -> m:C:/ModsLocal/HARTRivalBalance:rival_preset = 1", "Rival Balance: rival_preset found by owner and id, written to Normal");
foreach (var r in rows.Where(r => r.Owner == "DynamicRivals"))
    Check(Line(r.Owner, r.Id) == "Dynamic Rivals " + r.Id + " -> m:Dynamic Rivals:" + r.Id + " = " + r.Value, "Dynamic Rivals: " + r.Id + " = " + r.Value);
foreach (var r in rows.Where(r => r.Owner == "HARTModPack"))
    Check(Line(r.Owner, r.Id) == "HART. Mod Pack " + r.Id + " -> m:3830000000:" + r.Id + " = " + r.Value, "HMP: " + r.Id + " = " + r.Value);
Check(rows.Where(r => r.Owner == "RealLifeOverhaul").All(r => Line(r.Owner, r.Id).EndsWith("skipped, Real Life Overhaul isn't loaded or has no options")), "RLO not registered here: every RLO value is skipped and named, nothing guessed");
var drPage = entries["Dynamic Rivals"]!;
var drOptions = (IEnumerable)drPage.GetType().GetProperty("Options")!.GetValue(drPage)!;
var wholesale = drOptions.Cast<object>().First(o => (string)o.GetType().GetProperty("Id")!.GetValue(o)! == "wholesale_rival_count");
var choices = (string[])wholesale.GetType().GetProperty("ChoiceKeys")!.GetValue(wholesale)!;
var occupancy = drOptions.Cast<object>().First(o => (string)o.GetType().GetProperty("Id")!.GetValue(o)! == "retail_occupancy");
var frequency = drOptions.Cast<object>().First(o => (string)o.GetType().GetProperty("Id")!.GetValue(o)! == "event_frequency");
Check(choices[0] == "7" && choices[1] == "8" && ((string[])occupancy.GetType().GetProperty("ChoiceKeys")!.GetValue(occupancy)!)[0] == "Vanilla"
    && ((string[])frequency.GetType().GetProperty("ChoiceKeys")!.GetValue(frequency)!)[2] == "Balanced", "Dynamic Rivals' choice lists: index 0 is 7, 1 is 8, occupancy 0 Vanilla, frequency 2 Balanced");

// 3. Custom game prefill on the game's own screen type.
var prefill = P("CustomGamePrefill");
var hooks = (IList<MethodBase>)prefill.GetMethod("ResolveHooks")!.Invoke(null, null)!;
Check(hooks.Count == 3 && hooks.All(h => h != null), "CustomGameOptionsHandler.Start, SetPreset, every GameVariables field and CustomGamePanel.Start with its handler and start button resolve");
var harmony = new Harmony("settingscheck");
harmony.Patch(hooks[0], postfix: new HarmonyMethod(prefill.GetMethod("StartPostfix", Any)));
harmony.Patch(hooks[2], postfix: new HarmonyMethod(prefill.GetMethod("PanelStartPostfix", Any)));
Check(Harmony.GetPatchInfo(hooks[0])!.Postfixes.Count == 1 && Harmony.GetPatchInfo(hooks[2])!.Postfixes.Count == 1, "both Start postfixes apply, so either Start order ends on the series values");
harmony.UnpatchAll(harmony.Id);
var variables = game.GetType("GameVariables", true)!;
var current = Activator.CreateInstance(variables)!;
variables.GetField("startingAge")!.SetValue(current, 30);
var preset = prefill.GetMethod("SeriesPreset", Any)!.Invoke(null, new[] { current })!;
object V(string f) => variables.GetField(f)!.GetValue(preset)!;
Check(!ReferenceEquals(preset, current) && (int)V("daysPerYear") == 60 && (int)V("startingMoney") == 0 && (float)V("rivalsDifficultyMultiplier") == 1.5f
    && (int)V("startingAge") == 18 && (int)V("taxPercentage") == 51 && (float)V("marketPriceMultiplier") == 1.3f && (float)V("baseCustomerPromotionMultiplier") == 0.5f
    && (float)V("wholesaleUrgentFeeMultiplier") == 2f && (float)V("exportMultiplier") == 0.1f && (float)V("sellingMultiplier") == 0.5f && !(bool)V("tutorialEnabled"),
    "the series preset carries every series value onto a copy of the game's preset");
Check((int)variables.GetField("startingAge")!.GetValue(current)! == 30, "the game's own preset object is left unchanged");

// 4. The Windows export script lists exactly the button's values.
var script = File.ReadAllText(Path.GetFullPath(args[5]));
var listed = System.Text.RegularExpressions.Regex.Matches(script, "@\\{ Owner = '([^']+)'; Id = '([^']+)'; Value = (\\d+) \\}")
    .Select(m => (m.Groups[1].Value, m.Groups[2].Value, int.Parse(m.Groups[3].Value))).ToList();
Check(listed.SequenceEqual(rows.Select(r => (r.Owner, r.Id, r.Value))), "export-series-settings.ps1 lists the same " + rows.Count + " options and values as Core/SeriesSettings");
Console.WriteLine($"ALL {checks} SETTINGS CHECKS PASSED (offline; RLO's live options page, the in-game button, PlayerPrefs and the custom game screen remain unverified)");
return 0;
