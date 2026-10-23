# Windows rig acceptance, 23 October 2026

Completed native Windows runs on 23 October, 1920×1080 windowed, with the release-session-2 DLLs. No Workshop upload, SteamCMD, force-push or history rewrite. No Food Trucks repository/worktree changes. The installed bridge was used only in the isolated agent1 profile. Other installed members were retained for the full-stack test; this was not an update of every pack member.

**Rig restored and released:** original 4,290 profile files match SHA-256; preferences, installation manifest and all 105 manifest references match. No Big Ambitions process; lease unheld at 13:50:58 UTC. The lease was acquired only after the Street Food session released it and its game exited. Every subsequent launch checked the same compiled lock controller and absence of a game process. Evidence: `restore-receipt.json`, `final-rig-status.json`, `lease-polls.jsonl`. The first restore verification hit a PowerShell nested-array count bug in the test script; corrected the verifier, then checked every original file hash successfully. No restoration discrepancy remained.

Evidence root (paths below relative to it): `big-ambitions/big-ambitions-modpack/build/windows-rig-20261023/`. Windows backup, retained test profiles and full logs: `C:\Users\Peter\HART-Diagnostics\Performance\windows-rig-20261023`. Normal ModsLocal, normal preferences and Workshop subscriptions were not changed. Test-only reflection helper, temporary preferences and save fixtures were removed from the restored profile. The helper never ships in a package.

### City Performance 0.5.0

- PASS: exact release DLL/version, all-on requested defaults and recurring logging off. Stock Steam Windows game hash was read and matches the audited `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911`.
- FAIL acceptance / BLOCKED: the isolated rig actually runs `055700fd26a7a6430bbb6a9d79379781909fbc518af760453396947887db5063`. Native `Verified=false`; vanilla background-save/refill/cleanup/tree/cinematic hooks withdraw, Bakery Help reports an unreviewed binary. Hyperreal discovery alone is installed and active. This is the guard working, not evidence that all release hooks run on stock Windows. No hash bypass or rig-derived release DLL was substituted.
- PASS fallback saves: manual saves/reload and native Recover Midnight saves worked during the billing test. BLOCKED: optimized background autosave acceptance, complete Help/lamp parity and a measured all-on frame-time gain versus disabled. No performance gain is claimed; the screen FPS counter is not an A/B benchmark.
- PASS limited rendered smoke: sampled city/settings frames had no obvious new rendering artifact. BLOCKED: all-on visual parity because most hooks did not activate. Evidence: `diagnostic-city.json`, `diagnostic-menu.json`, `bridge-help.json`, `city-screen.png`, Windows `full-billing-diagnostics.log`. No code fix or new commit; release-session-2 commit `6277988` retained.
- **Release ready: no.** Requires an isolated test arrangement that can safely exercise the audited stock Windows binary, then actual all-on autosave/parity/frame-time acceptance.

### HART. Thriving Rivals 0.2.0

- PASS: actual native dropdown arrows selected Hard, Normal and Easy; runtime state followed. Actual Reset Mods to Defaults returned Normal.
- PASS: compiled native fallback loader accepted legacy Hard, returned Normal for invalid/missing config, and Normal for invalid stored index 99. `rivals-native2.json` has an unresolved callback probe; this is superseded for dropdown interaction by the actual UI actions and state snapshots, not counted as a passed callback probe.
- FAIL then FIXED/PASS: the original explanation crossed the next divider at 1920×1080. Shortened the English note; rebuilt ZIP has only that locale entry changed, with byte-identical release DLL. Native restart/screenshot confirms it fits. All 25 tests passed; CRC, eight-file inventory, versions, hashes and PNG-only thumbnail passed.
- Fix committed and pushed without force: `youtube/hart-group` `45a611c`. No tuning changed, so behavior change notes remain unchanged. Evidence: `rivals-hard-state.json`, `rivals-easy-state.json`, `reset-state.json`, `rivals-native2.json`, `rivals-screen.png`, `rivals-fixed.png`, `rivals-fixed-package.json`, `rivals-build.log`; source record `mods/rivalbalance/workshop/RELEASE-CHECK-0.2.0-windows.md`.
- **Release ready: yes**, for the requested settings/reset/fallback/visual checks. This is not a new long-term rival-economy benchmark or live Workshop installation test.

