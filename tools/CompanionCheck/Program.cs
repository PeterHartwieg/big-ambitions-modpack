using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using HarmonyLib;

if (args.Length < 2 || args.Length > 3) throw new ArgumentException("CompanionCheck <game Managed folder> <HARTModPack.dll> [fixture output directory]");
const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
string managed = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    if (new[] { "mscorlib", "System", "System.Core", "netstandard" }.Contains(name.Name)) return null;
    string file = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(file) ? ctx.LoadFromAssemblyPath(file) : null;
};
Assembly Load(string p) => AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(p));
var pack = Load(args[1]); var gameDll = Load(Path.Combine(managed, "BigAmbitions.dll")); var itemsDll = Load(Path.Combine(managed, "BigAmbitions.Items.dll"));
Type P(string name) => pack.GetType(name, true);
object Empty(Type t) => RuntimeHelpers.GetUninitializedObject(t);
object Get(object owner, string name) => owner.GetType().GetField(name, Any).GetValue(owner);
void Set(object owner, string name, object value) { var f = owner.GetType().GetField(name, Any); f.SetValue(owner, f.FieldType.IsEnum ? Enum.ToObject(f.FieldType, value) : value); }
void Static(Type t, string name, object value) => t.GetField(name, Any).SetValue(null, value);
object Call(Type t, string name, params object[] values) => t.GetMethod(name, Any).Invoke(null, values);
IList List(Type t) => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(t));
int count = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); Console.WriteLine("PASS " + label); count++; }
object save = Empty(gameDll.GetType("GameInstance", true));
Set(save, "characterId", "synthetic-companion"); Set(save, "Day", 9); Set(save, "Hour", 12); Set(save, "Minute", 17.75f);
Set(save, "realEstate", List(save.GetType().GetField("realEstate").FieldType.GetGenericArguments()[0]));
var data = new Dictionary<string, string> { { "unrelated:record", "unchanged" } }; Set(save, "modData", data);
var regType = gameDll.GetType("BuildingRegistration", true); var regs = List(regType); Set(save, "BuildingRegistrations", regs);
Static(gameDll.GetType("SaveGameManager", true), "_current", save);
var itemType = itemsDll.GetType("BigAmbitions.Items.Item", true); var instType = itemsDll.GetType("BigAmbitions.Items.ItemInstance", true);
var definitions = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), itemType));
Static(itemsDll.GetType("BigAmbitions.Items.ItemsGetter", true), "ItemsDictionary", definitions);
object Item(string name, int flags)
{
    var item = Empty(itemType); Set(item, "itemName", name); Set(item, "type", flags);
    Load(Path.Combine(managed, "UnityEngine.CoreModule.dll")).GetType("UnityEngine.Object", true).GetField("m_CachedPtr", Any).SetValue(item, new IntPtr(1));
    Set(item, "furnitureRequirements", List(itemsDll.GetType("BigAmbitions.Items.FurnitureRequirement", true)));
    definitions[name] = item; return item;
}
Item("ba:itemname_industrialfridge", 0); Item("ba:itemname_computer", 0x800000); Item("fixture:stock-computer", 0x800002);
var reg = Empty(regType); Set(reg, "StreetName", "Synthetic Avenue"); Set(reg, "StreetNumber", 1); Set(reg, "RentedByPlayer", true); Set(reg, "businessTypeName", "ba:businesstype_lawfirm"); regs.Add(reg);
var placed = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), instType));
var instance = Empty(instType); Set(instance, "itemName", "ba:itemname_computer"); Set(instance, "id", "pc"); placed["pc"] = instance;
Set(reg, "itemInstances", placed);
var costs = P("HardModePatch.RunningCostsPatch"); var features = P("HardModePatch.GameplayFeatures");
var guard = features.GetField("guard", Any).GetValue(null);
void Active(bool value) => Set(guard, "ownsFeatures", value);
void Blocked(bool value) => Set(guard, "<Blocked>k__BackingField", value);
void Rule(string name, bool value) => Set(P("HardModePatch.Core.Rules").GetField(name).GetValue(null), "Enabled", value);
string Capture() => (string)Call(P("HARTModPack.Companion"), "CaptureV1", save);
JsonElement Module(string text, string module) => JsonDocument.Parse(text).RootElement.GetProperty("modules").GetProperty(module);
string State()
{
    var loads = (IDictionary)costs.GetField("loads", Any).GetValue(null); var places = (IDictionary)costs.GetField("places", Any).GetValue(null);
    return JsonSerializer.Serialize(new { data, day=Get(save,"Day"), itemCache=Get(instance,"_itemCached") == null,
        addressCache=Get(instance,"_addressCached") == null, stamp=costs.GetField("stampDay",Any).GetValue(null), loads=loads.Count, places=places.Count,
        failed=P("HardModePatch.RealLifeOverhaulPatch").GetField("failed",Any).GetValue(null),
        warnings=((IEnumerable)P("HardModePatch.RealLifeOverhaulPatch").GetField("warnings",Any).GetValue(null)).Cast<object>().ToArray() });
}
void Fixture(string name, string text)
{
    Check(text != null, name + " capture exists");
    if (args.Length == 3) { Directory.CreateDirectory(args[2]); File.WriteAllText(Path.Combine(args[2],name+".json"),text+"\n"); }
}
var before = State(); var inactive = Capture(); Fixture("inactive", inactive);
Check(before == State() && inactive == Capture(), "repeated production capture is deterministic and does not warm items or mutate caches/save");
Check(Module(inactive,"runtime").GetProperty("data").GetProperty("gameplayActive").GetBoolean() == false, "inactive owner is explicit");
Check(JsonDocument.Parse(inactive).RootElement.GetProperty("captured").GetProperty("minute").GetInt32() == 17, "whole minute identity");
Active(true); Rule("EquipmentUtilities",true); Rule("OfficeIt",true); Static(costs,"<Available>k__BackingField",true);
var ready = Capture();
var site = Module(ready,"runningCosts").GetProperty("data").GetProperty("currentSites")[0];
Check(site.GetProperty("openWatts").GetInt32() == 150 && site.GetProperty("itSeatDayCents").ValueKind == JsonValueKind.Null, "cold item projection and unknown internet are explicit");
before = State(); Check(Capture() == ready && before == State(), "active capture leaves all observed state untouched");
// Fill billing cache through the production billing owner, then change furniture. Capture sees next-bill input without altering it.
Call(costs,"Refresh"); Call(costs,"LoadOf",reg); Set(instance,"itemName","ba:itemname_industrialfridge");
before=State(); var cached=Capture(); site=Module(cached,"runningCosts").GetProperty("data").GetProperty("currentSites")[0];
Check(site.GetProperty("inputBasis").GetString()=="billing-cache" && site.GetProperty("openWatts").GetInt32()==150 && before==State(), "capture preserves effective cached billing inputs and cache contents");
Set(save,"Day",10); before=State(); site=Module(Capture(),"runningCosts").GetProperty("data").GetProperty("currentSites")[0];
Check(site.GetProperty("inputBasis").GetString()=="current-items" && site.GetProperty("alwaysWatts").GetInt32()==500 && before==State(), "stale billing cache is virtually refreshed without writes");
Set(save,"Day",9); Set(instance,"itemName","ba:itemname_computer");
Blocked(true); Active(false); var blocked=Capture(); Fixture("blocked",blocked);
Check(Module(blocked,"runtime").GetProperty("data").GetProperty("blockedByLegacy").GetBoolean(), "legacy stand-down overrides selected running-cost options");
// Corrupt held record and missing catalogue fail independently and never call mutating recovery helpers.
data["hardmodepatch:factory_diplomas_held"]="malformed"; Static(itemsDll.GetType("BigAmbitions.Items.ItemsGetter",true),"ItemsDictionary",null);
before=State(); var partial=Capture(); Fixture("partial",partial);
Check(Module(partial,"runningCosts").GetProperty("status").GetString()=="unavailable" && Module(partial,"factoryPurchases").GetProperty("data").GetProperty("heldRecords").GetProperty("status").GetString()=="unavailable" && before==State(), "unavailable catalogue and malformed holds preserve unrelated modules and state");
Static(itemsDll.GetType("BigAmbitions.Items.ItemsGetter",true),"ItemsDictionary",definitions); data.Remove("hardmodepatch:factory_diplomas_held");
// Exercise real save relay without Unity's host creation; only the engine host is stubbed.
var api=Load(Path.Combine(managed,"BigAmbitions.ModAPI.dll")); var lifecycle=api.GetType("BAModAPI.Services.UnityLifecycleProvider",true);
var harmony=new Harmony("companioncheck.engine"); harmony.Patch(lifecycle.GetMethod("EnsureInstance",Any),prefix:new HarmonyMethod(typeof(Stubs).GetMethod(nameof(Stubs.Skip))));
var publisher=P("HARTModPack.CompanionSave"); var eventField=gameDll.GetType("GlobalEvents",true).GetField("onSaveGame",Any);
int unrelatedCalls=0; Action unrelated=()=>unrelatedCalls++; eventField.SetValue(null,unrelated);
Call(publisher,"Start"); Call(publisher,"Start"); Call(publisher,"Ensure");
Check(((Action)eventField.GetValue(null)).GetInvocationList().Length==2,"one relay across duplicate starts");
((Action)eventField.GetValue(null))();
Check(data.ContainsKey("hartmodpack:companion") && data["unrelated:record"]=="unchanged" && unrelatedCalls==1,"normal-save publisher touches only own key");
eventField.SetValue(null,null); Call(publisher,"Ensure"); ((Action)eventField.GetValue(null))();
Check(((Action)eventField.GetValue(null)).GetInvocationList().Length==1,"relay reattaches after game resets events");
Set(save,"characterId",""); ((Action)eventField.GetValue(null))();
Check(!data.ContainsKey("hartmodpack:companion") && data["unrelated:record"]=="unchanged","capture failure removes only stale own report");
Set(save,"characterId","synthetic-companion"); Call(publisher,"Stop"); Call(publisher,"Start");
Check(((Action)eventField.GetValue(null)).GetInvocationList().Length==1,"owner takeover does not duplicate relay");
Call(publisher,"Stop"); Check(eventField.GetValue(null)==null,"unload detaches relay"); harmony.UnpatchAll(harmony.Id);
// Optional reviewed-RLO fixture tests the current seat component against its actual account model.
string rloPath=Environment.GetEnvironmentVariable("BA_COMPANION_RLO");
if(!string.IsNullOrEmpty(rloPath))
{
    var rlo=Load(rloPath); Type R(string name)=>rlo.GetType("RealLifeOverhaul."+name,true);
    Blocked(false);Active(true);Static(costs,"<Available>k__BackingField",false);Call(costs,"TryPatch",rlo);
    if(Environment.GetEnvironmentVariable("BA_COMPANION_EXPECT_REFUSED")=="1")
    {
        var refused=Capture();Fixture("refused",refused);
        Check(!(bool)costs.GetProperty("Available").GetValue(null),"unreviewed RLO never enables running-cost hooks");
        var runtime=Module(refused,"runtime").GetProperty("data");
        Check(runtime.GetProperty("rloReview").GetString()=="refused" && !runtime.GetProperty("rules").GetProperty("equipment_utilities").GetProperty("effective").GetBoolean(),"unreviewed binary reports refused and effective false");
    }
    else
    {
    Check((bool)costs.GetProperty("Available").GetValue(null),"current reviewed RLO running-cost hooks apply");
    var utility=Empty(R("Utilities.UtilitiesDirector"));
    Load(Path.Combine(managed,"UnityEngine.CoreModule.dll")).GetType("UnityEngine.Object",true).GetField("m_CachedPtr",Any).SetValue(utility,new IntPtr(1));
    var state=Activator.CreateInstance(R("Utilities.UtilitiesState"));Set(utility,"_state",state);Set(utility,"_save",save);Set(utility,"_loaded",true);
    Static(R("Utilities.UtilitiesDirector"),"<Instance>k__BackingField",utility);Static(R("Settings"),"Utilities",true);
    var account=Activator.CreateInstance(R("Utilities.Account"));Set(account,"Place","Synthetic%20Avenue~1");Set(account,"Service",2);Set(account,"State",1);
    ((IList)Get(state,"Accounts")).Add(account);
    Fixture("ready",Capture());
    int Seat()=>Module(Capture(),"runningCosts").GetProperty("data").GetProperty("currentSites")[0].GetProperty("itSeatDayCents").GetInt32();
    Check(Seat()==3000,"active Internet adds the captured one-seat daily share");
    Set(account,"State",0);Check(Seat()==0,"inactive Internet does not accrue seat costs");Set(account,"State",1);
    Set(account,"OutageFrom",9*24+23);Set(account,"OutageUntil",10*24+2);Check(Seat()==0,"outage start day receives no seat accrual even before start hour");
    Set(save,"Day",10);Check(Seat()==3000,"cross-midnight outage does not exempt the following billing day");
    Set(account,"OutageUntil",9*24+23);Set(save,"Day",9);Check(Seat()==3000,"empty outage window is not a free billing day");
    var accountBefore=JsonSerializer.Serialize(new{accrued=Get(account,"Accrued"),from=Get(account,"OutageFrom"),until=Get(account,"OutageUntil")});
    for(int i=0;i<3;i++)Capture();
    Check(accountBefore==JsonSerializer.Serialize(new{accrued=Get(account,"Accrued"),from=Get(account,"OutageFrom"),until=Get(account,"OutageUntil")}),"seat observation never accrues or changes the RLO account");
    Set(utility,"_save",Empty(save.GetType()));
    Check(Module(Capture(),"runningCosts").GetProperty("data").GetProperty("currentSites")[0].GetProperty("itStatus").GetString()=="unavailable","foreign director save is unknown");
    Call(costs,"Stop");
    }
}
// Pure station pairing shares production root traversal without warming live item caches.
int seatFlag=Convert.ToInt32(Enum.Parse(itemsDll.GetType("BigAmbitions.Items.ItemType",true),"Seat"));
Item("fixture:cafe-seat",seatFlag);Item("fixture:cafe-desk",0);
var cafe=Empty(regType);
var furniture=(IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string),instType));
object Placed(string id,string name,string parent)
{
    var value=Empty(instType);Set(value,"id",id);Set(value,"itemName",name);Set(value,"parentId",parent);furniture[id]=value;return value;
}
var desk=Placed("desk","fixture:cafe-desk",null);var pc=Placed("pc","ba:itemname_computer","desk");var seat=Placed("seat","fixture:cafe-seat","desk");
Set(cafe,"itemInstances",furniture);
int Stations()=>(int)Call(P("HardModePatch.InternetCafePatch"),"ObserveStations",cafe,true);
Check(Stations()==1 && Stations()==1 && Get(pc,"_itemCached")==null && Get(seat,"_itemCached")==null,
    "pure cafe pairing finds one shared-root station without item cache writes");
