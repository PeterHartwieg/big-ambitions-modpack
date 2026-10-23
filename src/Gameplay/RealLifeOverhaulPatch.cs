using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using Controllers;
using BigAmbitions.Items;
using Entities;
using HardModePatch.Core;
using HarmonyLib;
using UI.Notification;
using UI.Purchase;

namespace HardModePatch
{
    public sealed class RealLifeOverhaulPatch : ITargetPatch, IReviewedOnly
    {
        const string Careers = "RealLifeOverhaul.Careers.";
        static FieldInfo catalogue, courseId, courseKey, careersOn, studyOn, campusCache, broken;
        static MethodInfo instance, study, holds, find;
        static ConstructorInfo construct;
        static Type courseType, kindType, campusType;
        static Array injected;
        static bool active, failed;
        static readonly HashSet<string> warnings = new HashSet<string>();

        public TargetInfo Target { get { return Targets.RealLifeOverhaul; } }

        public bool Accepts(Assembly target) { return RloGate.Accepts(target); }

        // HMP's three courses are in RLO's catalogue, so RLO can read and keep their records.
        public static bool CoursesRegistered { get { return injected != null; } }

        static MethodInfo Method(Type type, string name, Type result, bool isStatic, params Type[] parameters)
        {
            var method = type == null ? null : AccessTools.DeclaredMethod(type, name, parameters);
            return method != null && method.IsStatic == isStatic && method.ReturnType == result ? method : null;
        }

        static FieldInfo Field(Type type, string name, Type value, bool isStatic)
        {
            var field = type == null ? null : AccessTools.DeclaredField(type, name);
            return field != null && field.IsStatic == isStatic && field.FieldType == value ? field : null;
        }

        public IList<MethodBase> ResolveHooks(Assembly target)
        {
            courseType = target.GetType(Careers + "CourseDef", false);
            kindType = target.GetType(Careers + "CourseKind", false);
            campusType = target.GetType(Careers + "Campus", false);
            var book = target.GetType(Careers + "StudyCatalogue", false);
            var director = target.GetType(Careers + "CareersDirector", false);
            var file = target.GetType(Careers + "StudyFile", false);
            var settings = target.GetType("RealLifeOverhaul.Settings", false);
            var campus = target.GetType(Careers + "CampusRules", false);
            if (courseType == null || kindType == null || campusType == null || book == null || director == null || file == null)
                return new MethodBase[] { null };
            catalogue = Field(book, "All", courseType.MakeArrayType(), true);
            courseId = Field(courseType, "Id", typeof(int), false);
            courseKey = Field(courseType, "Key", typeof(string), false);
            careersOn = Field(settings, "Careers", typeof(bool), true);
            studyOn = Field(settings, "CareersStudy", typeof(bool), true);
            campusCache = Field(campus, "_courses", typeof(int[]), true);
            broken = Field(director, "_broken", typeof(bool), false);
            instance = Method(director, "get_Instance", director, true);
            study = Method(director, "get_Study", file, false);
            holds = Method(file, "Holds", typeof(bool), false, typeof(int));
            find = Method(book, "Find", courseType, true, typeof(int));
            construct = AccessTools.Constructor(courseType, new[] { typeof(int), typeof(string), kindType, campusType,
                typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                typeof(int), typeof(int), typeof(bool), typeof(int[]), typeof(bool) });
            bool shape = catalogue != null && catalogue.IsInitOnly && courseId != null && courseKey != null && careersOn != null
                && studyOn != null && broken != null && campusCache != null && instance != null && study != null && holds != null && find != null && construct != null;
            return new MethodBase[]
            {
                shape ? Method(typeof(PlayerItemPurchaser), "GrabItem", typeof(void), false) : null,
                shape && Field(typeof(PurchaseUI), "_cargoInstances", typeof(List<CargoInstance>), false) != null
                    && Field(typeof(PurchaseUI), "_cargoInstancesAmounts", typeof(Dictionary<int, int>), false) != null
                    && Field(typeof(PurchaseUI), "_mode", typeof(PurchaseUI.Mode), false) != null
                    && Field(typeof(PurchaseUI), "_type", typeof(PurchaseUI.Type), false) != null
                    ? Method(typeof(PurchaseUI), "PlaceOrder", typeof(void), false) : null,
                shape ? Method(director, "Offered", typeof(bool), true, courseType) : null,
                find, holds, construct, book.TypeInitializer, instance, study,
                campus == null ? null : Method(campus, "get_Courses", typeof(int[]), true)
            };
        }