### HART. Mod Pack Patch 0.5.0

- PASS: unchanged final DLL; reviewed RLO 0.49.0 running-cost hooks active; `equipment_utilities=1`, `office_it=1`, `rent_rebalance=0`. Real native rentals at 31 2nd Street and 34 10th Avenue retained market rents $27/$32 per day.
- Fixture scope: disposable `FT Windows Release…` saves, supplied $250,000 startup capital and unlocked courses, native rent/start-business actions and native matching layouts (four-computer law office, furnished fast-food shop). No fabricated account or direct billing invocation. Clock ran through real hours/midnights at bridge speed 60 with needs maintained. This used the bridge-created world's settings, not a verified 130% series-income scenario; no staff or sales revenue was set up.
- PASS nightly accrual: first office power/water $33.30/$5.48; kitchen $115.76/$36.64. Active office connection accrued $139.44 per day at the first price level: $19.44 connection plus four × $30 daily IT in the five-day month. This is consistent with the specified $150 per computer per game month.
- PASS first-month billing: day 6 issued office power $167, water $27, internet/IT $279; kitchen power $579 and water $183. Total $1,235, due day 8. Accrual reset after billing. Day 8 auto-debit cleared all five owed balances and incremented each OnTime counter; cash change reconciled to $1,235 plus two regular $478 daily costs ($243,740 to $241,549).
- PASS Utilities accounts/internet screens: rendered values agree with native account state. As documented in the existing README, Internet's plan-price preview excludes desk IT; Accounts shows the actual combined bill. PASS BizMan rendering/capacity (four computer workstations). PASS closure behavior/reason log: office closed while installation pending (`reallife_util_closed_net_install`), reopened on connection day 4. LIMIT: captured BizMan shows generic “Is temporarily closed”; the specific cause is verified in RLO's log, not a rendered BizMan explanation.
- PASS arithmetic/finite-value sanity; **NOT DONE: staffed-business profitability/balance calibration at actual series settings**. Supplied capital and empty businesses cannot establish that the pressure is playable. Do not promote this fixture to that claim.
- Evidence: `diagnostic-city.json`, `office-rented.json`, `shop-rented.json`, `economy-day2.json`, `subscribe-office.json`, `billing-day4.json`, `billing-month.json`, `firstbill.json`, `due-state.json`, `accounts-panel.png`, `internet-panel.png`, `bizman-day2-screen.png`, `paid-save.json`; Windows `full-billing-diagnostics.log`. No runtime fix; release-session-2 commit `d692a5c` retained.
- **Release ready: no** under the full requested balance acceptance. Night/month/first-bill/payment and rendered account checks passed; economic calibration and the specific rendered closure explanation remain unverified.

### HART. Mod Pack 1.0.0

- PASS: unchanged final DLL loaded, local member detection/version scan and settings page; native log correctly skips Steam enable handling when Steam is disconnected. Series-settings reminder emitted.
- BLOCKED: actual subscription, Steam Required items propagation, initially disabled subscriptions, enable/restart flow. Agent1 explicitly disables Workshop and Steam connection/writes. These were not simulated as native successes. No normal-profile/subscription changes were made.
- After publishing, check the real Required items list/IDs, subscribe from a clean Steam test account/profile, confirm downloads and dependency subscriptions, start with required items disabled, verify automatic enable and restart notice, restart and verify loaded members and cleared warning. Missing/failed/duplicate cases should then be checked through the real installed-member path. No upload was attempted.
- Evidence: `health-full.json`, `diagnostic-menu.json`, `mods2.json`; Windows full-stack logs. No code fix; release-session-2 package retained.
- **Release ready: no.** Steam flow and City Performance acceptance remain open.

