using System;

namespace HardModePatch.Core
{
    // Meaningful Ambience's projection for one retail hour, the fields this patch rewrites.
    public sealed class AmbienceHour
    {
        public double VanillaRaw;       // customers the game would send this hour, before capacity
        public int VanillaTarget;       // the same, rounded up and capped at capacity
        public int EffectiveCapacity;
        public bool Eligible;           // open, the game sends customers, and something sold recently
        public double AmbienceRaw;
        public double CombinedRaw;
        public int FinalTarget;
        public int TheoreticalAddedWithoutCapacity;
        public int AddedAfterCapacity;
        public int BlockedByCapacity;
        public int TotalBlockedByCapacity;
    }

    public static class AmbienceMath
    {
        // The mod adds capacity × score × bonus customers every open hour, whatever the difficulty's
        // customer promotion, the marketing and the hour say. Here the bonus scales the game's own demand
        // instead: a perfect score at the 10% preset brings 10% more customers. The rest follows the mod's
        // own arithmetic (GameBridge.ProjectRetail), so its ledger and injection stay consistent.
        public static void ScaleWithDemand(AmbienceHour hour, double score01, double maxBonus01)
        {
            double score = Math.Max(0.0, Math.Min(1.0, score01));
            hour.AmbienceRaw = hour.Eligible ? Math.Max(0.0, hour.VanillaRaw) * score * Math.Max(0.0, maxBonus01) : 0.0;
            hour.CombinedRaw = Math.Max(0.0, hour.VanillaRaw + hour.AmbienceRaw);
            int vanilla = CeilingToInt(hour.VanillaRaw);
            int combined = CeilingToInt(hour.CombinedRaw);
            int capacity = Math.Max(0, hour.EffectiveCapacity);
            hour.TheoreticalAddedWithoutCapacity = Math.Max(0, combined - vanilla);
            hour.FinalTarget = capacity > 0 ? CeilingToInt(Math.Min(hour.CombinedRaw, capacity)) : 0;
            hour.AddedAfterCapacity = Math.Max(0, hour.FinalTarget - hour.VanillaTarget);
            hour.BlockedByCapacity = Math.Max(0, hour.TheoreticalAddedWithoutCapacity - hour.AddedAfterCapacity);
            hour.TotalBlockedByCapacity = Math.Max(0, combined - hour.FinalTarget);
        }

        // The mod's rounding: up, ignoring floating-point dust.
        public static int CeilingToInt(double value)
        {
            return value <= 0.0 ? 0 : (int)Math.Ceiling(value - 1E-07);
        }
    }
}
