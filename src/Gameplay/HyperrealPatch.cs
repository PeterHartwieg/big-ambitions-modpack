using System;
using System.Collections.Generic;
using System.Reflection;
using HardModePatch.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HardModePatch
{
    public sealed class HyperrealPatch : ITargetPatch
    {
        static readonly MapReturn mapReturn = new MapReturn();
        static FieldInfo mapOpen, mapEngaged, mapBlend, indoors, exposure, exposureMode, modeValue, resetHistory;
        static MethodInfo getHdCamera;
        static Camera returnCamera;
        static bool mapReturnFailed;

        public TargetInfo Target { get { return Targets.Hyperreal; } }

        public IList<MethodBase> ResolveHooks(Assembly target)
        {
            var volume = target.GetType("PolyModsHyperreal.EnhancementVolume", false);
            var environment = target.GetType("PolyModsHyperreal.GameEnvironment", false);
            var config = target.GetType("PolyModsHyperreal.HRConfig", false);
            var map = target.GetType("PolyModsHyperreal.MapMode", false);
            var hdCamera = AccessTools.TypeByName("UnityEngine.Rendering.HighDefinition.HDCamera");
            var apply = volume == null || environment == null || config == null || map == null ? null
                : AccessTools.DeclaredMethod(volume, "Apply", new[] { typeof(bool), environment, config, map });
            var reset = map == null ? null : AccessTools.DeclaredMethod(map, "Reset", Type.EmptyTypes);
            mapOpen = CheckedField(map, "<Open>k__BackingField", typeof(bool));
            mapEngaged = CheckedField(map, "<Engaged>k__BackingField", typeof(bool));
            mapBlend = CheckedField(map, "<Blend>k__BackingField", typeof(float));
            indoors = CheckedField(environment, "Indoors", typeof(bool));
            exposure = volume == null ? null : AccessTools.DeclaredField(volume, "_exposure");
            exposureMode = exposure == null ? null : AccessTools.Field(exposure.FieldType, "mode");
            modeValue = exposureMode == null ? null : AccessTools.Field(exposureMode.FieldType, "m_Value");
            resetHistory = CheckedField(hdCamera, "resetPostProcessingHistory", typeof(bool));
            getHdCamera = hdCamera == null ? null : AccessTools.DeclaredMethod(hdCamera, "GetOrCreate", new[] { typeof(Camera), typeof(int) });
            bool returnMembers = mapOpen != null && mapEngaged != null && mapBlend != null && indoors != null
                && exposure != null && exposure.FieldType.FullName == "UnityEngine.Rendering.HighDefinition.Exposure"
                && modeValue != null && modeValue.FieldType.FullName == "UnityEngine.Rendering.HighDefinition.ExposureMode"
                && resetHistory != null && getHdCamera != null && getHdCamera.ReturnType == hdCamera;
            bool reviewed = BinaryCompatibility.Reviewed(target, Targets.Hyperreal.VerifiedHashes[0])
                && BinaryCompatibility.Reviewed(typeof(HelpSystem).Assembly, BinaryCompatibility.GameHash)
                && BinaryCompatibility.Reviewed(typeof(UnityEngine.Object).Assembly, BinaryCompatibility.UnityCoreHash)
                && hdCamera != null && BinaryCompatibility.Reviewed(hdCamera.Assembly, BinaryCompatibility.HdrpHash);
            if (!reviewed) Log.Warn("Hyperreal: unreviewed or unreadable game/mod/HDRP binary; map-return workaround inactive.");
            return new MethodBase[] { reviewed && returnMembers && apply != null && apply.ReturnType == typeof(void) ? apply : null,
                reset != null && reset.ReturnType == typeof(void) ? reset : null,
                volume == null || hdCamera == null ? null : AccessTools.DeclaredMethod(volume, "Ensure",
                    new[] { typeof(Camera), hdCamera.Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData", true) }),
                getHdCamera,
                AccessTools.DeclaredMethod(typeof(GlobalEvents), "Init", Type.EmptyTypes) };
        }

        public IList<ContractCheck> Contracts(Assembly target, IList<MethodBase> hooks)
        {
            return new[] {
                new ContractCheck(hooks[0], new Contract("Hyperreal EnhancementVolume.Apply", "b3c3d17b93a11bc1")),
                new ContractCheck(hooks[1], new Contract("Hyperreal MapMode.Reset", "4ce85b86cc606688")),
                new ContractCheck(hooks[2], new Contract("Hyperreal EnhancementVolume.Ensure", "095bef24db378bf1")),
                new ContractCheck(hooks[3], new Contract("HDRP HDCamera.GetOrCreate", "a13081bd19bedb27")),
                new ContractCheck(hooks[4], new Contract("GlobalEvents.Init", "e8e1603ce5118fed")) };
        }

        public void Apply(Harmony harmony, Assembly target, IList<MethodBase> hooks)
        {
            harmony.Patch(hooks[0], postfix: new HarmonyMethod(typeof(HyperrealPatch), "MapReturnPostfix"));
            harmony.Patch(hooks[1], postfix: new HarmonyMethod(typeof(HyperrealPatch), "ResetMapReturn"));
            harmony.Patch(hooks[2], postfix: new HarmonyMethod(typeof(HyperrealPatch), "EnsureCamera"));
            harmony.Patch(hooks[4], postfix: new HarmonyMethod(typeof(HyperrealPatch), "EventsInitialized"));
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
            Rules.HyperrealMapReturnFix.Changed += MapReturnOptionChanged;
            EventsInitialized();
            Log.Info("Hyperreal map return: exposure-history workaround active. Option=" + Rules.HyperrealMapReturnFix.Enabled + ".");
        }

        public void Revert()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            Rules.HyperrealMapReturnFix.Changed -= MapReturnOptionChanged;
            GlobalEvents.onCityMapClosed -= CameraReturned;
            ResetMapReturn();
            mapReturnFailed = false;
        }

        static FieldInfo CheckedField(Type type, string name, Type expected)
        {
            var field = type == null ? null : AccessTools.DeclaredField(type, name);
            return field != null && field.FieldType == expected ? field : null;
        }

        // Runs after the author's Apply has chosen the final exposure mode. IsOpen=false starts the
        // return, but onCityMapClosed follows the camera coroutine; neither alone means the fade ended.
        static void MapReturnPostfix(object __instance, bool on, object env, object map)
        {
            if (mapReturnFailed || map == null) return;
            try
            {
                if (!mapReturn.Advance(Rules.HyperrealMapReturnFix.Enabled, (bool)mapOpen.GetValue(map),
                    (bool)mapEngaged.GetValue(map), (float)mapBlend.GetValue(map), on)) return;
                if ((bool)indoors.GetValue(env)) return;
                var currentExposure = exposure.GetValue(__instance) as UnityEngine.Object;
                if (currentExposure == null || Convert.ToInt32(modeValue.GetValue(exposureMode.GetValue(currentExposure))) != 4) return;
                var camera = returnCamera; // Exactly the camera Hyperreal supplied this frame, not the game's cached Camera.main.
                if (camera == null) return;
                var hd = getHdCamera.Invoke(null, new object[] { camera, 0 });
                resetHistory.SetValue(hd, true);
                Log.Info("Hyperreal map return: reset exposure history after camera return and fade (AutomaticHistogram).");
            }
            catch (Exception ex)
            {
                mapReturnFailed = true;
                ResetMapReturn();
                Log.Warn("Hyperreal map return: workaround disabled for this session: " + ex.Message);
            }
        }

        static void CameraReturned() { mapReturn.CameraReturned(); }
        static void EnsureCamera(Camera cam) { returnCamera = cam; }
        static void ResetMapReturn() { mapReturn.Reset(); returnCamera = null; }
        static void MapReturnOptionChanged(bool enabled) { ResetMapReturn(); }
        static void EventsInitialized()
        {
            ResetMapReturn();
            // The game clears its event delegates on load and on returning to the menu.
            GlobalEvents.onCityMapClosed -= CameraReturned;
            GlobalEvents.onCityMapClosed += CameraReturned;
        }

        static void SceneLoaded(Scene scene, LoadSceneMode mode) { ResetMapReturn(); }
        static void SceneUnloaded(Scene scene) { ResetMapReturn(); }
    }
}