### RLO and asset-bundle classification

- **Minimap diagnostic FAIL reproduced without our mods.** Full stack: marker fit 0.0000 m, door-to-outline outlier 3636.4 m. Vanilla game + RLO 0.49.0 only (plus test observer): fit 0.0000 m, outlier 3689.9 m. Classification: pre-existing RLO/rig diagnostic failure, not introduced by our gameplay hooks; different values between loads are not claimed identical.
- **Transit network FAIL reproduced without our mods.** Both runs generated 13 lines/11 routes, only 10 lines with an opposite direction, 35 stops; one-way-line condition remained. Classification: pre-existing RLO/rig issue.
- **Transit poles FAIL reproduced without our mods.** Full stack 20/35 clear, RLO-only 21/35 clear. Classification: pre-existing RLO/rig issue; slight count variation does not clear the failure.
- These diagnostic methods were explicitly invoked with RLO diagnostic logging temporarily enabled and then restored; City Performance recurring logging stayed off. The RLO-only run loaded the same session's clean day-1 checkpoint, originally created with the full stack; stale mod-data keys can remain in that save, but their mods were absent. It is not a newly generated pristine vanilla save. The full-stack diagnostic capture was on the subsequent billing save, so this comparison establishes reproduction, not byte-identical world state.
- **Bakery/Hyperreal bundle errors NOT REPRODUCED on Windows.** Full stack and a separate RLO + Bakery + Hyperreal launch with all our gameplay mods absent loaded the bundles. Hyperreal reported all six compute shaders OK with SSR/SSGI/volumetrics/clouds/exposure enabled; Bakery registered its runtime assets/help. Vanilla+RLO-only also loaded; it cannot exercise absent Bakery/Hyperreal bundles. The earlier Mac failures remain third-party/platform compatibility observations; this Windows run does not prove their exact root cause or claim they are fixed. No evidence attributes a Windows bundle failure to our mods.
- Evidence: `rlo-full-ready.json`, `baseline-installed.json`, `baseline-rlo.json`, `baseline-screen.png`, `thirdparty-installed.json`, `thirdparty-diag.json`, `thirdparty-bundles.txt`; Windows `rlo-only-diagnostics.log` SHA-256 `4db438e807674ed1ca483f7f9bfb35c08e1f245a50794f2a2bc7d0ba9e3c3703`, `thirdparty-without-ours.log` SHA-256 `7841a95280bc557ea9a9d904707b13b75f77b823a7110612c4acd056fb6c22f6`.

### Final release ZIPs

CRC, file inventories, DLL hashes/version metadata and only `preview.png` thumbnail verified. Only Rivals was rebuilt; all four DLLs remain byte-identical to release session 2. These paths are relative to `~/Coding_Projects`:

- `big-ambitions/big-ambitions-performance/build/CityPerformance-0.5.0.zip` (7 files)
  SHA-256: `a4c3566c582ee1e671b7260f623cd1d3828ae766bf9861c72e61c54d8b19744f`
- `youtube/hart-group/mods/rivalbalance/build/HARTRivalBalance-0.2.0.zip` (8 files)
  SHA-256: `db81ffb15d81dd8a16e69ff5a6bd8635cefbe1f1c1a182ee3541959e641e5b20`
- `big-ambitions/big-ambitions-hard-mode-patch/build/HardModePatch-0.5.0.zip` (13 files)
  SHA-256: `aa5b327150269bf57402cca2becd0bc7d7903ade7c1a73f0a01579219132201a`
- `big-ambitions/big-ambitions-modpack/build/HARTModPack-1.0.0.zip` (6 files)
  SHA-256: `f61795a8edf784a2ce7e98637007e998e51a55fa357088dfa0c89329eeff7451`
