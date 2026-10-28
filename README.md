# HART. Mod Pack

The Workshop item for the mods of the HART modded series of Big Ambitions. Its Workshop page lists every pack member under Steam's Required items. It includes the series settings, gameplay rules and compatibility fixes from HardModePatch 0.5.0, finds the members, turns on the ones the game left off, and reports missing or untested members.

Version 1.1.1, built for game build 3682. One HARTModPack.dll and one settings page. Gameplay uses checksum-pinned Harmony; scenario and diploma records keep their original save keys.

Supports reviewed RLO 0.50.0. [The per-target review](docs/rlo-compatibility.md) records signatures, IL comparisons and offline checks. **Mute RLO night chirping** defaults on and the series preset enables it. It mutes only RLO’s generated cricket layer; switching it off restores normal sound without restarting or changing RLO’s settings.

See [the merge and migration notes](docs/merge-1.1.0.md). If HardModePatch.dll is also loaded, the pack gameplay stands down and asks the player to unsubscribe from Mod Pack Patch, remove any ModsLocal copy and restart. The member checker remains active. Apply the series preset and personal rule choices on the new page.

## What it does

**Finds the members by DLL name.** The game identifies a mod by the assembly name of the one DLL in its folder, so the pack does the same and never relies on a Workshop ID. Required: RealLifeOverhaul, DynamicRivals, HARTRivalBalance, CityPerformance, FoodTrucks, DrivenRides, Roadster, AlcwareSeasons, RetailCapacityUpgrades, InternetCafe, Bakery, PolyModsHyperreal and CampaignObjectives. Optional: CameraTools, HARTFreeCamera and BigCopilotLink. The names come from the installed copies on 22 October 2026. Persistent City is not part of the pack.

**Turns on members the game left off.** When Steam downloads a subscription while the game is closed, the game never adds it to its list of enabled Workshop mods (`steam_mod_manifest.txt`), so it stays off. Once the other mods have loaded, the pack asks Steam for the subscribed mods (a read-only query), finds required members that are subscribed but off, adds them to that list and shows one notice: "HART. Mod Pack turned on N mods. Restart the game to load them." The rules:

- It only adds. It never switches anything off.
- Required members only. Optional members and Persistent City stay as they are.
- Each Workshop item at most once. If you switch a member off after the pack turned it on, it stays off and the pack reports it instead.
- A member that has a copy in `ModsLocal` keeps its Workshop copy off, so it isn't loaded twice. Two subscribed items with the same DLL name are both left off.
- It finds members by DLL name only, so another author's mod with the same DLL name counts as that member.
- While the Mods panel is open it waits: the game loads mods added during that time as soon as the panel closes.
- It reads `ModsLocal` but never writes there.
- Without Steam it skips this step and writes one line to `Player.log`. The game loads no Workshop mods without Steam, so this only matters for a `ModsLocal` copy of the pack.

**Checks the pack once per launch.** One notice lists required members that are missing, switched off, waiting for a restart, refused by the game or failed during their start, members whose version this pack wasn't tested with, and members loaded from both the Workshop and `ModsLocal`. The game forgets start failures when the next scene's mods load, so a startup-scope member that isn't among the game's started mods counts as failed. The notice waits until the game can show notices and is worded from a fresh scan at that moment. Click it to hide it until the list changes or the pack updates; the choice is kept in PlayerPrefs. The check notice skips a launch that turned members on (a restart is pending anyway) and a launch where Steam didn't answer, because switched-off members would then read as missing. Once per pack version, while the integrated gameplay is active, the pack reminds you that the series settings come from Settings > Mods > HART. Mod Pack > Apply HART series settings.

The tested versions live in `Core/TestedVersions.cs`: Real Life Overhaul 0.45.0, 0.49.0 and 0.50.0, HART. Thriving Rivals 0.2.0, City Performance 0.5.0, Campaign Objectives 1.2.0 and 1.2.1, HART Free Camera 1.1.0 and 1.1.1. These are the builds this release goes out with; the evidence section says which have run together in the game. The check reads each member's informational version, else its assembly version. Most other members report 0.0.0.0, which says nothing, so they are never flagged.

