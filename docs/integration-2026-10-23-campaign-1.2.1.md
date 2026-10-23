# Campaign Objectives 1.2.1 Mac integration

23 October 2026. PASS for the documented series-settings, new-game, midnight and save/reload flow. Campaign Objectives was added to the tested-version table only after this flow passed.

## Setup

Used the existing isolated `agentmac` rig and its ownership lock, in headless mode. The normal game and profile were untouched. Followed the integration flow in HART Group's `docs/series-2/modpack-release-plan.md` and the rig's `tools/agent-tests/MAC.md`. Backed up the complete rig profile, installation manifest and preferences before changing it. Verified 3,115 profile-file hashes against the backup. Parked Persistent City for the test.

Candidates: fresh Campaign Objectives 1.2.1 from HART main `4c987f7`, Thriving Rivals 0.2.0 from `d63882e`, Mod Pack Patch 0.5.0 from `daf9187`, and the renamed Mod Pack 1.0.0. RLO was 0.49.0. City Performance was the existing 0.5.0 candidate from the prior integration, not a newly merged main build. Exact installed hashes and source paths are in ignored `build/release-prep-20261023/configured.json`.

The successful session used the rig's existing Food Trucks 0.10.2 bridge snapshot, which supports named custom presets, as the prior integration did. Other untouched members were the rig's snapshots (including Driven Rides 0.2.0-rc7 and Roadster 0.0.1). A preliminary attempt with the release Food Trucks 0.10.5 bridge ignored the preset argument and started a Normal world; that attempt was excluded. The temporary helper's direct menu method also failed; invoking the actual New Game and Custom buttons succeeded. No shipped mod was changed to accommodate either harness issue.

## Checks

- The pack reported Campaign Objectives 1.2.1, Thriving Rivals 0.2.0 and Mod Pack Patch 0.5.0 as loaded. The renamed status and reminder text appeared in the log. The Steam enable step was skipped because the rig is offline.
- Invoked Mod Pack Patch's series-settings action at the main menu. All 74 stored option values matched `tools/export-series-settings.ps1` and survived restart.
- Opened the actual New Game and Custom buttons through the rig helper. Saved the screen's prefill as `FT Release Prep 121`, then created the disposable `FT Release Prep Series121` world using that preset. All game-variable fields matched the screen's values.
- Day 1: $551.37 cash, one $75,454.35 loan, and the `complete-v1` indebted-start marker.
- Ran the simulation at bridge speed 20 with needs maintained to day 2 at 00:05. The bridge paused at the target. Cash was $41.37 and loan principal $75,140.35, following the $196 interest and $314 principal payment.
- Saved `FT Release Prep Series121 Day2`, reloaded, and compared day, money, loans, every game variable and the indebted-start marker. All matched; exactly one indebted-start creation was logged.

Successful session: `624994a1`. Structured results, request/response JSON, full logs and the preserved disposable profile are under ignored `build/release-prep-20261023/`.

## Limits

The headless rig cannot establish panel appearance, dock behavior, rendering/performance or the Steam subscription/enable/restart path. Bakery and Hyperreal report their known unavailable Windows-only asset bundles. RLO's transit-pole placement diagnostic reports FAIL, also present in the previous integration log; this run does not clear RLO's own diagnostic or gameplay gaps. No new exceptions beyond the two known asset-bundle failures appeared in the successful flow. This is scoped Campaign Objectives coexistence evidence, not acceptance of every latest pack member or every campaign feature.

Native Windows/Workshop acceptance and City Performance's main-branch release remain pending. Nothing was uploaded.

## Final package and restoration

After the successful flow, added 1.2.1 beside 1.2.0 in `Core/TestedVersions.cs` and its pinning test. Rebuilt: 29 Core tests pass; 79/79 API references resolve on both Windows and Mac. A final rig launch using the rebuilt helper reported `Campaign Objectives: on, 1.2.1, ModsLocal` without the untested warning. The final helper DLL SHA-256 is `dd00eb7fc0e10b2e0133dffc09ac5ae3d89f69fb108c5ee87ec9617467541921`.

`build/HARTModPack-1.0.0.zip` SHA-256: `d9b5d38cd1a4b1e0c047fabd6f7c4d95d4965e27f7759e6bad46944ed226559f`. ZIP integrity, exact six-file inventory and every file's bytes verified; `preview.jpg` is the only root image and matches the Workshop source.

Stopped the rig, restored all 3,115 original profile files with exact inventory/hash equality, restored the installation manifest and verified every referenced file, restored the preferences with dictionary equality, and released ownership. The disposable tested profile and logs remain in the ignored evidence folder; no prior saves or profile files were lost.
