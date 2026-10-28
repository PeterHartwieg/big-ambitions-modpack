using System;
using System.Collections.Generic;
using System.Reflection;
using HardModePatch.Core;
using HarmonyLib;

namespace HardModePatch
{
    // Meaningful Ambience (3810641423) adds building capacity × decor score × preset customers every open
    // hour (GameBridge.ProjectRetail), ignoring the difficulty's customer promotion, marketing and the hour.
    // The postfix rescales that bonus to a share of the customers the game itself sends, and recomputes
    // the projection fields the mod's ledger and injection read. Offices already scale with game clients.
    public sealed class MeaningfulAmbiencePatch : ITargetPatch
    {
        const string BridgeType = "MeaningfulAmbience.GameBridge";
        const string ProjectionType = "MeaningfulAmbience.CustomerProjection";
        // Every projection field the postfix reads or writes, with the type it must have.
        static readonly KeyValuePair<string, Type>[] Fields =
        {
            Field<bool>("Supported"), Field<bool>("IsOffice"), Field<bool>("IsOpen"), Field<bool>("GameGeneratesCustomers"),
            Field<bool>("NothingBought"), Field<double>("VanillaRaw"), Field<int>("VanillaTarget"), Field<int>("EffectiveCapacity"),
            Field<double>("AmbienceRaw"), Field<double>("CombinedRaw"), Field<int>("FinalTarget"),
            Field<int>("TheoreticalAddedWithoutCapacity"), Field<int>("AddedAfterCapacity"), Field<int>("BlockedByCapacity"),
            Field<int>("TotalBlockedByCapacity")
        };
        // ProjectRetail's reviewed body (mod hash 5564717279c75596): the postfix redoes its last steps, so
        // a change to its arithmetic must leave the mod unpatched.
        static readonly Contract ProjectBody = new Contract("GameBridge.ProjectRetail", "ca0915815150d570");

        static KeyValuePair<string, Type> Field<T>(string name) { return new KeyValuePair<string, Type>(name, typeof(T)); }
        static readonly Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();

        public TargetInfo Target { get { return Targets.MeaningfulAmbience; } }

        public IList<MethodBase> ResolveHooks(Assembly target)
        {
            var bridge = target.GetType(BridgeType, false);
            var projection = target.GetType(ProjectionType, false);
            MethodBase project = null;
            if (bridge != null && projection != null && ReadFields(projection))
            {
                var method = AccessTools.Method(bridge, "ProjectRetail", new[] { typeof(object), typeof(int), typeof(double), typeof(double) });
                if (method != null && method.ReturnType == projection) project = method;
            }
            return new[] { project };
        }

        static bool ReadFields(Type projection)
        {
            fields.Clear();
            if (projection.IsValueType) return false;   // the postfix writes into the returned object
            foreach (var wanted in Fields)
            {
                var field = AccessTools.Field(projection, wanted.Key);
                if (field == null || field.IsStatic || field.IsInitOnly || field.IsLiteral || field.FieldType != wanted.Value) return false;
                fields[wanted.Key] = field;
            }
            return true;
        }

        public IList<ContractCheck> Contracts(Assembly target, IList<MethodBase> hooks)
        {
            return new[] { new ContractCheck(hooks[0], ProjectBody) };
        }

        public void Revert() { }

        public void Apply(Harmony harmony, Assembly target, IList<MethodBase> hooks)
        {
            harmony.Patch(hooks[0], postfix: new HarmonyMethod(typeof(MeaningfulAmbiencePatch), "ProjectPostfix"));
        }

        static bool loggedOnce;

        static void ProjectPostfix(object __result, object[] __args)
        {
            if (!Rules.AmbienceScalesWithDemand.Enabled || __result == null) return;
            try
            {
                if (!Get<bool>(__result, "Supported") || Get<bool>(__result, "IsOffice")) return;
                var hour = new AmbienceHour
                {
                    VanillaRaw = Get<double>(__result, "VanillaRaw"),
                    VanillaTarget = Get<int>(__result, "VanillaTarget"),
                    EffectiveCapacity = Get<int>(__result, "EffectiveCapacity"),
                    Eligible = Get<bool>(__result, "IsOpen") && Get<bool>(__result, "GameGeneratesCustomers") && !Get<bool>(__result, "NothingBought")
                };
                AmbienceMath.ScaleWithDemand(hour, (double)__args[2], (double)__args[3]);
                Set(__result, "AmbienceRaw", hour.AmbienceRaw);
                Set(__result, "CombinedRaw", hour.CombinedRaw);
                Set(__result, "FinalTarget", hour.FinalTarget);
                Set(__result, "TheoreticalAddedWithoutCapacity", hour.TheoreticalAddedWithoutCapacity);
                Set(__result, "AddedAfterCapacity", hour.AddedAfterCapacity);
                Set(__result, "BlockedByCapacity", hour.BlockedByCapacity);
                Set(__result, "TotalBlockedByCapacity", hour.TotalBlockedByCapacity);
                if (!loggedOnce) Log.Info("Meaningful Ambience: decor now adds a share of the customers the game sends.");
                loggedOnce = true;
            }
            catch (Exception ex) { Log.Error("Meaningful Ambience: projection left as the mod computed it: " + ex); }
        }

        static T Get<T>(object owner, string name) { return (T)fields[name].GetValue(owner); }
        static void Set(object owner, string name, object value) { fields[name].SetValue(owner, value); }
    }
}
