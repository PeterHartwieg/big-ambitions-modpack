using System;
namespace HARTModPack
{
    /// <summary>Read-only, main-thread public companion contract. Never publishes to the live save.</summary>
    public static class Companion
    {
        public static string CaptureV1(GameInstance game)
        {
            try { return HardModePatch.CompanionGame.Capture(game); }
            catch (Exception) { return null; }
        }
    }
}