**Mods page.** Settings > Mods > HART. Mod Pack includes the series button and all 23 gameplay toggles, followed by a summary, a "Check again" button and one line per member with its state, version and source. The lines change at runtime while the game's option labels are localisation keys, so the mod writes them as a small locale table to `HARTModPack/StatusLocale/` in the game's data folder and registers it with the game. The page is rebuilt only when a line changes, because a rebuild while the page is open re-runs every mod's option controls, as opening the page does. So "Check again" shows no change when nothing changed. Every line also goes to `Player.log`. A startup mod that a later rescan discovers (the game rescans when its window regains focus after a file change) shows as waiting for a restart, not as on.

**Two copies of the pack.** With a Workshop and a `ModsLocal` copy both on, the first copy works and the other waits. If the working copy is switched off, the waiting one takes over; at quit this can leave a few cleanup warnings in `Player.log`.

**Incomplete Steam answers.** Steam's helper returns an empty page when it times out. A listing that misses a Workshop mod the game loaded this launch, or is empty while mods are enabled, counts as failed: nothing is turned on and the check notice skips that launch.

## Game code it uses

The member checker uses public classes in the game's own assemblies; `tools/check-api.sh` resolves each one against a game install without running it.

| Call | What for | Risk on a game update |
| --- | --- | --- |
| `SteamHelper.GetUserSubscribedItems(int, string, string, SteamId)`, by reflection, reading `Item.Id.Value` and `Item.Directory` | Subscribed Workshop mods and their folders, read-only. Steam's types live in `Facepunch.Steamworks.Win64` on Windows and `.Posix` on the Mac, so the mod doesn't compile against them. | Medium. A changed signature makes the listing fail with one log line; the check then runs without Steam data. `check-api.sh` checks these members on both platforms. Not `SteamModLoadingService.GetSubscribedMods`, which also downloads and enables any subscribed item without a folder. |
| `SteamHelper.IsConnectedToSteam()` | Skipping the Steam step without Steam. | Low. |
| `ModManifest.Add(ulong)`, `ModManifest.Contains(ulong)` | Turning a member on; reading whether it's on. | Medium: `ModsInternal` has no stability promise. If the game moves away from `steam_mod_manifest.txt`, turning on stops and the check still runs. |
| `ModDiscoveryRegistry.Entries`, `.FailedModReasons`, `.ModsLocalPath`, `.OnDiscoveryUpdated`; `ModLifecycleLoader.FailedModMessages`, `.ActiveModIdsByScope` | Which mods the game discovered, refused or started, and where `ModsLocal` is. | Medium, `ModsInternal` again. A change fails the build or `check-api.sh`. |
| `ModEvents.onModsLoaded` | Starting the check once the startup mods have loaded, and noting which startup mods were discovered then. Falls back to 10 s, and then no startup mod counts as failed for not having started. | Low. |
| `ModManifest.GetIds()` | Telling an empty Steam answer (timeout) from no subscriptions. | Medium, `ModsInternal`. |
| `Object.FindAnyObjectByType<ModsPanel>()`, `<UploadModPanel>()` | Waiting while the Mods panel or the Mod Creator is open. | Low to medium: if the panel's object stays active when closed, turning on waits for good, after one line in `Player.log`. |
| `OptionsService.Register`, `.RemoveModOptions`, `ModOptions` | The Mods page. | Low, the documented mod API. |
| `ModOptionsButtonControl.Initialize(ModOption)`, native caption child, `TextLocalizationComponent.SetData`, localization properties, `Behaviour.enabled`, `TMP_Text.text` | Localize this pack's two button captions, which build 3682 leaves as prefab placeholders. Clear leftover formatting on clones or a prefab component, retain localization on language changes, and restore the prior caption when our control is rebound to another option. | Medium. Scoped by callback assembly and label; each distinct caption failure logs once. Native screenshot acceptance required. |
| `LocalizorManager.RegisterExternalLocalization`, `.UnregisterExternalLocalization`, `.LoadedLocale` | The changing status lines. | Low to medium. If it breaks, the page shows keys instead of text and `Player.log` still has the lines. |
| `Notifications.Show`, `Notifications.onShow` | The notices. A notice waits until the game has a notification list. | Low. |
| `PlayerPrefs`, `Application.persistentDataPath` | Remembering hidden notices and turned-on items; the locale folder. | Low. |
| `ModOptions.AddToggle`, `AddSplitter`; `OptionsService.RegisteredEntries`, option callbacks; `MainMenuController.IsInitialized` | Integrated rule controls and the main-menu-only 75-value preset. | Medium. Offline SettingsCheck checks real option IDs and page resolution. |
| `CustomGameOptionsHandler`, `CustomGamePanel`, `GameVariables` | Series custom-game prefill. | Medium. Every hook/field checked by SettingsCheck. |
| `SaveGameManager.New`, `GlobalEvents.InvokeOnGameLoaded`, bank and calendar classes | Indebted scenario and retained save markers. | High. ScenarioCheck and native save acceptance required. |
| `Building.GetBuildingDailyMarketRent`, `EstimatedWeeklyIncomeHelper` | Optional rent rule. | Medium. ScenarioCheck guards the rival estimate exception. |
| `HappinessHelper` methods and modifier registry | Happiness values and hotel refresh. | Medium. ScenarioCheck and reviewed RLO gate. |
| `PlayerItemPurchaser.GrabItem`, `PurchaseUI.PlaceOrder`, RLO catalogue/save members by reflection | Factory diplomas and held records. | High. FactoryCheck, RloGateCheck and reviewed hash required. |
| `TasksUI.GetTodoDescription`, closure and business fields; RLO utility members by reflection | Read-only BizMan reasons and running costs. | High. CostCheck, CompatibilityCheck and reviewed hashes/shape checks required. |
| `AudioSource.mute`, `.clip`, `Object.name`; RLO `LeisureSound.Tick(float)`, `_instance`, `_layer`, `Crickets` and `Layers` by reflection | Mute only the exact generated cricket source and restore its prior mute state on option-off/unload. | High. Reviewed RLO hash, exact shape and clip identity required; NightSoundCheck and Core restoration tests. |
| `GlobalEvents.onSaveGame`, `UnityLifecycleProvider.OnUpdate`, `GameInstance` clock and `modData` | Optional companion report in ordinary saves; reattach after the game resets events. Only `hartmodpack:companion` is written. | Medium. CompanionCheck covers relay/reset/ownership and report nonmutation; native save acceptance pending. |
| `BuildingHelper.GetBuilding`, `Address(street, number)`, `Building.BuildingType` | Exclude residential accounts from companion equipment adjustments, matching RLO home classification. | Medium. Pure dictionary lookup; synthetic home exclusion check. |
| `ItemsGetter.AllItems`, `GetByName(name, true)`, `ItemInstance._itemCached` by reflection, furniture roots/requirements | Read-only equipment and cafe station projections without warming live item caches. | High. Exact native requirements audited; unknown requirements make that observation unavailable. CompanionCheck and CostCheck cover synthetic projections. |
| RLO utilities `Instance`, `Failed`, `State`, `_save`, accounts, `Prices.Level`, `GameCalendar.MonthDays` by reflection | Conditional daily office seat component, already included in RLO Internet accrual. | High. Read only after the existing reviewed-binary gate; missing shape reports unknown, never zero. |
| `AppDomain.AssemblyLoad`, `UnityLifecycleProvider.OnUpdate`, Harmony patch/unpatch | Target discovery, duplicate refusal and gameplay cleanup. | Medium. Both assembly load orders and ownership checks required. |

