# Release session 2, 23 October 2026

The helper continues to delegate the preset to HART. Mod Pack Patch. Documentation now directs the series to equipment utilities and office IT instead of the flat rent increase. The approved shared JPG artwork was format-converted to a new 952,547-byte PNG without resizing or redesigning it; only preview.png is packaged. The existing JPG remains in source, outside the package.

29 Core tests passed. All 79 game references resolved against Windows and Mac assemblies. Logs: `build/release-session2-build.log`, `build/release-session2-api.log`.

Campaign Objectives 1.2.1 was already in `Core/TestedVersions.cs` and its pinning test because the earlier integration passed (see `docs/integration-2026-10-23-campaign-1.2.1.md`). It was not newly bumped or claimed newly tested here. Campaign Objectives files are unchanged. A fresh integration with the revised economy and CP defaults remains blocked by occupied rigs; do not treat the prior integration as acceptance of this combination.

Pending: rerun the 74-setting persistence/new Custom game/day-1 debt/midnight/save-reload flow, check the new running-cost rules, then Windows/Workshop installation. Required items for the three new mods need their real IDs after Peter publishes them. Descriptions use their names without fake URLs or Discord placeholders.

## Package verification

- Archive: `build/HARTModPack-1.0.0.zip`.
- SHA-256: `e50f733897537a4c1b51a74cdd5bce3db8f73f7c57116517ef1d71cce179ebd7`; matching `.zip.sha256` sidecar.
- DLL SHA-256: `dd00eb7fc0e10b2e0133dffc09ac5ae3d89f69fb108c5ee87ec9617467541921`.
- 6 files: ZIP CRC, exact file inventory, every decompressed hash, package manifest and approved thumbnail equality passed. Exactly one root DLL and only `preview.png` as the root image; no preview.jpg.
- Full description: `workshop/DESCRIPTION.bbcode.txt` (5426 characters, below 8,000), ends with the PayPal Support section.
- Change notes: `workshop/CHANGENOTE-1.0.0.txt`, `.bbcode.txt` and `.md`.

## Rig availability and limits

Read-only availability checks and retries during this session found both rigs occupied. At 13:55 CEST, Windows agent1 PID 34936 was reserved by `persistent-city-neighborhood-finance-20261023`; Mac agentmac PID 18917 was reserved by `street-food-cooler-release-20261023`. No reservation was taken over; no game, mod, profile, save, preference or manifest was changed. New native integration and running-cost acceptance remain pending. These are verified build candidates, not a claim that the missing in-game checks passed.

No Workshop upload, force-push or history rewrite. Food Trucks repository/worktrees/files and Campaign Objectives files were untouched. Artwork images were not redesigned or re-rendered. The required gallery redraws are in the shared `DESIGN-HANDOFF-MODS.md`.