Set(seat,"parentId",null);Check(Stations()==0,"unpaired cafe chair is not a station");
Set(seat,"parentId","missing");Check(Stations()==0,"missing cafe parent is not a station");
Set(seat,"parentId","desk");Set(desk,"parentId","pc");Check(Stations()==0,"cyclic cafe roots terminate without inventing stations");
// Production arithmetic vectors, never duplicated arithmetic in the generator.
var core=P("HardModePatch.Core.RunningCosts"); var applianceType=core.GetNestedType("Appliance"); var vectors=new List<object>();
foreach (bool power in new[]{true,false}) foreach(int hours in new[]{0,2,12,24}) foreach(long use in new[]{0L,1001L,12000L})
{
    object a=Activator.CreateInstance(applianceType,150,500,40);
    vectors.Add(new { power,rloUse=use,hours,openWatts=150,alwaysWatts=500,litresPerOpenHour=40,
        expectedUse=(long)Call(core,"DayUse",power,use,hours,a) });
}
var seats=new List<object>();
foreach(int n in new[]{0,1,3,1000}) foreach(int level in new[]{499,500,999,1000,1234,4001}) foreach(int days in new[]{0,5,30,31})
    seats.Add(new{seats=n,levelThousandths=level,monthDays=days,expectedCents=(int)Call(core,"SeatDayCents",n,level,days)});
if(args.Length==3)File.WriteAllText(Path.Combine(args[2],"running-costs-vectors.json"),JsonSerializer.Serialize(new{model="hart-running-costs-v1",use=vectors,seatDay=seats})+"\n");
Console.WriteLine($"ALL {count} COMPANION CHECKS PASSED (synthetic offline)");
public static class Stubs { public static bool Skip()=>false; }
