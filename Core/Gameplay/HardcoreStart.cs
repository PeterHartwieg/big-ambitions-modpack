namespace HardModePatch.Core
{
    // Hardcore Ambitions' starting package, as its code sets it (build 3682, mod hash 62ba60cb66dc226c).
    public static class HardcoreStart
    {
        // The cash it leaves after buying the three investment funds. Without the funds the player still
        // starts on this, so the start reads the same on screen.
        public const float StartingCash = 551.37f;
        public const string SaveEnabledFlag = "HardcoreAmbitions.SaveEnabled";
        public const string ReserveInitializedFlag = "HardcoreAmbitions.StartingReserveInitialized";
        public const string NoFundsFlag = "HardModePatch.HardcoreNoFunds";

        // Vehicle and boat prices are Unity assets, so Hardcore Ambitions' changes last until the game quits.
        // They are put back whenever a save that Hardcore Ambitions didn't mark is loaded.
        public static bool ShouldRestoreVanillaPrices(bool snapshotTaken, bool saveIsHardcore)
        {
            return snapshotTaken && !saveIsHardcore;
        }
    }
}
