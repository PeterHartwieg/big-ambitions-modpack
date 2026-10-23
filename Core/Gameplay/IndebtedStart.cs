namespace HardModePatch.Core
{
    // This is a new-game scenario, not a conversion of existing saves. The option is latched by the
    // game's New call; a saved completion marker survives payoff so the debt can never respawn.
    public static class IndebtedStart
    {
        public const float Cash = 551.37f;
        public const float Debt = 75454.35f;
        public const string StateKey = "HardModePatch.IndebtedStart";
        public const string Pending = "pending-v1";
        public const string Complete = "complete-v1";
        public const string Failed = "failed-v1";

        public static bool CanArm(bool selected, bool hardcoreLoaded, bool alreadyMarked)
        {
            return selected && !hardcoreLoaded && !alreadyMarked;
        }

        // Character creation raises game-loaded before the city has created any building registrations.
        public static bool ShouldWaitForCity(int buildingRegistrations)
        {
            return buildingRegistrations <= 0;
        }

        public static bool CanComplete(string state, bool existingDebt, bool bankReady, int interest, int payment)
        {
            return state == Pending && !existingDebt && bankReady && interest >= 0 && payment > 0;
        }
    }
}
