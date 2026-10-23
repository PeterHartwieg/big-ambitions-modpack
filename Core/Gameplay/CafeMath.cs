using System;
using System.Collections.Generic;
using System.Linq;

namespace HardModePatch.Core
{
    public static class CafeMath
    {
        // A session lasts 30 game minutes, so one PC seats two customers an hour.
        public const int SessionsPerPcPerHour = 2;

        // Off screen the game caps customers by building capacity and staffed items only; the cafe's PCs
        // need no staff, so they never lowered the cap. This applies the PC limit the cafe has on screen.
        public static int Cap(int gameCap, int usableStations)
        {
            return Math.Max(0, Math.Min(gameCap, Math.Max(0, usableStations) * SessionsPerPcPerHour));
        }

        // A station is a PC with its own seat under the same furniture root (desk or table), as the cafe
        // pairs them on screen (CafePcStation) and for service quality (CafeServiceQuality). Two PCs on
        // one desk with one chair are one station. Items whose root can't be resolved count for nothing.
        public static int UsableStations(IEnumerable<string> pcRoots, IEnumerable<string> seatRoots)
        {
            var seats = seatRoots.Where(r => r != null).GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
            return pcRoots.Where(r => r != null).GroupBy(r => r).Sum(g =>
            {
                int count;
                return seats.TryGetValue(g.Key, out count) ? Math.Min(g.Count(), count) : 0;
            });
        }
    }
}
