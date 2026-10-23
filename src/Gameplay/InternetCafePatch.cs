using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Items;
using Buildings.Retail.Simulation;
using HardModePatch.Core;
using HarmonyLib;

namespace HardModePatch
{
    // Internet Cafe (3796860957). Off screen, RetailBusinessSimulator caps an hour's customers by building
    // capacity and by staffed items. The cafe's PCs need no staff, so a one-PC cafe could serve the whole
    // building at $24 a head with no cost of goods. The postfix caps a cafe at two sessions per station:
    // a PC with its own seat under the same desk or table, paired the way the cafe pairs them.
    public sealed class InternetCafePatch : ITargetPatch
    {
        static readonly string[] ComputerNames = { "ba:itemname_gamingcomputer", "ba:itemname_desktopcomputer", "ba:itemname_computer", "ba:itemname_laptop" };
        static Type cafeSimulator;
        static AccessTools.FieldRef<object, BuildingRegistration> registration;

        public TargetInfo Target { get { return Targets.InternetCafe; } }

        public IList<MethodBase> ResolveHooks(Assembly target)
        {
            cafeSimulator = target.GetType("InternetCafe.CafeBusinessSimulator", false);
            var capacity = AccessTools.Method(typeof(RetailBusinessSimulator), "GetMaxCustomerCapacityThisHour", Type.EmptyTypes);
            var field = AccessTools.Field(typeof(RetailBusinessSimulator), "buildingRegistration");
            if (capacity == null || capacity.ReturnType != typeof(int) || field == null || field.FieldType != typeof(BuildingRegistration)) capacity = null;
            else registration = AccessTools.FieldRefAccess<object, BuildingRegistration>(field);
            MethodBase cafeService = cafeSimulator == null || !typeof(RetailBusinessSimulator).IsAssignableFrom(cafeSimulator)
                ? null
                : AccessTools.DeclaredMethod(cafeSimulator, "GetCustomerService");
            return new MethodBase[] { capacity, cafeService };
        }

        // The station count mirrors CafeServiceQuality.Calculate's pairing of PCs and seats.
        static readonly Contract QualityBody = new Contract("CafeServiceQuality.Calculate", "dfc9a44495409b6d");

        public IList<ContractCheck> Contracts(Assembly target, IList<MethodBase> hooks)
        {
            var quality = target.GetType("InternetCafe.CafeServiceQuality", false);
            return new[] { new ContractCheck(quality == null ? null : AccessTools.DeclaredMethod(quality, "Calculate"), QualityBody) };
        }

        public void Revert() { }

        public void Apply(Harmony harmony, Assembly target, IList<MethodBase> hooks)
        {
            harmony.Patch(hooks[0], postfix: new HarmonyMethod(typeof(InternetCafePatch), "CapacityPostfix"));
        }

        static void CapacityPostfix(object __instance, ref int __result)
        {
            if (!Rules.CafePcCapacity.Enabled || __instance == null || __instance.GetType() != cafeSimulator) return;
            try
            {
                var building = registration(__instance);
                if (building == null || building.itemInstances == null) return;
                var furniture = building.itemInstances;
                var pcRoots = new List<string>();
                var seatRoots = new List<string>();
                foreach (var item in furniture.Values)
                {
                    if (item == null || string.IsNullOrEmpty(item.itemName) || ItemHelper.HasAnyMissingRequirements(item)) continue;
                    if (Array.IndexOf(ComputerNames, item.itemName) >= 0) pcRoots.Add(RootId(item, furniture));
                    else if (item.ItemCached != null && (item.ItemCached.type & ItemType.Seat) != 0) seatRoots.Add(RootId(item, furniture));
                }
                __result = CafeMath.Cap(__result, CafeMath.UsableStations(pcRoots, seatRoots));
            }
            catch (Exception ex) { Log.Error("Internet Cafe: capacity left as the game computed it: " + ex); }
        }

        // The top of the item's parent chain, as the cafe finds it (CafeServiceQuality.RootId).
        static string RootId(ItemInstance item, Dictionary<string, ItemInstance> furniture)
        {
            for (int step = 0; step <= furniture.Count; step++)
            {
                if (string.IsNullOrEmpty(item.parentId)) return item.id;
                ItemInstance parent;
                if (!furniture.TryGetValue(item.parentId, out parent) || parent == null) return null;
                item = parent;
            }
            return null;
        }
    }
}
