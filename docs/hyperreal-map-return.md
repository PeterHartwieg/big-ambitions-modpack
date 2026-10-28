# Hyperreal darkness after the map, 19 October 2026

> Imported design and historical evidence from HardModePatch 0.5.0. Its implementation now ships in HART. Mod Pack 1.1.1 under `Core/Gameplay` and `src/Gameplay`. Original version, namespace and save-key references below are historical; current migration and acceptance status is in [merge-1.1.0.md](merge-1.1.0.md). City Performance retains performance ownership.

Current ownership, 22 October: HMP 0.4.2 retains the map-return workaround and its exact binary guards, with five remaining method contracts. Lamp discovery now belongs to City Performance. The 0.4.1 cache and 49-check results below are historical diagnosis evidence. Current regression coverage is `tools/CompatibilityCheck`; see [the paired handoff](performance-ownership.md).

The most plausible explanation is Hyperreal's return from Fixed exposure to AutomaticHistogram without invalidating HDRP exposure history. **Confidence: moderate; this is a diagnosis to test, not a proven root cause.** The log does not mark the black screen or record GPU exposure/history at the time. Two independent code reviews found no stronger deterministic fault.

HMP 0.4.1's light-discovery patch is **ruled out for the proposed map-disable/stale-list mechanism**. This is a strong code conclusion, not an in-game HMP-off comparison. Its optimization is unchanged by this fix.

## Reviewed inputs

- Supplied `PolyModsHyperreal.dll`, SHA-256 prefix `7d1bd589947558e2`, Workshop 3803756901, and the supplied decompile under `dark/hyperreal-src/PolyModsHyperreal/`.
- Supplied `dark/Player.log`; references below use LF-based line numbers (`nl -ba`/`rg`), not Python's broader `splitlines()` numbering.
- Game decompile `decompiled-1.0-build-2026-09-23/BigAmbitions/` and exact `build/game-dlls` binaries. Installed HDRP was additionally inspected into ignored `build/HDCamera.cs` and `build/HDRenderPipeline.cs`; no decompiled code is committed.
- The clone initially contained committed 0.1.0 source. The local origin's uncommitted 0.4.1 source was imported as a separate baseline. Its clean build reproduced installed SHA-256 `7a5857b0ff51d8d9c70c80daacca5ec3eb9982b03ef15ca96ae89fee53fc1aa8` exactly.

## Ranked causes

1. **Hyperreal/HDRP exposure-history handoff: most likely, moderate confidence.** `MapMode.Update` starts closing when `CityMap.IsOpen` becomes false and moves `Blend` to zero over 0.5 seconds (`MapMode.cs:117–123`). `EnhancementVolume.Apply` forces Fixed exposure while blend is positive (`EnhancementVolume.cs:375–379`), then resumes AutomaticHistogram (`:381–400`). No exposure-history reset accompanies that transition. In the installed HDRP, `resetPostProcessingHistory` forces immediate adaptation (`build/HDRenderPipeline.cs:16084`), discards previous exposure (`:16150`) and applies the new exposure that frame (`:16281`). This is a targeted reset mechanism; its absence alone does not explain why ordinary progressive adaptation would sometimes stay black.

2. **Camera-dependent metering or another camera mod: plausible alternative/contributor.** Hyperreal changes view classification with height, pitch and focus distance (`ViewClassifier.cs:111–145`), then interpolates exposure compensation, limits and histogram percentiles (`EnhancementVolume.cs:369–400`). Zoom can therefore change brightness directly. Defaults also reduce moon intensity to 6% and night sky to 10% (`HRConfig.cs:143–145`). This explains the zoom clue without proving exposure-history corruption. The workaround preserves these values; another camera interaction remains possible.

3. **Persistent map-volume capture, wrong weight, or Fixed exposure left on: unlikely.** The native map volume disables synchronously with `IsOpen=false` (`CityMap.cs:211–212`). Its logged profile contains only shadow settings and color adjustments (`Player.log:1696–1703`), no Exposure override. Hyperreal refreshes a separate baseline using the original volume mask every 0.2 seconds, every frame during fades (`GameBaseline.cs:70–87`; `HRRuntime.cs:278–286`). It rewrites its volume weight/enabled state every frame (`EnhancementVolume.cs:303–308`) and restores automatic mode at zero blend (`:381–400`). Camera mask restoration/reapplication is present (`CameraFeatures.cs:114–163`). A stale value during a fade is possible; permanent capture through this path is not supported by the code.

4. **Sun/moon, lamp boosts or rain-sun not restored: unlikely.** The native environment resumes updating once the map closes (`TimeOfDayController.cs:490–513`), rewriting sun/moon intensity (`:624–631`). Hyperreal tracks external writes before scaling (`LightFeatures.cs:230–240`); volume, lights, lamps and rain are reapplied each LateUpdate (`HRRuntime.cs:278–289`). Rain's wanted state is recomputed and its off path disables the temporary sun (`RainSun.cs:31–60`). The later `Rain overcast sun created` log is not evidence of a failed map restore.

