using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: FactoryCheck <game Managed folder> <HARTModPack.dll> <RealLifeOverhaul.dll>");
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
var rlo = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2]));
Type bridge = patch.GetType("HardModePatch.RealLifeOverhaulPatch", true);
Type T(string type) => rlo.GetType("RealLifeOverhaul.Careers." + type, true);
const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
object Call(string name, params object[] values) => bridge.GetMethod(name, Any).Invoke(null, values);
object Get(object o, string field) => o.GetType().GetField(field, Any).GetValue(o);
void Set(object o, string field, object value)
{
    var member = o.GetType().GetField(field, Any);
    var setter = new DynamicMethod("FixtureInstanceField", typeof(void), new[] { typeof(object), typeof(object) }, typeof(Program).Module, true);
    var il = setter.GetILGenerator();
    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, member.DeclaringType);
    il.Emit(OpCodes.Ldarg_1);
    il.Emit(member.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, member.FieldType);
    il.Emit(OpCodes.Stfld, member); il.Emit(OpCodes.Ret);
    ((Action<object, object>)setter.CreateDelegate(typeof(Action<object, object>)))(o, value);
}
object Rlo(string type, string method, params object[] values) => T(type).GetMethod(method, Any).Invoke(null, values);
int checks = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL " + name);
    checks++; Console.WriteLine("PASS " + name);
}
void WriteStatic(FieldInfo field, object value)
{
    var setter = new DynamicMethod("FixtureField", typeof(void), new[] { typeof(object) }, typeof(Program).Module, true);
    var il = setter.GetILGenerator();
    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, field.FieldType); il.Emit(OpCodes.Stsfld, field); il.Emit(OpCodes.Ret);
    ((Action<object>)setter.CreateDelegate(typeof(Action<object>)))(value);
}
void Observe(string expected)
{
    var failed = bridge.GetField("failed", Any).GetValue(null);
    var warnings = ((IEnumerable)bridge.GetField("warnings", Any).GetValue(null)).Cast<object>().ToArray();
    Check((string)Call("ObserveGate") == expected && (string)Call("ObserveGate") == expected
        && Equals(failed, bridge.GetField("failed", Any).GetValue(null))
        && warnings.SequenceEqual(((IEnumerable)bridge.GetField("warnings", Any).GetValue(null)).Cast<object>()),
        "pure companion gate: " + expected);
}
var rule = patch.GetType("HardModePatch.Core.Rules").GetField("FactoryDiplomas").GetValue(null);
void Option(bool on) => rule.GetType().GetMethod("Set").Invoke(rule, new object[] { on });
var target = Activator.CreateInstance(bridge);
var hooks = (IList<MethodBase>)bridge.GetMethod("ResolveHooks").Invoke(target, new object[] { rlo });
Check(hooks.Count == 10 && hooks.All(h => h != null), "all game and RLO members resolve exactly");
Check(!(bool)Get(rule, "Default"), "new factory rule defaults off");
foreach (var contract in (IEnumerable)bridge.GetMethod("Contracts").Invoke(target, new object[] { rlo, hooks }))
{
    var method = (MethodBase)Get(contract, "Method"); var spec = Get(contract, "Contract");
    string hash = (string)patch.GetType("HardModePatch.IlText").GetMethod("Fingerprint").Invoke(null, new object[] { method });
    Check(hash == (string)Get(spec, "Expected"), "reviewed game purchase contract " + Get(spec, "Name"));
}
var catalogue = T("StudyCatalogue").GetField("All");
var original = (Array)catalogue.GetValue(null);
var originalIds = original.Cast<object>().Select(c => (int)Get(c, "Id")).ToArray();
Check(!originalIds.Intersect(new[] { 18, 19, 34 }).Any(), "released RLO catalogue leaves ids 18, 19, 34 free");
Check(new[] { 18, 19, 34, 35, 36, 37, 38, 39 }.All(id => !originalIds.Contains(id)), "all candidate spare ids verified against released binary");
var harmony = new Harmony("factorycheck");
bridge.GetMethod("Apply").Invoke(target, new object[] { harmony, rlo, hooks });
try
{
    Check(hooks.Take(3).All(h => Harmony.GetPatchInfo(h).Owners.Contains(harmony.Id)), "pickup, checkout and Campus offering hooks apply");
    var extended = (Array)catalogue.GetValue(null);
    Check(extended.Length == original.Length + 3 && !ReferenceEquals(original, extended), "readonly catalogue replaced atomically before careers start");
    Check(original.Cast<object>().Select(c => (int)Get(c, "Id")).SequenceEqual(originalIds), "original courses and order unchanged");
    var defs = new[] { 18, 19, 34 }.Select(id => Rlo("StudyCatalogue", "Find", id)).ToArray();
    int skillCount = (int)T("Skills").GetField("Count").GetRawConstantValue();
    foreach (var c in defs)
    {
        Check(c != null && Get(c, "Kind").ToString() == "Vocational" && Get(c, "Campus").ToString() == "School"
            && (int)Get(c, "Sessions") == 8 && (int)Get(c, "Homework") == 4 && (int)Get(c, "Hours") == 4
            && (int)Get(c, "School") == 2 && (int)Get(c, "Cert") == -1 && !(bool)Get(c, "Distance")
            && (int)Get(c, "Pass") == 50 && !((int[])Get(c, "Needs")).Any(), "native school diploma shape: " + Get(c, "Key"));
        Check((int)Get(c, "SkillA") < skillCount && (int)Get(c, "SkillB") < skillCount
            && (int)Get(c, "Fee") >= 3500 && (int)Get(c, "Fee") <= 4000, "existing skill ids and fee: " + Get(c, "Key"));
        Check(!(bool)Rlo("CareersDirector", "Offered", c), "option off hides new enrolments: " + Get(c, "Key"));
    }
    Option(true);
    Check(defs.All(c => (bool)Rlo("CareersDirector", "Offered", c)), "option on offers all three diplomas through RLO");
    Check(!(bool)Call("Ready", new object[] { null }), "missing director/study fails open");
    var file = Activator.CreateInstance(T("StudyFile"));
    object Enrol(int id)
    {
        var refusal = Rlo("StudyRules", "CanEnrol", file, Rlo("StudyCatalogue", "Find", id), Enum.ToObject(T("StudyMode"), 0));
        Check(Convert.ToInt32(refusal) == 0, "RLO permits native enrolment for id " + id);
        var e = Activator.CreateInstance(T("Enrolment"));
        Set(e, "Course", id); Set(e, "Start", 1); Set(e, "Fee", (int)Get(Rlo("StudyCatalogue", "Find", id), "Fee"));
        ((IList)Get(file, "Courses")).Add(e);
        return e;
    }
    var first = Enrol(18); var second = Enrol(19); var third = Enrol(34);
    foreach (var enrolment in new[] { first, second, third })
        Check((bool)Rlo("ClassPlayRules", "AtSchool", enrolment, false) && (bool)Rlo("ClassPlayRules", "AtSchool", enrolment, true),
            "native class and exam still route to school for diploma " + Get(enrolment, "Course"));
    var atDesk = T("ClassPlayRules").GetMethod("AtDesk", Any);
    if (atDesk != null) // Added in 0.50.0; the earlier reviewed release keeps its original school path.
    {
        object Desk(string cls, string exam, string busy) => atDesk.Invoke(null, new object[] { cls, exam, false, busy, false });
        var cls = Desk(null, "reallife_study_why_no_exam", null);
        var deskExam = Desk("reallife_study_why_no_class", null, null);
        Check((bool)Get(cls, "Can") && Get(cls, "Kind").ToString() == "Class", "0.50.0 school decision permits an available class");
        Check((bool)Get(deskExam, "Can") && Get(deskExam, "Kind").ToString() == "Exam", "0.50.0 school decision permits an available exam");
        Check(!(bool)Get(Desk(null, null, "reallife_study_why_busy"), "Can"), "0.50.0 school decision preserves busy-seat refusal");
    }
    for (int day = 1; day <= 21; day++)
    {
        var c = Rlo("StudyCatalogue", "Find", 18);
        if ((bool)Rlo("StudyRules", "On", Get(c, "Days"), day)) Rlo("StudyRules", "Attend", first, day);
        if ((int)Get(first, "Done") == 8)
        {
            Set(first, "Homework", 4);
            Rlo("StudyRules", "Advance", first, day);
            break;
        }
    }
    Check((int)Get(first, "Stage") == 1 && (int)Get(first, "Done") == 8 && (int)Get(first, "ExamFrom") <= 22, "RLO class accounting for eight sittings leads to exam in about three weeks");
    int examDay = (int)Get(first, "ExamFrom");
    while (!(bool)Rlo("StudyRules", "ExamDay", first, examDay)) examDay++;
    int score = (int)Rlo("StudyRules", "ExamScore", first, 35, false, 0);
    Rlo("StudyRules", "Sit", first, score, examDay);
    file.GetType().GetMethod("Finish").Invoke(file, new[] { first });
    Check((bool)file.GetType().GetMethod("Holds").Invoke(file, new object[] { 18 }), "native exam result grants matching diploma");
    Check(!(bool)file.GetType().GetMethod("Holds").Invoke(file, new object[] { 19 }), "unfinished diploma does not qualify");

    var ordinary = Activator.CreateInstance(T("Enrolment"));
    Set(ordinary, "Course", 10); Set(ordinary, "Stage", 2); Set(ordinary, "Score", 75); Set(ordinary, "Grade", 2);
    ((IList)Get(file, "Results")).Add(ordinary);
    Set(file, "LoanCents", 123456L);
    ((int[])Get(file, "Skill"))[16] = 12000;
    string text = (string)Rlo("CareersStore", "Write", file);
    object Read(string saved)
    {
        object[] a = { saved, 0 };
        return T("CareersStore").GetMethod("Read").Invoke(null, a);
    }
    var restored = Read(text);
    Check(((IList)Get(restored, "Courses")).Count == 2 && ((IList)Get(restored, "Results")).Count == 2
        && (bool)restored.GetType().GetMethod("Holds").Invoke(restored, new object[] { 18 }), "enrolled and completed diplomas survive RLO codec roundtrip");
    Check((int)Get(((IList)Get(restored, "Courses"))[0], "Fee") == (int)Get(second, "Fee"), "course progress and fees survive save/load");
    long mask = (long)Rlo("StudyRules", "Passed", restored);
    Check((mask & (1L << 18)) != 0 && (mask & (1L << 19)) == 0, "native 64-bit qualification mask includes new pass only");
    var highFile = Activator.CreateInstance(T("StudyFile"));
    var highResult = Activator.CreateInstance(T("Enrolment"));
    Set(highResult, "Course", 34); Set(highResult, "Stage", 2);
    ((IList)Get(highFile, "Results")).Add(highResult);
    var highRestored = Read((string)Rlo("CareersStore", "Write", highFile));
    Check(((long)Rlo("StudyRules", "Passed", highRestored) & (1L << 34)) != 0
        && (bool)highRestored.GetType().GetMethod("Holds").Invoke(highRestored, new object[] { 34 }), "id 34 survives save/load and the high half of the native mask");
    Option(false);
    Check(((IList)Get(Read(text), "Courses")).Count == 2 && ((IList)Get(Read(text), "Results")).Count == 2, "option off retains enrolled and completed records");
    Option(true);
    var director = RuntimeHelpers.GetUninitializedObject(T("CareersDirector"));
    Set(director, "_study", restored); Set(director, "_studyOn", true);
    var singleton = T("CareersDirector").GetField("<Instance>k__BackingField", Any);
    singleton.SetValue(null, director);
    Observe("active");
    object Denied(params string[] items) => Call("Denied", (object)items);
    Check(Denied("ba:itemname_industrialsewingmachine") == null, "held matching diploma allows sewing purchase");
    Check(Denied("ba:itemname_kilnmachine") != null && Denied("ba:itemname_polishingmachine") != null, "other diplomas still needed despite sewing pass");
    Check(Denied("ba:itemname_lasercuttingmachine", "ba:itemname_consumergoodsassemblymachine") == null, "laser and plain consumer goods stay free");
    Check(Denied("ba:itemname_lasercuttingmachine", "ba:itemname_kilnmachine") != null, "mixed basket catches an unqualified machine");
    var notices = hooks[0].DeclaringType.Assembly.GetType("UI.Notification.Notifications", true);
    harmony.Patch(notices.GetMethod("ShowError", new[] { typeof(string), typeof(string), typeof(bool) }),
        prefix: new HarmonyMethod(typeof(NoticeSink).GetMethod("Capture")));
    var cargoType = typeof(List<>).MakeGenericType(hooks[1].DeclaringType.GetField("_cargoInstances", Any).FieldType.GetGenericArguments()[0]);
    IList Basket(params string[] items)
    {
        var list = (IList)Activator.CreateInstance(cargoType);
        foreach (var item in items)
        {
            var cargo = RuntimeHelpers.GetUninitializedObject(cargoType.GetGenericArguments()[0]);
            Set(cargo, "itemName", item); Set(cargo, "amount", 1); list.Add(cargo);
        }
        return list;
    }
    var ui = hooks[1].DeclaringType;
    var mode = ui.GetNestedType("Mode"); var uiType = ui.GetNestedType("Type");
    var mixed = Basket("ba:itemname_industrialsewingmachine", "ba:itemname_kilnmachine");
    bool Purchase(IList list, int m, Dictionary<int, int> quantities = null, int kind = 0) =>
        (bool)Call("PurchasePrefix", list, quantities ?? new Dictionary<int, int>(), Enum.ToObject(mode, m), Enum.ToObject(uiType, kind));
    Check(!Purchase(mixed, 0) && NoticeSink.Keys.Last() == "hardmode_factory_requires_hmp_electronics_technician", "checkout refuses mixed basket with matching in-world locale key");
    Check(Purchase(mixed, 1, new Dictionary<int, int> { { 0, 1 }, { 1, 0 } }), "single-toggle checkout ignores unselected locked machines");
    Check(!Purchase(mixed, 1, new Dictionary<int, int> { { 1, 1 } }), "single-toggle checkout refuses selected locked machine");
    Check(Purchase(mixed, 0, kind: 1), "tax payment UI stays untouched");
    var window = RuntimeHelpers.GetUninitializedObject(ui);
    Set(window, "_cargoInstances", mixed); Set(window, "_cargoInstancesAmounts", new Dictionary<int, int>());
    Set(window, "_mode", Enum.ToObject(mode, 0)); Set(window, "_type", Enum.ToObject(uiType, 0));
    int before = NoticeSink.Keys.Count;
    hooks[1].Invoke(window, null);
    Check(NoticeSink.Keys.Count == before + 1 && !(bool)Get(window, "_orderPlaced"), "actual patched checkout stops before money, queue and UI order state");
    var purchaser = Activator.CreateInstance(hooks[0].DeclaringType);
    var controllerType = purchaser.GetType().GetField("itemController").FieldType;
    var controller = RuntimeHelpers.GetUninitializedObject(controllerType);
    var sale = Activator.CreateInstance(controllerType.GetField("playerItemPurchaserSettings").FieldType);
    Set(sale, "enabled", true); Set(sale, "itemName", "ba:itemname_kilnmachine");
    Set(controller, "playerItemPurchaserSettings", sale); Set(purchaser, "itemController", controller);
    before = NoticeSink.Keys.Count;
    hooks[0].Invoke(purchaser, null);
    Check(NoticeSink.Keys.Count == before + 1, "actual patched depot pickup refuses before cargo is created");
    Set(sale, "enabled", false);
    Check((bool)Call("GrabPrefix", purchaser), "owned machine outside a sale is not gated");
    Set(director, "_broken", true);
    Observe("director-failed");
    Check(Denied("ba:itemname_kilnmachine") == null, "faulted careers director fails open");
    Set(director, "_broken", false);
    var settings = rlo.GetType("RealLifeOverhaul.Settings");
    foreach (string flag in new[] { "Careers", "CareersStudy" })
    {
        settings.GetField(flag).SetValue(null, false);
        Observe(flag == "Careers" ? "careers-disabled" : "study-disabled");
        Check(Denied("ba:itemname_kilnmachine") == null, flag + " off fails open immediately");
        settings.GetField(flag).SetValue(null, true);
    }
    Set(director, "_studyOn", false);
    Observe("study-unavailable");
    Check(Denied("ba:itemname_kilnmachine") == null, "stopped study fails open immediately");
    Set(director, "_studyOn", true);
    Option(false); Check(Denied("ba:itemname_kilnmachine") == null, "HMP option off fails open immediately"); Option(true);
    WriteStatic(catalogue, original);
    Observe("catalogue-unavailable");
    Check(Denied("ba:itemname_kilnmachine") == null, "lost catalogue injection fails open");
    var withoutHmp = Read(text);
    Check(((IList)Get(withoutHmp, "Courses")).Count == 0 && ((IList)Get(withoutHmp, "Results")).Count == 1, "unmodified RLO safely ignores removed diploma ids");
    Check((bool)withoutHmp.GetType().GetMethod("Holds").Invoke(withoutHmp, new object[] { 10 })
        && (long)Get(withoutHmp, "LoanCents") == 123456L && ((int[])Get(withoutHmp, "Skill"))[16] == 12000, "removal preserves ordinary diploma, skills and student debt");
    string resavedWithout = (string)Rlo("CareersStore", "Write", withoutHmp);
    Check(resavedWithout is string, "RLO can resave after HMP removal");
    WriteStatic(catalogue, extended);
    // A save played under an unreviewed RLO lost its diploma records; HMP held them (Core/DiplomaHold, RloGate).
    var hold = patch.GetType("HardModePatch.Core.DiplomaHold", true);
    var gateType = patch.GetType("HardModePatch.RloGate", true);
    string Keep(string line, string previous, int day) => (string)hold.GetMethod("Keep").Invoke(null, new object[] { line, previous, day });
    int HoldCount(string h) => (int)hold.GetMethod("Count").Invoke(null, new object[] { h });
    string held = Keep(text, null, 10);
    Check(held != null && HoldCount(held) == 3, "the hold takes the three HMP diploma attempts from RLO's line");
    var gameAssembly = hooks[0].DeclaringType.Assembly;
    Type gameType = gameAssembly.GetType("GameInstance", true);
    object save = RuntimeHelpers.GetUninitializedObject(gameType);
    var registrations = (IList)Activator.CreateInstance(gameType.GetField("BuildingRegistrations").FieldType);
    registrations.Add(RuntimeHelpers.GetUninitializedObject(registrations.GetType().GetGenericArguments()[0]));
    Set(save, "BuildingRegistrations", registrations);
    Set(save, "Day", 60);
    var modData = new Dictionary<string, string> { { "reallife:careers", resavedWithout }, { "hardmodepatch:factory_diplomas_held", held } };
    Set(save, "modData", modData);
    bool OnLoaded() => (bool)gateType.GetMethod("OnGameLoaded", Any).Invoke(null, new[] { save });
    Check(OnLoaded() && !modData.ContainsKey("hardmodepatch:factory_diplomas_held"), "reviewed RLO with courses registered: every held record given back at once, the hold cleared");
    var given = Read(modData["reallife:careers"]);
    Check(((IList)Get(given, "Courses")).Count == 2 && (int)Get(((IList)Get(given, "Courses"))[0], "Course") == 19
        && (bool)given.GetType().GetMethod("Holds").Invoke(given, new object[] { 18 })
        && (bool)given.GetType().GetMethod("Holds").Invoke(given, new object[] { 10 }) && (long)Get(given, "LoanCents") == 123456L,
        "RLO's own reader keeps every given-back course and result, ordinary records unchanged");
    Check(!OnLoaded(), "nothing held: the careers record is left alone");
    // RLO's catch-up after a load counts the classes of a course under way from its last counted class (at most two
    // weeks back) as missed. A held course must not have more counted missed than when it was held.
    int Missed(object e, int today)
    {
        var c = Rlo("StudyCatalogue", "Find", (int)Get(e, "Course"));
        object days = T("StudyRules").GetMethod("Days", Any).Invoke(null, new[] { c, Enum.ToObject(T("StudyMode"), (int)Get(e, "Mode")) });
        int n = 0;
        for (int d = Math.Max((int)Get(e, "Start"), Math.Max((int)Get(e, "SettledDay") + 1, today - 14)); d < today; d++)
            if ((bool)Rlo("StudyRules", "On", days, d) && (bool)Rlo("StudyRules", "Miss", e, d)) n++;
        return n;
    }
    object CourseIn(object file, int id) => ((IList)Get(file, "Courses")).Cast<object>().First(e => (int)Get(e, "Course") == id);
    string Settle(string careers, string h, int day)
    {
        modData.Clear(); modData["reallife:careers"] = careers; modData["hardmodepatch:factory_diplomas_held"] = h;
        Set(save, "Day", day);
        OnLoaded();
        return modData["reallife:careers"];
    }
    int before19 = Missed(CourseIn(Read(text), 19), 10);
    foreach (int day in new[] { 11, 13, 17, 31, 66 })
    {
        int after = Missed(CourseIn(Read(Settle(resavedWithout, held, day)), 19), day), unmoved = Missed(CourseIn(Read(text), 19), day);
        Check(after <= before19 && (day != 66 || after == before19), $"held from day 10 to {day}: {after} classes counted missed, {before19} when held ({unmoved} unmoved)");
    }
    // An exam held during its window comes back with at least the exam days it had left, on the same weekdays.
    var examFile = Read(text);
    var exam = CourseIn(examFile, 34);
    Set(exam, "Stage", 1); Set(exam, "ExamFrom", 8); Set(exam, "ExamTo", 12);
    string examLine = (string)Rlo("CareersStore", "Write", examFile);
    string examHeld = Keep(examLine, null, 12);
    int ExamDays(object e, int from) { int n = 0; for (int d = from; d <= (int)Get(e, "ExamTo"); d++) if ((bool)Rlo("StudyRules", "ExamDay", e, d)) n++; return n; }
    int examLeft = ExamDays(exam, 12);
    foreach (int day in new[] { 13, 14, 19, 60 })
    {
        var back = CourseIn(Read(Settle(resavedWithout, examHeld, day)), 34);
        Check(!(bool)Rlo("StudyRules", "ExamMissed", back, day) && ExamDays(back, day) >= examLeft,
            $"exam held on day 12 with {examLeft} exam day(s) left, back on day {day}: {ExamDays(back, day)} left, not missed");
    }
    // RLO left its line alone while held (its study switched off): the records are moved on where they are.
    string untouched = Settle(text, held, 66);
    Check(!modData.ContainsKey("hardmodepatch:factory_diplomas_held") && Missed(CourseIn(Read(untouched), 19), 66) == before19,
        "records RLO never rewrote are moved on in place and the hold is cleared");
    // A full results list: nothing goes back until everything fits, nothing is dropped.
    IList Results(int count)
    {
        var f = Activator.CreateInstance(T("StudyFile"));
        for (int i = 0; i < count; i++)
        {
            var r = Activator.CreateInstance(T("Enrolment"));
            Set(r, "Course", 10); Set(r, "Start", 100 + i); Set(r, "Stage", 2);
            ((IList)Get(f, "Results")).Add(r);
        }
        return new object[] { f };
    }
    var attempts = Activator.CreateInstance(T("StudyFile"));
    foreach (var (start, stage) in new[] { (1, 3), (1, 4), (15, 2) })
    {
        var r = Activator.CreateInstance(T("Enrolment"));
        Set(r, "Course", 18); Set(r, "Start", start); Set(r, "Stage", stage);
        ((IList)Get(attempts, "Results")).Add(r);
    }
    string attemptsHeld = Keep((string)Rlo("CareersStore", "Write", attempts), null, 10);
    string crowded = (string)Rlo("CareersStore", "Write", Results(39)[0]);
    Check(Settle(crowded, attemptsHeld, 20) == crowded && modData["hardmodepatch:factory_diplomas_held"] == attemptsHeld,
        "39 results and three held attempts, two on the same day: nothing given back, the hold waits unchanged");
    // While the hold waits, the player enrols in another HMP course and RLO saves it: the held attempts stay.
    var enrolled = Results(39)[0];
    var fresh = Activator.CreateInstance(T("Enrolment"));
    Set(fresh, "Course", 19); Set(fresh, "Start", 21);
    ((IList)Get(enrolled, "Courses")).Add(fresh);
    string withNew = (string)Rlo("CareersStore", "Write", enrolled);
    Check(Settle(withNew, attemptsHeld, 22) == withNew && HoldCount(modData["hardmodepatch:factory_diplomas_held"]) == 3,
        "a new HMP enrolment while the hold waits: the line is left alone and all three held attempts stay");
    string keptUnreviewed = Keep(withNew, attemptsHeld, 23);
    Check(HoldCount(keptUnreviewed) == 4, "the same save under an unreviewed RLO: the hold keeps the three attempts and adds the new course");
    var roomy = Read(Settle((string)Rlo("CareersStore", "Write", Results(37)[0]), attemptsHeld, 20));
    Check(((IList)Get(roomy, "Results")).Count == 40 && (bool)roomy.GetType().GetMethod("Holds").Invoke(roomy, new object[] { 18 })
        && !modData.ContainsKey("hardmodepatch:factory_diplomas_held"), "with room for all three, RLO keeps every attempt and the hold is cleared");
    modData.Clear();
    var holdsBridge = bridge.GetField("holds", Any);
    var savedHolds = holdsBridge.GetValue(null);
    holdsBridge.SetValue(null, null);
    Check(Denied("ba:itemname_kilnmachine") == null && (bool)bridge.GetField("failed", Any).GetValue(null), "reflection failure opens the sale and latches the bridge off");
    holdsBridge.SetValue(null, savedHolds);
    singleton.SetValue(null, null);
    var accepts = gateType.GetMethod("Accepts");
    Check(!(bool)accepts.Invoke(null, new object[] { patch }) && (bool)gateType.GetProperty("Refused").GetValue(null), "unreviewed assembly identity rejected");
    Check((bool)accepts.Invoke(null, new object[] { rlo }) && !(bool)gateType.GetProperty("Refused").GetValue(null), "reviewed RLO accepted");
    bridge.GetField("failed", Any).SetValue(null, true);
    Check(Denied("ba:itemname_kilnmachine") == null, "bridge fault latches gate off");
    bridge.GetField("failed", Any).SetValue(null, false);
    try
    {
        Call("Inject", rlo);
        Check(false, "colliding injection rejected");
    }
    catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
    { Check(ReferenceEquals(catalogue.GetValue(null), extended), "id collision rejects injection without partial write"); }

    // Duplicate-install takeover: the working copy retires and the waiting copy applies again in the same session,
    // both before RLO starts careers or indexes the campus and after it has.
    var showError = notices.GetMethod("ShowError", new[] { typeof(string), typeof(string), typeof(bool) });
    var campusCourses = rlo.GetType("RealLifeOverhaul.Careers.CampusRules", true).GetField("_courses", Any);
    var applied = bridge.GetField("active", Any);
    object Retire()
    {
        bridge.GetMethod("Revert").Invoke(target, null);
        harmony.UnpatchAll(harmony.Id);
        harmony.Patch(showError, prefix: new HarmonyMethod(typeof(NoticeSink).GetMethod("Capture")));
        var next = Activator.CreateInstance(bridge);
        return next;
    }
    IList<MethodBase> ApplyNext(object next)
    {
        var nextHooks = (IList<MethodBase>)bridge.GetMethod("ResolveHooks").Invoke(next, new object[] { rlo });
        bridge.GetMethod("Apply").Invoke(next, new object[] { harmony, rlo, nextHooks });
        return nextHooks;
    }
    Set(sale, "enabled", true);
    foreach (bool started in new[] { false, true })
    {
        string when = started ? "takeover after careers and campus start" : "takeover before careers start";
        var next = Retire();
        Check(!(bool)applied.GetValue(null) && (bool)bridge.GetProperty("CoursesRegistered").GetValue(null)
            && hooks.Take(3).All(h => Harmony.GetPatchInfo(h) == null || !Harmony.GetPatchInfo(h).Owners.Contains(harmony.Id)),
            when + ": retiring copy removes its hooks and keeps the courses for saves");
        singleton.SetValue(null, started ? director : null);
        WriteStatic(campusCourses, started ? new[] { 18, 19, 34 } : null);
        var nextHooks = ApplyNext(next);
        Check((bool)applied.GetValue(null) && ReferenceEquals(catalogue.GetValue(null), extended)
            && ((Array)catalogue.GetValue(null)).Length == original.Length + 3, when + ": replacement reuses the retained catalogue without reinjecting");
        Check(nextHooks.Take(3).All(h => Harmony.GetPatchInfo(h).Owners.Contains(harmony.Id)), when + ": replacement reinstalls pickup, checkout and offering hooks");
        Check(defs.All(c => (bool)Rlo("CareersDirector", "Offered", c)), when + ": option on still offers all three diplomas");
        if (!started)
        {
            Observe("director-unavailable");
            singleton.SetValue(null, director);
        }
        Observe("active");
        Check(Denied("ba:itemname_kilnmachine") != null && Denied("ba:itemname_industrialsewingmachine") == null, when + ": factory gate refuses an unqualified machine only");
        before = NoticeSink.Keys.Count;
        nextHooks[1].Invoke(window, null);
        Check(NoticeSink.Keys.Count == before + 1 && !(bool)Get(window, "_orderPlaced"), when + ": actual patched checkout still stops a locked machine");
        before = NoticeSink.Keys.Count;
        nextHooks[0].Invoke(purchaser, null);
        Check(NoticeSink.Keys.Count == before + 1, when + ": actual patched depot pickup still refuses a locked machine");
        Option(false);
        Check(defs.All(c => !(bool)Rlo("CareersDirector", "Offered", c)), when + ": option off hides new enrolments again");
        Option(true);
    }
    bool Refused(object next)
    {
        try { ApplyNext(next); return false; }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { return !(bool)applied.GetValue(null); }
    }
    WriteStatic(campusCourses, null);
    singleton.SetValue(null, null);
    var changed = Retire();
    WriteStatic(catalogue, original);
    Check(Refused(changed) && ReferenceEquals(catalogue.GetValue(null), original), "takeover refuses a catalogue changed since registration and writes nothing");
    WriteStatic(catalogue, extended);
    var injectedField = bridge.GetField("injected", Any);
    injectedField.SetValue(null, null);
    Check(Refused(Retire()) && ReferenceEquals(catalogue.GetValue(null), extended), "a copy that did not register the courses still refuses the occupied ids");
    injectedField.SetValue(null, extended);
}
finally
{
    bridge.GetMethod("Revert").Invoke(target, null);
    harmony.UnpatchAll(harmony.Id);
}
var missing = (IList<MethodBase>)bridge.GetMethod("ResolveHooks").Invoke(target, new object[] { patch });
Check(missing.Any(h => h == null), "missing or changed RLO shape cannot resolve");
Check(Call("Denied", (object)new[] { "ba:itemname_kilnmachine" }) == null, "missing or changed RLO leaves gate off");
Console.WriteLine($"{checks} offline factory checks passed; Unity UI, class play and actual saves still need an in-game check.");
return 0;

public static class NoticeSink
{
    public static readonly List<string> Keys = new();
    public static bool Capture(string __0) { Keys.Add(__0); return false; }
}
