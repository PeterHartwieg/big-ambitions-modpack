using System;
using System.Collections.Generic;
using System.Linq;
using HARTModPack.Core;
using BAModAPI.Services;

namespace HARTModPack
{
    // This publisher belongs to the pack, not the optional/legacy-blocked gameplay.
    internal static class CompanionSave
    {
        static readonly Action callback = Save;
        static Action observed;
        static bool listening;
        internal static void Start()
        {
            if (listening) return;
            listening = true;
            UnityLifecycleProvider.OnUpdate += Ensure;
            Ensure();
        }
        internal static void Stop()
        {
            listening = false;
            UnityLifecycleProvider.OnUpdate -= Ensure;
            GlobalEvents.onSaveGame -= callback;
            observed = null;
            CompanionSnapshot.Publish(SaveGameManager.Current == null ? null : SaveGameManager.Current.modData, null);
        }
        static void Ensure()
        {
            var current = GlobalEvents.onSaveGame;
            if (!listening || ReferenceEquals(current, observed) && current != null) return;
            if (current == null || !current.GetInvocationList().Contains(callback)) GlobalEvents.onSaveGame += callback;
            observed = GlobalEvents.onSaveGame;
        }
        static void Save()
        {
            var game = SaveGameManager.Current;
            if (game == null) return;
            string report = Companion.CaptureV1(game);
            if (report != null && game.modData == null) game.modData = new Dictionary<string, string>();
            CompanionSnapshot.Publish(game.modData, report);
        }
    }
}
