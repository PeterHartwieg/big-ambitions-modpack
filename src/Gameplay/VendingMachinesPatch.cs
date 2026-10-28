using System;
using System.Collections.Generic;
using System.Reflection;
using HardModePatch.Core;
using HarmonyLib;
using Controllers;
using UI.InteriorDesigner;

namespace HardModePatch
{
    // Vending Machines (3812054455) turns on the game's developer flag BuildingManager
    // .isBuildingTemporarilyEditable while the player holds a machine in a rival's building, so the machine
    // can be placed there. The same flag lets the player pick up and move any item in that building, and
    // offers "change text" on a rival's sign, which opens the Interior Designer with its move and sell
    // tools. While the mod has the flag up (RivalPlacement._raised) in a rival's building, only its own
    // items can be picked up, a sign's change-text action is cancelled before it touches the designer,
    // and the Interior Designer doesn't open.
    public sealed class VendingMachinesPatch : ITargetPatch
    {
        const string OwnItemPrefix = "vendingmachines:";
        static FieldInfo raised;

        public TargetInfo Target { get { return Targets.VendingMachines; } }

        public IList<MethodBase> ResolveHooks(Assembly target)
        {
            var placement = target.GetType("VendingMachines.Game.RivalPlacement", false);
            raised = placement == null ? null : AccessTools.Field(placement, "_raised");
            bool ready = raised != null && raised.IsStatic && raised.FieldType == typeof(bool);
            return new MethodBase[]
            {
                ready ? AccessTools.Method(typeof(ItemController), "SecondaryInteract", Type.EmptyTypes) : null,
                ready ? AccessTools.Method(typeof(ItemController), "TryToGrabItem", Type.EmptyTypes) : null,
                ready ? AccessTools.DeclaredMethod(typeof(InteriorDesignerUI), "Show", Type.EmptyTypes) : null,
                ready ? AccessTools.DeclaredMethod(typeof(ItemWithTextController), "OpenChangeTextOverlayInInteriorDesigner", Type.EmptyTypes) : null
            };
        }

        // RivalPlacement.Tick decides when _raised is set; the lock relies on what it means.
        static readonly Contract TickBody = new Contract("RivalPlacement.Tick", "c0cb4fbaec857754");

        public IList<ContractCheck> Contracts(Assembly target, IList<MethodBase> hooks)
        {
            var placement = target.GetType("VendingMachines.Game.RivalPlacement", false);
            return new[] { new ContractCheck(placement == null ? null : AccessTools.DeclaredMethod(placement, "Tick", Type.EmptyTypes), TickBody) };
        }

        public void Revert() { }

        public void Apply(Harmony harmony, Assembly target, IList<MethodBase> hooks)
        {
            harmony.Patch(hooks[0], prefix: new HarmonyMethod(typeof(VendingMachinesPatch), "InteractPrefix"));
            harmony.Patch(hooks[1], prefix: new HarmonyMethod(typeof(VendingMachinesPatch), "GrabPrefix"));
            harmony.Patch(hooks[2], prefix: new HarmonyMethod(typeof(VendingMachinesPatch), "DesignerPrefix"));
            harmony.Patch(hooks[3], prefix: new HarmonyMethod(typeof(VendingMachinesPatch), "SignTextPrefix"));
        }

        // The sign action disables designer tools, opens the designer and runs a tool regardless of whether
        // it opened, so it is cancelled as a whole.
        static bool SignTextPrefix()
        {
            return !InRivalPlacement();
        }

        static bool DesignerPrefix()
        {
            if (!InRivalPlacement()) return true;
            Log.Info("Vending Machines: the Interior Designer stays closed while placing a machine in a rival's building.");
            return false;
        }

        static bool InteractPrefix(ItemController __instance)
        {
            return !Locked(__instance);
        }

        static bool GrabPrefix(ItemController __instance, ref bool __result)
        {
            if (!Locked(__instance)) return true;
            __result = false;
            return false;
        }

        static bool Locked(ItemController item)
        {
            if (item == null || !InRivalPlacement()) return false;
            return item.itemName == null || !item.itemName.StartsWith(OwnItemPrefix, StringComparison.Ordinal);
        }

        static bool InRivalPlacement()
        {
            try
            {
                if (!Rules.VendingProtectRivalFurniture.Enabled) return false;
                if (!BuildingManager.isBuildingTemporarilyEditable || !(bool)raised.GetValue(null)) return false;
                if (!BuildingManager.IsInsideBuilding) return false;
                var manager = InstanceBehavior<BuildingManager>.Instance;
                var building = manager == null ? null : manager.buildingRegistration;
                return building != null && !building.RentedByPlayer;
            }
            catch (Exception) { return false; }
        }
    }
}
