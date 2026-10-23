# Changelog

## 1.1.0 (23 October 2026)

- Supports reviewed RLO 0.50.0, retaining 0.49.0. All existing RLO contracts match; extend the main and BizMan presentation allowlists.
- Add Mute RLO night chirping, on by default and in the series preset. Mute only the generated cricket AudioSource; restore its prior mute state when disabled or unloaded without changing RLO settings.
- Fill in the published Thriving Rivals and City Performance Workshop IDs.
- Integrate all HardModePatch 0.5.0 settings, gameplay rules, compatibility fixes and the RLO hash guard into HARTModPack.dll. Preserve rule defaults, preset values and save markers.
- Put the series button, rule toggles and member status on one HART. Mod Pack page.
- Stop the integrated gameplay if HardModePatch.dll is loaded, in either load order. Show one notice to unsubscribe from Mod Pack Patch, remove any ModsLocal copy and restart. Keep the member checker available.
- Remove HardModePatch from required members. Thriving Rivals and City Performance remain separate requirements.
- Package pinned Harmony in Dependencies/; no other mod DLLs are bundled.
- Offline validation and merged-runtime acceptance are recorded in docs/merge-1.1.0.md.

## 1.0.0 (22 October 2026)

- First release. Checks required pack members, turns on subscribed required mods the game left off, and reports missing, failed, duplicate or untested members.
- Uses the approved HART. Thriving Rivals and HART. Mod Pack Patch names.
- Apply HART series settings in Mod Pack Patch to enable Economy Expansion-inspired equipment utilities and office IT on RLO, replacing the flat 2x/3x rent increase. Existing signed leases retain their stored rent.

First release, the Workshop item for the HART modded series pack.

- Finds the 14 required and 3 optional pack mods by their DLL's assembly name, wherever they come from.
- Turns on required members that Steam downloaded but the game left switched off, then asks for one restart. It only ever adds to the game's list of enabled Workshop mods, never removes, turns each item on at most once, leaves optional members alone and never turns on Persistent City.
- Checks the pack once per launch and shows one notice for missing, switched-off, failed or untested members, and for mods loaded from both the Workshop and ModsLocal. A click hides it until the list changes or the pack updates.
- Reminds you once to apply the series settings from HART. Mod Pack Patch's "Apply HART series settings" button.
- Settings > Mods > HART. Mod Pack lists every member's state and version, with a "Check again" button.
- Recognizes Campaign Objectives 1.2.1 after a Mac integration check of the series settings, new game, midnight and save/reload.
- No Harmony.
