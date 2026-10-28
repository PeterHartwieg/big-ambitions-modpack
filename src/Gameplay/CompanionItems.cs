using System;
using System.Linq;
using System.Reflection;
using BigAmbitions.Items;

namespace HardModePatch
{
    internal static class CompanionItems
    {
        // ItemCached and AddressCached are lazy setters. Read their fields without warming live items.
        static readonly FieldInfo cached = typeof(ItemInstance).GetField("_itemCached", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static Item Data(ItemInstance item)
        {
            if (cached == null) throw new MissingFieldException("ItemInstance._itemCached");
            var known = cached.GetValue(item) as Item;
            return known == null ? ItemsGetter.GetByName(item.itemName, true) : known;
        }
        internal static bool Eligible(ItemInstance item, BuildingRegistration building)
        {
            var data = Data(item);
            if (ReferenceEquals(data, null)) throw new InvalidOperationException("item-definition-unavailable");
            foreach (var requirement in data.furnitureRequirements)
            {
                if (ReferenceEquals(requirement, null)) throw new InvalidOperationException("requirement-unavailable");
                var name = requirement.GetType().FullName;
                if (name == "Furniture.Requirements.HasItemTypeAttached" || name == "Furniture.Requirements.IsNotDuplicated")
                {
                    var parent = item;
                    if (!string.IsNullOrEmpty(item.parentId))
                    {
                        ItemInstance found;
                        if (building.itemInstances.TryGetValue(item.parentId, out found) && found != null) parent = found;
                    }
                    if (name.EndsWith("HasItemTypeAttached", StringComparison.Ordinal))
                    {
                        var mask = (ItemType)requirement.GetType().GetField("itemType").GetValue(requirement);
                        bool attached = !ReferenceEquals(parent, item) && (Data(parent).type & mask) != 0;
                        if (!attached) attached = parent.stackedItems.Any(child => (ItemsGetter.GetByName(child.childItemName, true).type & mask) != 0);
                        if (!attached) return false;
                    }
                    else if (parent.stackedItems.Count(child => ItemsGetter.GetByName(child.childItemName, true).type == data.type) > 1) return false;
                }
                else if (name == "Furniture.Requirements.IsPlacedAtHeight" || name == "Furniture.Requirements.IsPlacedInTheEntrance")
                {
                    // Audited pure predicates: position/physics or isSecured only. Other/unknown predicates stay unknown.
                    var copy = (ItemInstance)clone.Invoke(item, null);
                    cached.SetValue(copy, data);
                    if (!requirement.IsRequirementMet(copy)) return false;
                }
                else throw new InvalidOperationException("cafe-requirement-unreviewed:" + name);
            }
            return true;
        }
    }
}
