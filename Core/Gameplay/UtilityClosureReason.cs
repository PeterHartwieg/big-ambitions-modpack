namespace HardModePatch.Core
{
    // Only describe a reason RLO has recorded for a utility closure it still owns.
    public static class UtilityClosureReason
    {
        public static string Key(string reason, bool utilityClosure, long now, long until)
        {
            if (!utilityClosure || until <= now) return null;
            switch (reason)
            {
                case "reallife_util_closed_net_install": return "hardmode_closed_net_install";
                case "reallife_util_closed_net_none": return "hardmode_closed_net_none";
                case "reallife_util_closed_net_outage": return "hardmode_closed_net_outage";
                case "reallife_util_closed_connecting": return "hardmode_closed_connecting";
                case "reallife_util_closed_cut": return "hardmode_closed_cut";
                case "reallife_util_closed_apply": return "hardmode_closed_apply";
                default: return null;
            }
        }
    }
}
