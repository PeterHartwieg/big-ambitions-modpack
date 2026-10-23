# Mod Pack merge, 1.1.0

All gameplay from HardModePatch 0.5.0 at `36e8eec` is compiled into HARTModPack.dll. The original internal `HardModePatch` namespaces, rule IDs, locale keys, scenario save marker and diploma hold records are retained. There is one registered mod entry and one Settings > Mods > HART. Mod Pack page. No HardModePatch.dll dependency is shipped or required.

## Duplicate DLL policy

The merged gameplay yields to any loaded assembly whose simple name is `HardModePatch`, regardless of version or source. The guard subscribes to AssemblyLoad before scanning existing assemblies. If the old DLL loads later, the pack synchronously stops its gameplay hooks and subscriptions before the loader can invoke the old mod entry. The pack uses the separate Harmony owner prefix `hartmodpack.gameplay`; it never unpatches the legacy DLL or another mod. The member checker remains active.

Refusal lasts until process restart, including after the old mod is disabled: .NET/Mono keeps the assembly loaded. One notification per launch says to unsubscribe from HART. Mod Pack Patch, remove any HardModePatch folder from ModsLocal, and restart. A persistent explanation replaces the gameplay controls on the pack page. The pack never removes files, unsubscribes, or disables another mod itself. Notification and page work runs on the Unity thread.

A legacy DLL introduced into an already-running session can inherit data the pack has already initialized. Existing factory catalogue entries are retained to preserve save records, as in 0.5.0; the old patch may refuse its factory injection until restart. Removing the old DLL and restarting is required, not a hot migration. The old patch's own separate settings page can remain while it is loaded; the merged pack registers only its own page and does not alter another mod's UI.

## Defaults and settings

All 22 rule defaults and all 74 series values are unchanged. Optional house rules remain off until enabled or selected by the series button. The custom-game prefill and compatibility fixes remain on by default. The preset explicitly keeps `rent_rebalance=0` and enables the indebted start, happiness, full owner-job pay, factory diplomas, equipment utilities and office IT. The RLO hash and shape guards are unchanged.

The preset now locates its own controls by assembly name `HARTModPack`. New preferences belong to the Mod Pack's actual mod ID. Old patch preferences are left intact; Workshop IDs and local paths are not guessed. After removing the patch, apply the series preset at the main menu, reapply any personal rule choices on the Mod Pack page, and restart. Saved scenario completion and held diploma records continue under their original keys, preventing a second debt start or a lost hold solely because of this move.

## Ownership and packaging

Thriving Rivals 0.2.0 and City Performance 0.5.0 remain separate required Workshop members, alongside the other 11 required members. The three optional tools stay optional. Persistent City stays excluded. The release manifest is `modpack.json`; the series workspace mirrors it under `packRelease` in `mods/modpack/modpack.json`. No target-mod DLL is bundled. Harmony 2.4.2 is checksum pinned under Dependencies with its licence.

The gameplay sources and existing 0.5.0 tests/harnesses were copied into this repository. Build and test commands resolve game DLLs through `BA_GAME_DLLS` (defaulting to the existing local cache in the old patch checkout). This is a build-time reference cache, not a runtime dependency on the patch. Source provenance and allowed integration changes are checked by `tools/check-merge.py`.

## Verification

Run `tools/build.sh`, `tools/check-api.sh` and `tools/check-all.sh` with the reviewed targets, a deliberately unreviewed RLO binary, DynamicRivals.dll and HARTRivalBalance.dll. The release output records exact results, file hashes and any unavailable optional targets. Separate processes exercise real legacy-assembly loads in both orders and unload ownership. All imported rule and compatibility suites must pass against the merged DLL.

Final offline run on 23 October 2026: **205 core tests passed**, **256/256 API references** resolved on both cached Windows and installed Mac assemblies, and all **8 target mods** passed hook resolution, contracts and Harmony application. Scenario **38**, factory **76**, running costs/BizMan **44**, compatibility **30**, unreviewed-RLO gate **19** and series-settings **26** checks passed. Real legacy DLL load-order checks passed: legacy-first **11**, pack-first **17**, pack-only/unload/takeover **13**. The unchanged legacy repo's **170 core tests** also passed. All available optional target DLLs were included; no target was skipped. The offline settings harness deliberately leaves RLO's live options page unregistered and verifies the explicit skip path.

The provenance check confirms 47 imported source/test files match 0.5.0 after title/owner substitutions, with three reviewed UI/lifecycle adapters. The shared page has all 22 toggles exactly once and resolves its seven own preset values under the new assembly owner. ZIP CRC, exact inventory, all payload hashes, dependency identity and mirrored manifests pass. The initial test runs found stale test expectations for the removed member and renamed Harmony owners; those were updated. The lifecycle harness stubs engine object creation, preference reads and logging, and runs the real mod lifecycle and hook cleanup. No gameplay thresholds were relaxed.

Offline build, API, hook and lifecycle checks are not native gameplay acceptance. The old patch and helper's historical Mac/Windows evidence remains historical. Merged 1.1.0 still needs native checks of the settings page/button, prefill, new-game debt, midnight bills, diploma hold/restore, BizMan reasons, save/reload, and both Workshop and ModsLocal duplicate cases. No installation, save edit or Workshop upload is part of this merge task.

## Publishing files

Use `workshop/DESCRIPTION.bbcode.txt` and `CHANGENOTE-1.1.0.bbcode.txt` for HART. Mod Pack. Replace `THRIVING_RIVALS_ID` and `CITY_PERFORMANCE_ID` in the description once their Workshop IDs are chosen. Remove the old Patch from Steam's Required items. Use the old patch repository's short `workshop/DESCRIPTION.bbcode.txt` for that item. Retain all other required-item links. No Steam publication has been performed.
