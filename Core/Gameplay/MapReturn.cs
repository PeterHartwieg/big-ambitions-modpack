using System;

namespace HardModePatch.Core
{
    // A map return has two independent finishes: the game's camera coroutine and Hyperreal's fade.
    // Consume one attempt only after both. Never carry a return across disable/reset/reopen.
    public sealed class MapReturn
    {
        bool pending, cameraReturned;

        public void CameraReturned() { if (pending) cameraReturned = true; }
        public void Reset() { pending = cameraReturned = false; }

        public bool Advance(bool enabled, bool open, bool engaged, float blend, bool enhancementOn)
        {
            if (!enabled || float.IsNaN(blend) || float.IsInfinity(blend)) { Reset(); return false; }
            if (open)
            {
                pending = engaged; // No vanilla-map handoff when Hyperreal's option is off.
                cameraReturned = false;
                return false;
            }
            if (!enhancementOn) { Reset(); return false; }
            if (!pending || !cameraReturned || engaged || blend != 0f) return false;
            Reset();
            return true;
        }
    }
}
