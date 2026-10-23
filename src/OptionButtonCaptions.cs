using System;
using System.Reflection;
using BigAmbitions.Mods;
using BigAmbitions.ModsInternal;
using HARTModPack.Core;
using HarmonyLib;
using Localizor.LanguageChangeEvent;

namespace HARTModPack
{
    // Build 3682 initializes the row label but leaves the button's prefab caption unchanged.
    // Restrict this presentation fix to this assembly's two buttons, including the member checker during stand-down.
    static class OptionButtonCaptions
    {
        static readonly Harmony harmony = new Harmony("hartmodpack.ui.buttoncaptions");
        static bool warned;

        internal static MethodBase ResolveHook()
        {
            return typeof(ModOptionsButtonControl).GetMethod("Initialize", new[] { typeof(ModOption) });
        }

        public static void Start()
        {
            try { harmony.Patch(ResolveHook(), postfix: new HarmonyMethod(typeof(OptionButtonCaptions), "AfterInitialize")); }
            catch (Exception ex) { Warn(ex.Message); }
        }

        public static void Stop() { harmony.UnpatchAll(harmony.Id); warned = false; }

        static void AfterInitialize(ModOptionsButtonControl __instance, ModOption option)
        {
            var button = option as ButtonOption;
            if (button == null || button.OnClick == null || button.OnClick.Method.DeclaringType == null
                || button.OnClick.Method.DeclaringType.Assembly != typeof(HARTModPackMod).Assembly) return;
            string key = ButtonCaptions.KeyFor(button.Label);
            if (key == null) return;
            try
            {
                var caption = __instance.transform.Find("ButtonContainer/Button/Text (TMP)");
                if (caption == null) { Warn("the native button caption was not found"); return; }
                var localized = caption.GetComponent<TextLocalizationComponent>()
                    ?? caption.gameObject.AddComponent<TextLocalizationComponent>();
                localized.Key = key;
            }
            catch (Exception ex) { Warn(ex.Message); }
        }

        static void Warn(string message)
        {
            if (warned) return;
            warned = true;
            Log.Warn("Button captions unavailable: " + message);
        }
    }
}
