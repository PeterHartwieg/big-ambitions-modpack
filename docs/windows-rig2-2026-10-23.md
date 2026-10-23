# Stock Windows acceptance preflight, 23 October 2026

**Release ready: no.** Stopped before launch because a safe isolated stock-game launch was not established. This is a preflight and package verification record, not a gameplay test. No runtime code, tuning, release descriptions or packages changed.

## Live safety checks

- At 16:09:57 CEST, the first remote check found no `Big Ambitions.exe` process. At 16:10:36 CEST, a broader `*Ambitions*` process query also returned none, and the existing compiled `BridgeControl.LeaseStore` reported agent1 unheld.
- Acquired agent1 as `windows-rig2-stock-20261023` at 14:10:52 UTC. All subsequent rig inspection while owned checked the owner token and absence of game processes. No game was launched.
- Released the lease after the read-only isolation investigation. Final status at **14:14:58 UTC / 16:14:58 CEST**: no game processes, lease unheld, zero pending operations.
- No normal or agent profile, ModsLocal, saves, preferences, Workshop subscriptions, game files or installed bridge files were changed. No restoration was necessary. Only lease acquire/release changed remote state; no wake, process termination, user-account change or scheduled-task creation occurred. Food Trucks repositories/worktrees were not accessed.

## Why the stock launch stopped

Live SHA-256 of the stock `Big Ambitions_Data/Managed/BigAmbitions.dll` is **9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911**, exactly the audited hash. This verifies the managed game assembly, not a `Verified=true` runtime result. The stock executable itself is **bda4d11adce732464408b14502722745c666fe6c3615d29c43fe3d9a9ed708b5**.

Read-only inspection of the installed launcher and receipt confirms the existing agent1 setup uses changed `globalgamemanagers`/product identity, `HGPlugins.dll`, `BigAmbitions.dll` and `BigAmbitions.ModsInternal.dll`. Its recorded managed game hash is `055700fd26a7a6430bbb6a9d79379781909fbc518af760453396947887db5063`. Reusing that setup does not meet the stock-game requirement.

Inspected local reference assemblies whose hashes exactly match the live stock files. `SaveGamePathHelper.SaveGameFolderPath` and `ModDiscoveryRegistry.ModsLocalPath` derive their locations directly from `Application.persistentDataPath`; these inspected paths have no separate test-profile argument. Stock `SteamAPI.InitSteam` still calls `SteamClient.Init` when `-disable-force-steam` is supplied: the flag only skips `RestartAppIfNecessary`. It does not provide Steam/Workshop isolation.

