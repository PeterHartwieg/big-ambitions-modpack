using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Entities;
using Enums;
using HardModePatch.Core;
using HarmonyLib;
using Localizor.LanguageChangeEvent;
using UI.Tasks;

namespace HardModePatch
{
    // Read-only presentation: no closure, account, task or save is changed.
    public static class UtilityClosureReasonPatch
    {
        static readonly Harmony harmony = new Harmony(GameplayFeatures.HarmonyId + ".closurereason");
        static MethodInfo instance;
        static FieldInfo reasons, save, loaded, failed, enabled, closures, place, day, hour, source;
        static bool warned;
        public static bool Available { get; private set; }

        static FieldInfo Field(Type t, string name, Type type, bool isStatic = false)
        {
            var f = t == null ? null : AccessTools.DeclaredField(t, name);
            return f != null && f.IsStatic == isStatic && f.FieldType == type ? f : null;
        }

        // Include every method referenced by the adapter. All RLO fields are shape-checked too.
        public static IList<MethodBase> ResolveHooks(Assembly rlo)
        {
            var director = rlo.GetType("RealLifeOverhaul.Utilities.UtilitiesDirector");
            var state = rlo.GetType("RealLifeOverhaul.BusinessState");
            var closure = rlo.GetType("RealLifeOverhaul.Closure");
            var closureSource = rlo.GetType("RealLifeOverhaul.ClosureSource");
            instance = director == null ? null : AccessTools.DeclaredProperty(director, "Instance")?.GetGetMethod();
            reasons = Field(director, "_toldClosed", typeof(Dictionary<string, string>));
            save = Field(director, "_save", typeof(GameInstance));
            loaded = Field(director, "_loaded", typeof(bool));
            failed = Field(director, "_failed", typeof(bool));
            enabled = Field(rlo.GetType("RealLifeOverhaul.Settings"), "Utilities", typeof(bool), true);
            closures = closure == null ? null : Field(state, "Closures", typeof(List<>).MakeGenericType(closure), true);
            place = Field(closure, "Place", typeof(string));
            day = Field(closure, "Day", typeof(int));
            hour = Field(closure, "Hour", typeof(int));
            source = Field(closure, "Source", closureSource);
            bool shape = instance != null && instance.IsStatic && instance.ReturnType == director
                && new[] { reasons, save, loaded, failed, enabled, closures, place, day, hour, source }.All(f => f != null)
                && closureSource.IsEnum && Convert.ToInt32(Enum.Parse(closureSource, "Utilities")) == 1;
            return new MethodBase[]
            {
                shape ? AccessTools.DeclaredMethod(typeof(TasksUI), "GetTodoDescription", new[] { typeof(TodoTask), typeof(bool) }) : null,
                instance
            };
        }

        public static void Start()
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnLoad;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) TryPatch(a);
        }

        public static void Stop()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnLoad;
            harmony.UnpatchAll(harmony.Id);
            Available = false;
        }

        static void OnLoad(object sender, AssemblyLoadEventArgs e) { TryPatch(e.LoadedAssembly); }

        static void TryPatch(Assembly a)
        {
            if (Available || a.GetName().Name != Targets.RealLifeOverhaul.AssemblyName || !RloGate.Accepts(a)) return;
            try
            {
                if (!TargetGate.Reviewed(Patcher.HashOf(a), new[] { "4f82b4f88f919178", "f339319c9ec1ad65", "43c4eeed466c7149" }))
                { Log.Info("Utility closure reasons: this presentation adapter is reviewed for RLO 0.49.0 and 0.50.0."); return; }
                var hooks = ResolveHooks(a);
                if (hooks.Any(m => m == null)) { Log.Warn("Utility closure reasons unavailable: RLO or game members changed."); return; }
                harmony.Patch(hooks[0], postfix: new HarmonyMethod(typeof(UtilityClosureReasonPatch), "Postfix"));
                Available = true;
                Log.Info("Utility closure reasons: BizMan reads RLO's current utility closure records.");
            }
            catch (Exception e) { harmony.UnpatchAll(harmony.Id); Log.Warn("Utility closure reasons unavailable: " + e.Message); }
        }

        static void Postfix(TodoTask task, ref LanguageChangeEventDataHolder __result)
        {
            if (!Available || !Rules.UtilityClosureReasons.Enabled || task == null
                || task.type != TodoTaskType.BusinessTemporarilyClosed || __result.Key != "alert_business_temporarily_closed") return;
            try
            {
                var current = SaveGameManager.Current;
                var director = instance.Invoke(null, null);
                if (current == null || director == null || !ReferenceEquals(save.GetValue(director), current)
                    || !(bool)loaded.GetValue(director) || (bool)failed.GetValue(director) || !(bool)enabled.GetValue(null)) return;
                if (task.address == null || current.BuildingRegistrations == null) return;
                var reg = current.BuildingRegistrations.FirstOrDefault(r => r != null
                    && r.StreetName == task.address.streetName && r.StreetNumber == task.address.streetNumber);
                if (reg == null || !reg.temporarilyClosed || !(reg.RentedByPlayer || reg.BuildingOwnedByPlayer)) return;
                string key = RunningCostsPatch.Key(reg.StreetName, reg.StreetNumber), reason;
                if (!((Dictionary<string, string>)reasons.GetValue(director)).TryGetValue(key, out reason)) return;
                long now = (long)current.Day * 24 + current.Hour;
                foreach (var c in (IEnumerable)closures.GetValue(null))
                {
                    if ((string)place.GetValue(c) != key || Convert.ToInt32(source.GetValue(c)) != 1) continue;
                    var label = UtilityClosureReason.Key(reason, true, now, (long)(int)day.GetValue(c) * 24 + (int)hour.GetValue(c));
                    if (label != null) __result.Key = label;
                    return;
                }
            }
            catch (Exception e)
            {
                if (!warned) { warned = true; Log.Warn("Utility closure reason left unchanged: " + e.Message); }
            }
        }
    }
}