        // The catalogue, constructor, save reader and campus cache are a single compatibility boundary.
        // Unknown RLO binaries must be reviewed before adding courses to their persistent id space.
        public IList<ContractCheck> Contracts(Assembly target, IList<MethodBase> hooks)
        {
            return new[]
            {
                new ContractCheck(hooks[0], new Contract("PlayerItemPurchaser.GrabItem", "280b3531bdd6f0c5")),
                new ContractCheck(hooks[1], new Contract("PurchaseUI.PlaceOrder", "a363d5c1244b1fbe"))
            };
        }

        public void Apply(Harmony harmony, Assembly target, IList<MethodBase> hooks)
        {
            if (!RloGate.Accepts(target)) throw new InvalidOperationException("Factory diplomas: RLO's catalogue/save layout has not been reviewed; machine sales stay unrestricted.");
            if (hooks.Any(h => h == null)) throw new MissingMemberException("Factory diploma hooks changed.");
            if (instance.Invoke(null, null) != null || campusCache.GetValue(null) != null)
                throw new InvalidOperationException("Factory diplomas must be installed before RLO starts careers or indexes the campus; restart with HMP loaded at initialization.");
            foreach (var check in Contracts(target, hooks))
                if (IlText.Fingerprint(check.Method) != check.Contract.Expected)
                    throw new InvalidOperationException("Factory diploma purchase path changed: " + check.Contract.Name);
            failed = false;
            Inject(target);
            harmony.Patch(hooks[0], prefix: new HarmonyMethod(typeof(RealLifeOverhaulPatch), "GrabPrefix"));
            harmony.Patch(hooks[1], prefix: new HarmonyMethod(typeof(RealLifeOverhaulPatch), "PurchasePrefix"));
            harmony.Patch(hooks[2], postfix: new HarmonyMethod(typeof(RealLifeOverhaulPatch), "OfferedPostfix"));
            active = true;
            Log.Info("Factory diplomas: three school diplomas registered before careers start (ids 18, 19, 34); machine gate is opt-in.");
        }

        static int Constant(Assembly target, string type, string name)
        {
            var field = target.GetType(type, true).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null || !field.IsLiteral || field.FieldType != typeof(int)) throw new MissingFieldException(type, name);
            return (int)field.GetRawConstantValue();
        }