The behaviour these calls rely on, from reading the decompile of build 3682 (22 September 2026): `SteamModDownloader.EnsureModInstalled` adds an item to the manifest only when it downloaded the item itself; discovery runs Workshop and `ModsLocal` mods as two separate groups, so the duplicate-name check never compares a Workshop copy with a local one, and the second copy then gets the first one's DLL and starts a second instance of its mod class; `ModLifecycleLoader.LoadScopeAsync` replaces the start failures with the discovery failures each time a scope loads; and closing the Mods panel loads every mod added to the manifest while it was open.

## Build and test

- `dotnet test tests/HARTModPack.Core.Tests.csproj`: the pure rules in `Core/`.
- `tools/build.sh`: runs the tests, compiles `HARTModPack.dll` with the .NET SDK's C# compiler against the game's assemblies, and packages `build/HARTModPack/`, `build/HARTModPack-1.1.1.zip` and their SHA-256 files. Game DLLs come from `BA_GAME_DLLS`, else `../big-ambitions-hard-mode-patch/build/game-dlls`.
- `tools/check-api.sh [Managed folder]`: resolves every game reference in the built DLL, and the members it reads by reflection, against the build 3682 Windows DLLs and, when present, this Mac's Steam install.
- `dotnet run --project tools/VersionProbe -- <ModsLocal or Workshop content folder>`: prints each mod's assembly name and versions, for updating the tested-version table.