Unity documents Windows persistent data as a user/company/product path resolved with Windows known-folder APIs, and PlayerPrefs as HKCU/company/product registry storage. Merely changing the working directory or `USERPROFILE` environment variable is therefore not a verified isolation method. See [Unity persistentDataPath](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/application/persistentdatapath), [PlayerPrefs](https://docs.unity.cn/2021.1/Documentation/ScriptReference/PlayerPrefs.html), and [standalone command-line arguments](https://docs.unity.cn/6000.1/Documentation/Manual/PlayerCommandLineArguments.html).

No supported combined profile/preferences/Steam isolation override was established. A helper loaded as a normal mod cannot be assumed to isolate the profile before the loader itself reads ModsLocal. Replacing the existing rig's game DLL alone would leave the other modified runtime components in place. Moving/junctioning Peter's normal profile, experimenting with his registry, or patching game binaries was not attempted. Existing Codex sandbox accounts were not repurposed for gameplay.

This does not prove that a non-invasive arrangement is impossible. A separately provisioned Windows test profile with a reviewed stock launcher and independent observer is a possible future route, but its startup, Steam behavior and early profile access need verification before any game launch. Under the explicit stop-if-unsafe instruction, no speculative launch was made.

## Acceptance status

| Mod / check | Result in this session |
| --- | --- |
| City Performance 0.5.0 stock managed hash | PASS |
| City Performance isolated stock launch | FAIL prerequisite; no launch |
| All-on defaults/logging off; hooks `Verified=true` | NOT RUN |
| Background autosaves; Help/lamp parity; visual off/on comparison | NOT RUN |
| Matched save/camera, warm-up, repeated frame-time A/B | NOT RUN |
| Mod Pack Patch HART button and series starting cash | NOT RUN |
| Staffed/profitable retail and office; 12h/24h; several months | NOT RUN |
| Specific BizMan closure message | NOT RUN; no speculative patch |
| Thriving Rivals native acceptance | Not repeated; prior bounded PASS retained |
| Mod Pack integration | Not repeated; members unchanged; prior open gates retained |
| All four ZIP CRC/inventory/hash/version/PNG checks | PASS |

| City Performance condition | Runs | Mean ms | Median ms | p95 ms | p99 ms | FPS |
| --- | --- | --- | --- | --- | --- | --- |
| Off | 0 | N/A | N/A | N/A | N/A | N/A |
| All-on, logging off | 0 | N/A | N/A | N/A | N/A | N/A |

No frame-time or improvement claim is available.

| Intended business/month sample | Revenue | Wages | Rent | Utilities | Office IT | Profit |
| --- | --- | --- | --- | --- | --- | --- |
| Retail, 12h, each of several months | N/A | N/A | N/A | N/A | N/A | N/A |
| Retail, 24h, matched months | N/A | N/A | N/A | N/A | N/A | N/A |
| Staffed office with computers, matched months | N/A | N/A | N/A | N/A | N/A | N/A |

**Balance verdict: unassessed.** The preceding session's unstaffed, supplied-capital billing fixture cannot establish series profitability or fair revenue-relative costs. No tuning is warranted from this session's evidence. No bugs were established or fixed. Full test suites, rebuilds and native retests were not run after the safety stop; historical test counts are not new results.

Release readiness: City Performance **no**; Mod Pack Patch **no**; Mod Pack **no**. Thriving Rivals retains the previous session's **yes for its bounded settings/reset/fallback/visual scope**, without a new gameplay endorsement.

## Existing release packages, verified again

Paths relative to `~/Coding_Projects`. Every archive is byte-identical to the preceding Windows session, passes CRC, matches its SHA-256 sidecar, has exactly one root mod DLL, and contains only `preview.png` as its image. Decompressed hashes were recorded for every file. Assembly versions were read directly from the packaged DLL metadata.

| Mod | ZIP | Files | Assembly / informational version | SHA-256 |
| --- | --- | --- | --- | --- |
| City Performance | `big-ambitions/big-ambitions-performance/build/CityPerformance-0.5.0.zip` | 7 | 0.5.0.0 / 0.5.0 | `a4c3566c582ee1e671b7260f623cd1d3828ae766bf9861c72e61c54d8b19744f` |
| Thriving Rivals | `youtube/hart-group/mods/rivalbalance/build/HARTRivalBalance-0.2.0.zip` | 8 | 0.2.0.0 / absent | `db81ffb15d81dd8a16e69ff5a6bd8635cefbe1f1c1a182ee3541959e641e5b20` |
| Mod Pack Patch | `big-ambitions/big-ambitions-hard-mode-patch/build/HardModePatch-0.5.0.zip` | 13 | 0.5.0.0 / 0.5.0 | `aa5b327150269bf57402cca2becd0bc7d7903ade7c1a73f0a01579219132201a` |
| Mod Pack | `big-ambitions/big-ambitions-modpack/build/HARTModPack-1.0.0.zip` | 6 | 1.0.0.0 / 1.0.0 | `f61795a8edf784a2ce7e98637007e998e51a55fa357088dfa0c89329eeff7451` |

Concise local evidence: ignored `build/windows-rig2-20261023/stock-preflight.json`, `lease-polls.jsonl`, `final-rig-status.json`, `package-verification.json`, `dll-versions.tsv`. No gameplay log or trace was created. Local decompilation working files remain ignored and are not committed.

No Workshop upload, SteamCMD, force push, history rewrite or game-hash/version-check change occurred.