        static void Inject(Assembly target)
        {
            var original = (Array)catalogue.GetValue(null);
            int limit = Constant(target, Careers + "StudyCatalogue", "IdsMost");
            if (original == null || !FactoryDiplomas.CanInject(original.Cast<object>().Select(c => (int)courseId.GetValue(c)), limit))
                throw new InvalidOperationException("Factory diploma ids are occupied or outside RLO's mask.");
            var extended = Array.CreateInstance(courseType, original.Length + FactoryDiplomas.All.Length);
            Array.Copy(original, extended, original.Length);
            int i = original.Length;
            foreach (var c in FactoryDiplomas.All)
            {
                Func<string, int> skill = name => Constant(target,
                    name == "Handling" || name == "Electrical" || name == "Joinery" ? Careers + "Skills" : "RealLifeOverhaul.Tech.TalentCatalogue", name);
                object definition = construct.Invoke(new object[] { c.Id, c.Key, Enum.Parse(kindType, "Vocational"), Enum.Parse(campusType, "School"),
                    FactoryDiplomas.Sessions, FactoryDiplomas.Homework, FactoryDiplomas.Hours, c.Days, c.Hour, c.Fee,
                    skill(c.SkillA), c.GainA, skill(c.SkillB), c.GainB, Constant(target, "RealLifeOverhaul.Tech.TalentCatalogue", "Diploma"), -1, false, null, false });
                extended.SetValue(definition, i++);
            }
            // Write the array reference once, before RLO builds any consumers. No element of its original array is changed.
            var setter = new DynamicMethod("SetFactoryCatalogue", typeof(void), new[] { typeof(Array) }, typeof(RealLifeOverhaulPatch).Module, true);
            var il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, catalogue.FieldType);
            il.Emit(OpCodes.Stsfld, catalogue);
            il.Emit(OpCodes.Ret);
            ((Action<Array>)setter.CreateDelegate(typeof(Action<Array>)))(extended);
            if (!ReferenceEquals(catalogue.GetValue(null), extended) || FactoryDiplomas.All.Any(c => find.Invoke(null, new object[] { c.Id }) == null))
                throw new InvalidOperationException("RLO did not retain the extended catalogue.");
            injected = extended;
        }

        public void Revert()
        {
            active = false;
            // Keep the definitions until process exit so RLO can still read and write an enrolled student's record.
        }

        public static void ReportAvailability()
        {
            if (Rules.FactoryDiplomas.Enabled && (!active || failed) && !RloGate.Refused) WarnOnce("RLO is missing, not loaded or incompatible");
        }

        static void WarnOnce(string reason)
        {
            if (warnings.Add(reason)) Log.Warn("Factory diplomas: " + reason + "; machine sales stay unrestricted.");
        }

        static bool CatalogueIntact()
        {
            if (!ReferenceEquals(catalogue.GetValue(null), injected)) return false;
            foreach (var c in FactoryDiplomas.All)
            {
                var found = find.Invoke(null, new object[] { c.Id });
                if (found == null || !string.Equals((string)courseKey.GetValue(found), c.Key, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        static bool Ready(out object record)
        {
            record = null;
            if (!Rules.FactoryDiplomas.Enabled) return false;
            try
            {
                if (!active || failed || injected == null || !CatalogueIntact())
                { WarnOnce("catalogue injection is unavailable"); return false; }
                bool careers = (bool)careersOn.GetValue(null), studying = (bool)studyOn.GetValue(null);
                if (!careers || !studying) { WarnOnce("RLO careers or study is switched off"); return false; }
                object director = instance.Invoke(null, null);
                record = director == null || (bool)broken.GetValue(director) ? null : study.Invoke(director, null);
                if (record == null) WarnOnce("RLO's study record is unavailable");
                return FactoryDiplomas.GateReady(true, true, careers, studying, record != null);
            }
            catch (Exception ex) { Fail(ex); return false; }
        }

        static void Fail(Exception ex)
        {
            failed = true;
            WarnOnce("RLO's study bridge failed (" + ex.GetType().Name + ")");
        }

        static FactoryDiploma Denied(IEnumerable<string> items)
        {
            var courses = items.Select(FactoryDiplomas.ForItem).Where(c => c != null).ToArray();
            if (courses.Length == 0) return null;
            object record;
            if (!Ready(out record)) return null;
            try
            {
                foreach (var c in courses)
                    if (FactoryDiplomas.Refuse(c.Item, true, (bool)holds.Invoke(record, new object[] { c.Id }))) return c;
            }
            catch (Exception ex) { Fail(ex); }
            return null;
        }

        static bool Notify(FactoryDiploma denied)
        {
            if (denied == null) return true;
            Notifications.ShowError("hardmode_factory_requires_" + denied.Key, "HmpFactoryDiploma");
            return false;
        }

        static bool GrabPrefix(PlayerItemPurchaser __instance)
        {
            try
            {
                var controller = __instance.itemController;
                var settings = ReferenceEquals(controller, null) ? null : controller.playerItemPurchaserSettings;
                return settings == null || !settings.enabled || Notify(Denied(new[] { settings.itemName }));
            }
            catch (Exception ex) { Fail(ex); return true; }
        }

        static bool PurchasePrefix(List<CargoInstance> ____cargoInstances, Dictionary<int, int> ____cargoInstancesAmounts,
            PurchaseUI.Mode ____mode, PurchaseUI.Type ____type)
        {
            try
            {
                if (____type != PurchaseUI.Type.Purchase || ____cargoInstances == null) return true;
                var selected = new List<string>();
                for (int i = 0; i < ____cargoInstances.Count; i++)
                {
                    int quantity;
                    if (____mode == PurchaseUI.Mode.SingleToggle && (!____cargoInstancesAmounts.TryGetValue(i, out quantity) || quantity <= 0)) continue;
                    var cargo = ____cargoInstances[i];
                    if (cargo != null && cargo.amount > 0) selected.Add(cargo.itemName);
                }
                return Notify(Denied(selected));
            }
            catch (Exception ex) { Fail(ex); return true; }
        }

        static void OfferedPostfix(object c, ref bool __result)
        {
            try
            {
                if (c == null || !__result) return;
                if (FactoryDiplomas.ForId((int)courseId.GetValue(c)) != null && (!active || failed || !Rules.FactoryDiplomas.Enabled)) __result = false;
            }
            catch (Exception ex) { Fail(ex); }
        }
    }
}
