using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardModePatch.Core;
using HarmonyLib;
using UnityEngine;

namespace HardModePatch
{
    // RLO's generated four-second cricket loop only. RLO continues to own its volume, playback and saved settings.
    public static class NightChirpingPatch
    {
        const string ClipName = "RealLifeOverhaul Ambience Crickets";
        const int Cricket = 1;
        static readonly Harmony harmony = new Harmony(GameplayFeatures.HarmonyId + ".nightchirping");
        static readonly SoundMute<AudioSource> mute = new SoundMute<AudioSource>(s => s.mute, (s, value) => { if (s != null) s.mute = value; });
        static FieldInfo layers, singleton;
        static bool attempted, warned;
        public static bool Available { get; private set; }

        // Every reflected member and the patched Tick signature must match. No global AudioSource patch.
        public static IList<MethodBase> ResolveHooks(Assembly target)
        {
            var type = target.GetType("RealLifeOverhaul.Leisure.LeisureSound", false);
            return ResolveType(type);
        }

        internal static IList<MethodBase> ResolveType(Type type)
        {
            layers = type == null ? null : AccessTools.DeclaredField(type, "_layer");
            singleton = type == null ? null : AccessTools.DeclaredField(type, "_instance");
            var cricket = type == null ? null : AccessTools.DeclaredField(type, "Crickets");
            var count = type == null ? null : AccessTools.DeclaredField(type, "Layers");
            var tick = type == null ? null : AccessTools.DeclaredMethod(type, "Tick", new[] { typeof(float) });
            bool shape = layers != null && !layers.IsStatic && layers.FieldType == typeof(AudioSource[])
                && singleton != null && singleton.IsStatic && singleton.FieldType == type
                && cricket != null && cricket.IsLiteral && cricket.FieldType == typeof(int) && (int)cricket.GetRawConstantValue() == Cricket
                && count != null && count.IsLiteral && count.FieldType == typeof(int) && (int)count.GetRawConstantValue() == 4
                && tick != null && !tick.IsStatic && tick.ReturnType == typeof(void);
            return new MethodBase[] { shape ? tick : null };
        }

        public static void Start()
        {
            Rules.MuteRloNightChirping.Changed += Changed;
            AppDomain.CurrentDomain.AssemblyLoad += OnLoad;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) TryPatch(a);
        }

        public static void Stop()
        {
            Rules.MuteRloNightChirping.Changed -= Changed;
            AppDomain.CurrentDomain.AssemblyLoad -= OnLoad;
            harmony.UnpatchAll(harmony.Id);
            mute.Release();
            Available = attempted = warned = false;
            layers = singleton = null;
        }

        static void OnLoad(object sender, AssemblyLoadEventArgs e) { TryPatch(e.LoadedAssembly); }
        static void TryPatch(Assembly a)
        {
            if (attempted || a == null || a.GetName().Name != Targets.RealLifeOverhaul.AssemblyName) return;
            attempted = true;
            if (!RloGate.Accepts(a)) return; // The shared gate logs once, including this feature.
            try
            {
                if (!TargetGate.Reviewed(Patcher.HashOf(a), new[] { "4f82b4f88f919178", "f339319c9ec1ad65", "14aa86b20dd354bc" }))
                { Fail("this sound adapter is reviewed for RLO 0.49.0 and 0.50.0"); return; }
                var hooks = ResolveHooks(a);
                if (hooks.Any(h => h == null)) { Fail("the exact RLO cricket target did not resolve"); return; }
                harmony.Patch(hooks[0], postfix: new HarmonyMethod(typeof(NightChirpingPatch), "Postfix"));
                Available = true;
                Changed(Rules.MuteRloNightChirping.Enabled);
            }
            catch (Exception e) { Fail(e.Message); }
        }

        static void Changed(bool value)
        {
            if (!Available) return;
            try { Apply(singleton.GetValue(null)); }
            catch (Exception e) { Fail(e.Message); }
        }

        static void Postfix(object __instance)
        {
            if (!Available) return;
            try { Apply(__instance); }
            catch (Exception e) { Fail(e.Message); }
        }

        static void Apply(object instance)
        {
            if (!Rules.MuteRloNightChirping.Enabled) { mute.Release(); return; }
            var sources = instance == null ? null : (AudioSource[])layers.GetValue(instance);
            if (sources == null) { mute.Release(); return; }
            if (sources.Length != 4) { Fail("the RLO sound layer count changed"); return; }
            var source = sources[Cricket];
            if (source == null) { mute.Release(); return; } // RLO builds one layer per tick.
            if (source.clip == null || source.clip.name != ClipName) { Fail("the RLO cricket clip changed"); return; }
            mute.Apply(source, true);
        }

        static void Fail(string reason)
        {
            Available = false;
            harmony.UnpatchAll(harmony.Id);
            mute.Release();
            if (!warned) { warned = true; Log.Warn("Mute RLO night chirping unavailable: " + reason + "; RLO's sounds stay unchanged."); }
        }
    }
}
