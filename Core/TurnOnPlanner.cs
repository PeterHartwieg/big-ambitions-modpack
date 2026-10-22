using System.Collections.Generic;
using System.Linq;

namespace HARTModPack.Core
{
    public sealed class TurnOnPlan
    {
        public List<ulong> ToAdd = new List<ulong>();
        public List<string> Notes = new List<string>();
    }

    // Decides which subscribed Workshop items to add to the game's enabled list (steam_mod_manifest.txt).
    // Only adds, never removes. Turns on required members only, and each Workshop item at most once:
    // a member the player switches off again afterwards stays off and is reported instead.
    public static class TurnOnPlanner
    {
        public static TurnOnPlan Plan(IEnumerable<SubscribedItem> subscribed, IEnumerable<string> localAssemblies, ICollection<ulong> turnedOnBefore)
        {
            var plan = new TurnOnPlan();
            var items = subscribed.Where(s => s != null && !string.IsNullOrEmpty(s.AssemblyName)).ToList();
            var local = new HashSet<string>(localAssemblies.Where(a => !string.IsNullOrEmpty(a)), System.StringComparer.OrdinalIgnoreCase);
            foreach (var group in items.GroupBy(s => s.AssemblyName, System.StringComparer.OrdinalIgnoreCase))
            {
                string name = group.Key;
                if (Pack.IsExcluded(name)) continue;
                var member = Pack.Find(name);
                if (member == null || !member.Required) continue;
                if (group.Any(s => s.Enabled)) continue;
                if (local.Contains(name))
                {
                    plan.Notes.Add(member.Title + " runs from ModsLocal, so its Workshop copy stays off.");
                    continue;
                }
                var candidates = group.Where(s => !turnedOnBefore.Contains(s.Id)).ToList();
                if (candidates.Count == 0)
                {
                    plan.Notes.Add(member.Title + " was turned on once before and is off again; left off.");
                    continue;
                }
                if (candidates.Count > 1)
                {
                    plan.Notes.Add("More than one subscribed Workshop item carries " + name + ".dll (" + string.Join(", ", candidates.Select(c => c.Id.ToString()).ToArray()) + "); left off.");
                    continue;
                }
                plan.ToAdd.Add(candidates[0].Id);
            }
            return plan;
        }
    }
}
