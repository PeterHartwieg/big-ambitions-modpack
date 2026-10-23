using System;

namespace HardModePatch.Core
{
    // Rent is the lever (Peter, 18 October 2026): higher daily rent for the player, deposits keep the game's
    // usual number of days, so they rise with the rent. Flats at twice the market rent make a hostel bed the cheap
    // start; shops at three times push the first real location past the food cart and truck. Rivals' estimated
    // income keeps the game's rent, so the leaderboard and rival survival don't move.
    public static class RentRules
    {
        public const float Residential = 2.0f;
        public const float Commercial = 3.0f;
        public const string ResidentialType = "ba:buildingtype_residential";

        public static float Multiplier(string buildingType)
        {
            return buildingType == ResidentialType ? Residential : Commercial;
        }

        // The game rounds market rent up to whole dollars; so does the scaled rent.
        public static int Scale(int marketRent, string buildingType)
        {
            if (marketRent <= 0) return marketRent;
            return (int)Math.Ceiling(marketRent * (double)Multiplier(buildingType));
        }

        // A rival business's weekly estimate keeps the game's rent. The player's own businesses use the scaled rent,
        // which is what they pay.
        public static bool KeepGameRentInEstimate(bool rentedByPlayer, bool ownedByPlayer)
        {
            return !rentedByPlayer && !ownedByPlayer;
        }
    }
}