5. **HMP's lamp-discovery cache: very unlikely; stale-on-disable theory ruled out.** `HyperrealPatch.ScanTranspiler` substitutes exactly the author's include-inactive search and preserves the rest of the scan. `FindLights` retains existing component identities; `LightEnabled` adds scene lights and `LightDestroyed` removes destroyed ones. Disabling a lamp does not evict it. Hyperreal's separate persistent lamp dictionary already holds discovered lamps (`StreetLights.cs:161–179`) and restores/updates them independently of discovery (`:56–109`). Neither the HMP cache nor its lifecycle callbacks writes exposure, volume state or intensity. Offline checks verify the one-call substitution and original-body preservation.

## What the session log establishes

There are twelve map closes, including six outdoor evening/night returns and one indoor evening return. Useful outdoor candidates are `Player.log:3576` (last RLO hour 20, captured 9.69 EV), `:4299` (21, 8.63 EV), `:5329` and `:5376` (18, 9.60/8.95 EV), `:7108` (19, 9.61 EV), and `:7248` (20, 8.91 EV). Hours are nearby RLO observations, not timestamps of the missing black frame. The 10 EV close at `:5515` is an indoor return around hour 19, consistent with vanilla indoor exposure.

The `exposure back to` lines announce the captured target at the start of the fade; they do not measure successful restoration. Cache statistics around the late closes remain approximately 3,837 lights (`:7103`, `:7184`, `:7271`). No lamp-list collapse or associated Hyperreal LateUpdate exception is recorded. An HMP-off rendering comparison has not been performed.

## Runtime workaround and verification

`hyperreal_map_return_fix`, default on, extends `src/HyperrealPatch.cs`. It records the camera passed to `EnhancementVolume.Ensure`, waits for the game's completed camera-return event **and** zero blend, then requests one HDRP postprocessing-history reset after the author's `Apply` selected AutomaticHistogram. It skips indoors, disabled enhancements, fixed exposure and missing cameras. It changes no exposure numbers or light intensities. HDRP shares this reset flag with other postprocessing histories; a brief temporal settling frame is possible.

The game's `onCityMapClosed` follows `RestartTime`, including the camera return coroutine (`CityMap.cs:322–324`, `:406–421`). Merely watching `IsOpen=false` would reset too early. `GlobalEvents.Init` clears that event (`GlobalEvents.cs:99`), so a fingerprinted postfix restores the subscription without duplicates. Scene changes, resets and option toggles cancel pending work. Unknown/unreadable binaries are rejected before installation. Reviewed hashes were already present in `Core/Targets.cs` and were not expanded.

`tools/build.sh` and `tools/check-hooks.sh build/hyperreal-check` pass: Hyperreal 8/8 members and contracts, Harmony application; Bakery also passes. Other target mods were absent from this check folder. The 49 PerformanceCheck checks include engine-stubbed execution of the actual postfix, Fixed/AutomaticHistogram/indoor/off guards, both completion orders, one-shot behavior, scene/reset/reload cleanup and unknown identity rejection. ScenarioCheck passes 38 checks with RLO 0.38.0. `dotnet test` compiled but VSTest was blocked from binding its local communication socket; all 103 Core Fact/InlineData cases passed via an in-process reflection runner. No Unity renderer, real save or GPU recovery is exercised offline.

## Disposable-save playtest

1. Install the new `build/HardModePatch` package only for a disposable test save. Leave `hyperreal_light_discovery` on. Confirm the reviewed Hyperreal activation and map-return workaround lines in Player.log.
2. At roughly 18:00–23:00, stand outdoors. Open the city map for several seconds and close it without zooming; wait a few seconds. Repeat from low and high zoom, including a quick reopen during the closing fade, clear/rain conditions, and a paused game.
3. Expect one `Hyperreal map return: reset exposure history after camera return and fade (AutomaticHistogram).` per eligible outdoor return. Confirm street lighting/brightness recovers without zoom, and no repeated resets occur while standing still. Check indoor return and Hyperreal auto-exposure-off produce no action line.
4. Load/reload the disposable save in the same session and repeat. Compare the same camera/time conditions with only `hyperreal_map_return_fix` off, then on. Option toggles alone must not reset ordinary gameplay.
5. If darkness recurs, note game time and the nearest map-return log, capture a screenshot and Hyperreal's diagnostic report before zooming, then again after recovery. Record auto-exposure mode, measured EV, StreetFactor, camera and active volumes. This distinguishes history failure from metering/camera/lighting faults.

No game installation or save was changed here. The workaround's visual effect and the reported bug's resolution remain unverified. Unity's [Exposure documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Override-Exposure.html) describes view-dependent automatic metering and progressive versus immediate adaptation; the installed DLL was used for the exact reset behavior.
