# Release session 2, 23 October 2026

The helper continues to delegate the preset to HART. Mod Pack Patch. Documentation now directs the series to equipment utilities and office IT instead of the flat rent increase. The approved shared JPG artwork was format-converted to a new 952,547-byte PNG without resizing or redesigning it; only preview.png is packaged. The existing JPG remains in source, outside the package.

29 Core tests passed. All 79 game references resolved against Windows and Mac assemblies. Logs: `build/release-session2-build.log`, `build/release-session2-api.log`.

Campaign Objectives 1.2.1 was already in `Core/TestedVersions.cs` and its pinning test because the earlier integration passed (see `docs/integration-2026-10-23-campaign-1.2.1.md`). It was not newly bumped or claimed newly tested here. Campaign Objectives files are unchanged. The fresh revised-economy/CP-default integration passed on Mac as recorded below; Windows acceptance remains separate.

The 74-setting/new-game/midnight/save-reload flow and native billing fixtures passed. Pending: real business-month settlement, Windows behavior/UI and Workshop installation. Required items for the three new mods need their real IDs after Peter publishes them. Descriptions use their names without fake URLs or Discord placeholders.

## Package verification

- Archive: `build/HARTModPack-1.0.0.zip`.
- SHA-256: `f61795a8edf784a2ce7e98637007e998e51a55fa357088dfa0c89329eeff7451`; matching `.zip.sha256` sidecar.
- DLL SHA-256: `dd00eb7fc0e10b2e0133dffc09ac5ae3d89f69fb108c5ee87ec9617467541921`.
- 6 files: ZIP CRC, exact file inventory, every decompressed hash, package manifest and approved thumbnail equality passed. Exactly one root DLL and only `preview.png` as the root image; no preview.jpg.
- Full description: `workshop/DESCRIPTION.bbcode.txt` (5426 characters, below 8,000), ends with the PayPal Support section.
- Change notes: `workshop/CHANGENOTE-1.0.0.txt`, `.bbcode.txt` and `.md`.

## Native Mac integration and limits

Both rigs were initially occupied and were left alone during offline work. At 14:01 CEST the Mac process had exited and its lock was released. Acquired a new reservation (`mods-release-session2-20261023`), verified/backed up all 3,144 profile files, preferences and installation manifest, and installed the four release candidates plus unchanged Campaign Objectives 1.2.1 and RLO 0.49.0. The existing Food Trucks 0.10.2 bridge snapshot was untouched. Private client shims used the existing compiled lock controller, so no Food Trucks repository/tool files were accessed or changed.

PASS: actual series button changed a deliberately seeded rent value from 1 to 0, enabled equipment utilities/office IT, and stored all 74 expected options. All persisted after restart. City Performance's 13 toggles matched the fresh defaults (nine behavior switches on, four diagnostics/trace switches off). The real Custom menu prefilled all 24 world fields. New Day 1 had $551.37 and one $75,454.35 loan. The world reached day 2 at 00:05, with $41.37 and $75,140.35 principal; save/quit/restart/reload preserved day, cash, debt, all world settings and the complete-v1 scenario marker. Main session `dc5f0d43`; reload session `a2ed72bc`.

PASS: 12 bounded running-cost fixtures executed inside native Mono using actual catalogue items and the patched RLO billing methods: 12h and 24h fryer/fridge use, closed-day refrigeration, sink allowance, unchanged home bills, factory equipment, office plan-only versus three-computer IT charges, outage credit and law-firm internet requirement. Fixtures used temporary registrations while paused, were removed synchronously without saving, and verified unchanged world money/day/registrations. Subsequent reads also matched cash, debt, settings and marker. This is native billing-method coverage, not actual player leases, nightly/monthly business settlement, office closure presentation or Utilities/BizMan UI acceptance.

Restored the complete original profile, all 3,144 hashes, preferences and install manifest; stopped the game and released ownership. Disposable test profile and structured evidence remain under Mod Pack's ignored `build/release-session2-20261023/`. The two game logs were archived and SHA-256 verified at `C:\Users\Peter\HART-Diagnostics\Performance\release-session2-20261023`; temporary Mac log copies were removed. Normal profiles were untouched. Windows agent1 remained reserved by the Persistent City session throughout; no Windows gameplay claim is made.

The Mac's unaudited game hash disables most City Performance behavior even when its toggles are on. Known Windows-only Bakery/Hyperreal bundle errors remain. RLO reports FAIL in minimap alignment, transit network and transit-pole diagnostics; these are outside this scoped release check and are not cleared by it. Native Windows all-on behavior, rendered UI/performance, actual business bills and the Steam install path remain manual checks. Campaign Objectives 1.2.1 was already pinned after the earlier passing integration; this fresh pass confirms the updated combination without changing Campaign Objectives or adding an untested pin.

No Workshop upload, force-push or history rewrite. Food Trucks repository/worktrees/files and Campaign Objectives files were untouched. Artwork images were not redesigned or re-rendered. The required gallery redraws are in the shared `DESIGN-HANDOFF-MODS.md`.