## Evidence

**Merged 1.1.0:** offline validation only; new native acceptance remains pending. The dated checks below used the earlier separate DLLs. Caption acceptance also needs page rebuilds, language changes and RLO key-binding rows: cloned rows inherit localization components but do not inherit the original control's weak-table tracking.


Checked means seen in the game. Code reading and offline checks are not checks.

**Checked, 23 October 2026:** the isolated Mac rig loaded Campaign Objectives 1.2.1 with the renamed HART. Thriving Rivals 0.2.0 and HART. Mod Pack Patch 0.5.0. All 74 series settings persisted after restart, the custom game prefill matched the new world, the indebted start ran once, and the world passed midnight and saved/reloaded with matching money, debt and settings. See [the integration record](docs/integration-2026-10-23-campaign-1.2.1.md) for the exact scope and rig limitations. This was a headless test with the established rig bridge and existing City Performance 0.5.0 candidate, not a final Windows/Workshop or complete latest-member acceptance.

**Offline, 22 October 2026:** the Core tests pass. `HARTModPack.dll` compiles against the build 3682 Windows DLLs (`BigAmbitions.dll` SHA-256 `9427561b…e911`), and every game reference, plus the reflected Steam members, resolves against those and against the Mac Steam install. Assembly names confirmed from the agent rig's `ModsLocal` and the Mac Workshop content folder.

**Pending:**

- Load the package from `ModsLocal` on the agent rig: Mods page, status lines, the check notice and its click, the reminder, and no Steam step without Steam.
- Peter's clean Windows profile, installing from the Workshop only: Steam's prompt for required items, the pack turning members on, the restart notice, and a full pack with no check notice.

**Unverified:**

- Everything that needs Steam: the reflected subscribed listing, `ModManifest.Add` taking effect after a restart, and how often the pack itself starts switched off. If Steam downloaded HART. Mod Pack while the game was closed, the game leaves it off too, and it can't turn itself on; the player turns it on once in Main menu > Mods.
- Whether the main menu has a notification list, or the notices wait for a city. The main menu's load screen and options call `Notifications.Show`, which suggests it has one.
- The double-load and failure-clearing readings above, and that the Mods panel's and Mod Creator's objects carry the `ModsPanel` and `UploadModPanel` components and are inactive while closed.
- The full final member set on Windows, including City Performance after its pending main-branch release preparation.

## Licence

MIT for this mod's code, see `LICENSE.txt`. The pack members belong to their authors and keep their own terms; this repository contains none of their files and no game files.

## Series economy, 1.1.1

Apply HART series settings in HART. Mod Pack to enable the Economy Expansion-inspired equipment utilities and office IT rules on RLO. The button explicitly switches off the former flat 2x/3x rent increase. Existing leases retain their stored rent; new leases use the game's market-rent path. The button writes these settings only when pressed at the main menu, and Economy Expansion is not a required item.

**Release session 2, 23 October:** the updated pack passed the native Mac series-settings/new-game/midnight/save-reload flow with Campaign Objectives 1.2.1. All 74 settings persisted, including rent 1 to 0; all 13 fresh City Performance defaults matched. Twelve native RLO billing fixtures passed. The complete rig backup was restored (3,144 files, preferences and install manifest verified). Windows/Workshop, real business settlement and visual checks remain pending. See [release session 2](docs/release-session2-2026-10-23.md).

## Optional companion report

The public read-only `HARTModPack.Companion.CaptureV1(GameInstance)` and normal-save report support offline and live companion readers. See [the version 1 contract](contracts/companion/v1/README.md). The report describes current ownership, rent rules, equipment/office running costs, factory purchase requirements and supported retail-demand inputs. RLO owns current education quotes and account/tariff facts. Native registered prices remain native observations.

The normal-save publisher runs outside gameplay, so a legacy conflict publishes the blocked state. Only its own `hartmodpack:companion` save line changes. Ordinary reports are captured facts with clock validation, not proof that the provider remains installed. Pure capture never calls gameplay refresh/settlement helpers.

Evidence: synthetic offline checks only. Native gameplay/ordinary-save UI acceptance and Workshop/upstream publication remain pending. Version 1.1.1 includes this optional report; gameplay is unchanged.
