# Plan

> Imported design and historical evidence from HardModePatch 0.5.0. Its implementation now ships in HART. Mod Pack 1.1.1 under `Core/Gameplay` and `src/Gameplay`. Original version, namespace and save-key references below are historical; current migration and acceptance status is in [merge-1.1.0.md](merge-1.1.0.md). City Performance retains performance ownership.

A public Workshop mod with a standalone indebted start and optional runtime balance patches. Revised 15 October for Peter's RLO-led life-simulation pack: borrowing is allowed subject to realistic debt/leverage balance, and Hardcore Ambitions is dropped from that pack. The six older patches remain available for other configurations. The reviewed target versions are in `Core/Targets.cs`; the original reviews are in the HART repository. In-game validation is partial; each section records its limits.

## Ownership split, 22 October 2026 (0.4.2 candidate)

Peter: performance belongs to City Performance; HMP ensures mods work together and implements the intended pack mechanics. The paired HMP 0.4.2 / CP 0.4.4 candidate moves the entire coupled Bakery Help feature to CP and removes HMP's old Hyperreal lamp cache. CP's reviewed full unsorted lamp query replaces that algorithm; it requires a new native comparison. HMP retains price rules and all map-return lifecycle hooks. Old performance keys remain inert for downgrade, and CP refuses legacy overlapping hooks in either load order. See [handoff, settings and validation](performance-ownership.md). No installation is implied.

The dated 0.4.1 sections below describe the previous implementation and historical checks; they are not current feature ownership or release instructions.

## Standalone scenario, implemented in 0.2.0

- Explicit option, off by default, selected before creating a new game. The pack enables `scenario_indebted_start=1`. This standalone feature is not an enabled-by-default balance patch; existing installations retain their patch-only behavior.
- `SaveGameManager.New(GameVariables)` postfix records the choice; `GlobalEvents.InvokeOnGameLoaded()` prefix creates the starting package after bank assets load and before RLO's first reading. Every patched method is listed by `IndebtedScenario.ResolveHooks` and checked offline.
- $551.37 cash, one native $75,454.35 loan, no loan payout or investment purchases, no difficulty/catalogue mutations. Native bank/calendar/difficulty formulas define the contract; the game and RLO handle later repayments, penalties and debt arrangements.
- Completion persists after payoff/refinance, so reload cannot reset cash or recreate debt. Untagged saves are never converted. Failure/conflict is recorded and reported rather than retried after play begins.
- 67 core tests and 24 offline scenario checks pass, including released RLO 0.24.0 and the locally available 0.20.0. The older six-target hook check still passes. Engine lifecycle, real bank assets, Finance UI and midnight/save settlement remain pending.
- First live run, 18 October: game-loaded also fires when character creation opens, before the city has building registrations, so no bank was found and the save was marked failed. The prefix now waits while there are no registrations and fails only on a loaded city without a bank (`IndebtedStart.ShouldWaitForCity`, 70 tests). Second live run pending.
- This replaces the need for Hardcore's start in the HART pack. Keep the old optional Hardcore patch unchanged for users of that mod; it still retains that mod's fixed terms. No RLO methods or installed DLLs are patched by the standalone scenario.

## Rent rebalance, implemented in 0.3.0

Peter, 18 October 2026: rent is the lever, not deposits. Option `rent_rebalance`, off by default. The original pack enabled it; the 23 October release decision below supersedes that preset.

- For leases the player signs, flats cost twice and every other building type three times the game's market rent (`Core/RentRules`). Deposits keep the game's days per building type, so they rise with the rent. A lease already signed keeps its rent.
- Hook: postfix `Buildings.Building.GetBuildingDailyMarketRent`. The rent screen, the deposit, BizMan and the rent stored at signing all read it, so what the player sees is what they pay.
- Rivals: `EstimatedWeeklyIncomeHelper.GetEstimatedWeeklyIncome` subtracts a week of the same market rent. A prefix/finalizer bracket keeps the game's rent while it runs for a business the player neither rents nor owns, so rival survival, the leaderboard and Rival Balance are unchanged.
- Checks: 78 core tests; 6 offline checks in `ScenarioCheck` (hooks resolve and apply, off/on scaling, the rival estimate exception). Not yet seen in the game.
- To watch: AI businesses established later store the scaled market rent in their registration (`BuildingRegistration` reset); nothing found charges or estimates rivals from it. Retail Expansion writes its own rent for upgrades and isn't scaled.

## Happiness rebalance, implemented in 0.4.0

Peter, 18 October 2026: home gives at most about 60, long-lasting outside activities the other 40, so a mix of activities beats a routine. Option `happiness_rebalance`, off by default; the HART pack turns it on. Values in `Core/HappinessRules`, extracted vanilla values in the HART research notes.

- **Home, banked:** shower +15 (48 h max); TV, video games, reading, exercise +8 each, banking the game's own hours per hour up to 96 h. 47 in all, plus RLO's small home moods.
- **Outside, one visit fills the duration:** cinema/theatre +10 (5 days); golf, tennis, casino +10 (7 days); swimming, nightclub, pier party, DJing, dancing in your own club +8 (5 days); park, beach, cemetery walks +5 each (4 days); pier rides +5 (4 days); yacht night +10, boat night +8.
- **Small:** luxury car night +5, donating +5. While doing an activity, its "-ing" boost counts the same as its result.
- **No home:** −15 instead of −50 while RLO's `TravelState.HasRoom` says a hotel room is booked. RLO is read by name and never patched; without it −50 stands.
- **Hooks:** postfix `HappinessHelper.OnHappinessModifiersLoaded` sets absolute values (no compounding on a second load; the game's values are kept and restored when the rule is switched off); prefix `AddModifier` gives an outside result its whole duration; prefix `UpdateHappiness` sets the no-home amount. RLO's own modifier types are never in the list.
- **Checks:** 89 core tests; 8 offline checks (hooks resolve and apply, values set once, outside grants, home banking, no home in and out of a hotel against RLO 0.35.0). Not yet seen in the game. The tutorial's 60-day "a fresh start" +100 only appears with the tutorial on; the series plays with it off.

## Historical hitch fixes in 0.4.1 (removed/moved in 0.4.2)

Peter's exported 18 October trace has recurring ~89 ms frames 1.08 seconds apart during Bakery's Help retry window, and ~111 ms frames 8.01 seconds apart matching Hyperreal's street-light discovery. These are timing/code matches, not yet an A/B result.

- Bakery: replace only its global Help-object discovery with the game's registered singleton (no missing-instance search). Run Bakery's own page insertion before `HelpSystem.LoadCategories`, suppressing its redundant deferred refresh within that call. This also covers first opening after the retry timeout, language changes and Help reloads. Keep its prices and page content in their existing code paths.
- Hyperreal: seed a light cache once on demand, including inactive lights; update it from HDRP light enable/destroy callbacks, and invalidate on scene load/unload or option changes. Its existing eight-second scan consumes that cache and retains its own lighting/restoration/foliage logic. New inactive lights are discovered when first enabled. Scene transitions still incur an initial discovery; foliage discovery when new street lamps appear remains a possible hitch.
- Independent default-on options; reviewed Bakery/Hyperreal and game/Unity Core/HGExtensions/HDRP fingerprints required for the applicable performance hooks. Unknown builds keep their original discovery. Log discovery counts, cache activity and measured scan duration without per-frame log spam. City Performance remains the frame-trace recorder for the A/B playtest.
- 94 core tests, 32 focused offline performance checks, and all seven target hook/contract checks pass. Astra and Sol reviewed; fixed optional Help compatibility so it cannot disable Bakery pricing, including a partial-install rollback, and added a one-time Help population when enabling after the retry timeout. The harness checks both fixes and that the pre-patch delegate reaches patched discovery. In-game Help entries, day/night lights, switching options, save reload and frame-time gains remain playtest checks.

## Hyperreal map return, implemented 19 October (unreleased; awaiting playtest)

- Peter reports occasional pitch-black evening/night rendering after closing the map, sometimes relieved by zoom. [Diagnosis](hyperreal-map-return.md): the leading hypothesis is Hyperreal's fixed-to-histogram exposure-history handoff, at moderate confidence. No persistent CPU restore defect or lamp-cache cause was found. The log has no marked occurrence or GPU-state capture.
- Extend the existing `HyperrealPatch` with default-on `hyperreal_map_return_fix`. Postfix `EnhancementVolume.Ensure` records the author's actual camera; postfix `Apply` requests one HDRP `resetPostProcessingHistory` only after both `GlobalEvents.onCityMapClosed` and zero map blend, outdoors in AutomaticHistogram mode. No exposure values or lighting values are forced.
- `MapMode.Reset`, scenes, option changes and `GlobalEvents.Init` clear pending state. Postfix `GlobalEvents.Init` reattaches the deduplicated map-close handler because the game clears it on load. Revert removes subscriptions. Exact reviewed Hyperreal `7d1bd589947558e2`, game, Unity Core and HDRP hashes remain required; all eight touched/referenced methods have body contracts in `ResolveHooks`/`Contracts`.
- Checks: build passes; 8/8 Hyperreal members and contracts resolve and Harmony applies (Bakery also passes; other target DLLs were absent). 49 focused offline checks cover both discovery patches and map-return guards/lifecycle, using inert engine objects. 38 ScenarioCheck checks pass against RLO 0.38.0. Core test assembly compiles; VSTest cannot bind its communication socket in this sandbox, so all 103 Fact/InlineData cases were invoked successfully in-process instead. This does not validate rendering.
- Astra and Sol reviewed diagnosis and implementation; their lifecycle/camera findings were fixed. The local notes also request Opus, which is unavailable in this session. Keep release version at 0.4.1 until Peter chooses a release number; no mandatory bump rule was found. No installation or game/save changes made.

## Advanced factory diplomas, implemented 20 October (unreleased; awaiting playtest)

- Option `factory_diplomas`, off by default. Three native RLO vocational school diplomas, ids 18, 19, 34: eight four-hour classes, three a week, four homework pieces and an exam. Base fees $3,600 garment making, $3,900 electronics technician, $3,800 jewelry and goldsmithing. Existing skills; full-time schedules do not overlap.
- Runtime-only injection into reviewed RLO 0.41.1 or 0.45.0's `StudyCatalogue.All` before city careers/save reading; `CareersDirector.Offered` hides new enrolments while the option is off. `StudyFile.Holds` supplies the qualification. RLO's own class, exam, masks and save codec handle progress and results.
- Game prefixes: `PlayerItemPurchaser.GrabItem` and `PurchaseUI.PlaceOrder`, before unpaid cargo or checkout state changes. Only the three exact machine keys are gated; laser, plain workstation and owned production remain available. No RLO files changed.
- Missing, changed, stopped or faulted dependencies leave sales unrestricted, logging once per reason. Option off retains all records; HMP removal lets RLO skip unknown diploma records safely, losing them on resave without clearing ordinary courses, skills or student debt.
- See [full design, compatibility boundary, validation and playtest](factory-diplomas.md). Build/core, FactoryCheck and existing harness results are recorded there; live Campus, school activities, Mono initialization and save/depot behavior remain unverified. Version stays 0.4.1.

## Running costs, implemented 22 October (unreleased; awaiting playtest)

Peter, 22 October 2026: the pack dropped Economy Expansion for RLO; bring back the two EE pressures RLO lacks, costs that follow how a business is run and expensive office infrastructure. The 23 October decision replaces the pack's flat rent increase with these running costs: the preset explicitly writes `rent_rebalance=0`. The optional rent rule remains unchanged for standalone users; existing signed leases keep their stored rent. Options `equipment_utilities` and `office_it`, off by default; the HART pack turns both on.

- **Equipment.** Prefix RLO `UtilitiesRules.AddDay(Account, long use, int level, int day)`, the one place a day of power or water is added to an account. For a business account at a place the player rents or owns: `Core/RunningCosts.DayUse`: RLO's open-day figure splits into its standby quarter (its "fridges, alarms") and the open part, which scales with the day's scheduled hours (12 h = RLO's figure, 24 h = twice the open part). Placed refrigeration and security replace the standby when they draw more; placed plumbing replaces the open water (RLO's "basins") when it uses more; cooking, machines and IT add on top (`Core/RunningCosts.Of`, a table of vanilla keys from the Big Copilot item list, flag fallbacks for other mods). Homes untouched. RLO keeps billing, due days, late fees, cuts and price level.
- **Offices.** Transpile RLO `UtilitiesDirector.NeedsNet` so its single read of `InternetCatalogue.Needs` returns RLO's list plus every game office type while the rule is on; its hourly closing and BizMan reason follow, and its grace too if the rule was on when the save first met RLO's internet rule; switched on mid-save, unconnected offices close at the next hourly check. Postfix `InternetRules.AddDay(Account, int monthDays, bool outage)`: an office's active connection, not on an outage day, accrues `SeatMonthCents` ($150 at start prices × RLO's price level) per computer-flag item (computer, desktop, gaming PC, laptop) over the game month. RLO's own tech firms are excluded: RLO already charges them software per seat. EE closed offices without its plans and cut capacity without cloud/software; this uses RLO's existing connection rule instead of a capacity cut.
- Places and per-place equipment are cached per save and game day. RLO is patched by name on assembly load, before its books first run; a missing member or a changed `NeedsNet` leaves RLO's bills. The 0.5.0 RloGate now requires a reviewed RLO hash as well as names, signatures and the single `Needs` read; unreviewed RLO keeps its own bills. Review each RLO update.
- **Reviews:** Opus, Astra and Sol. Round 1: All three found the standby/basin double count (fixed with `DayUse`); Astra and Sol flagged tech firms without desk IT (kept: RLO charges them), and an overstated refusal claim (narrowed). Defaults stay off like the other own rules; the pack turns them on. Round 2: all three agree it is ready for the in-game playtest.
- **Checks:** 133 core tests (20 new); `tools/CostCheck` 21 checks against released RLO 0.45.0 (`2f360a365cf7915f`), with Harmony applied to RLO's own methods. Scenario 38, factory 57 and hook checks unchanged. Not yet seen in the game.
- **To watch in a test save:** the first month's bill in RLO's Utilities app for a food shop and an office, a 24-hour business, a factory, and the BizMan closed reason for a law firm without a connection. Calibrate `SeatMonthCents` and the table against real income if the pressure is too light or heavy.

## Planned, not in 0.5.0: Alcware Seasons follows RLO's calendar

Peter's decision, 17 October: keep both seasonal mods in the HART pack and make Alcware Seasons follow RLO's calendar. Not written yet.

**Why.** The two mods do different jobs. Alcware Seasons draws the season (snow, settled snow, leaves, summer storms, warm outfits, the badge under the date) and changes demand for 27 products through `Item.productSalesRatio`, which reaches rival estimates too. RLO's seasons draw nothing. They drive its heat waves, cold snaps and ice, wholesale supply shocks, travel, property, online sales and summer jobs. Their calendars disagree. In a 60-day year:

| Days | RLO 0.32.0 | Alcware Seasons (15 days a season) |
| --- | --- | --- |
| 1–11 | Winter | Spring (1–15) |
| 12–26 | Spring | Spring to 15, Summer from 16 |
| 27–41 | Summer | Summer to 30, Autumn from 31 |
| 42–56 | Autumn | Autumn to 45, Winter from 46 |
| 57–60 | Winter | Winter |

Only 16 of 60 days agree. Without the patch, Day 1 is spring in the picture and in demand, but a cold snap or ice in RLO.

**Who leads.** RLO. Its calendar scales with the save's year length (`daysPerYear`, 60 to 365) and many RLO systems read it. Alcware's calendar counts fixed-length seasons from Day 1 and has no option to shift them.

**Hook.** Prefix `AlcwareSeasons.SeasonState.Phase(int day)` and return RLO's phase in Alcware's season order (Spring 0, Summer 1, Autumn 2, Winter 3): the season index plus the fraction of that season gone. `CurrentSeason` and `DayOfSeason` derive from `Phase`, so every caller follows: demand (`SeasonController` :1470), snow (:1552), leaves (:594), storms (:1842), surface weather (:2021), clothing (:366) and the badge (:492–504). Line numbers are from the decompile of the installed DLL, hash prefix `d56a88d2c5aff831`, the reviewed version.

**Reading RLO without patching it.** At startup, resolve `RealLifeOverhaul.CalendarRules.Season(int day, int year)` by name with `AccessTools`. It is game-free. Per year length, scan the year once with it and cache each season's first day and length, including winter wrapping over the year's end. That follows RLO's own thresholds through its updates without copying them. No RLO method is patched. If RLO or the method is missing, or the signature has changed, skip this patch, log it and leave Alcware's own calendar.

**Season length option.** The badge reads `[day/DaysPerSeason]`, and `DayOfSeason` scales by that field. RLO's seasons are about a quarter year each: exactly 15 days each in a 60-day year, up to a day off at other lengths. When the save's Seasons length differs from the RLO season it is in, log a warning and show it on the options page: "Set Seasons' length to N days". Don't write Alcware's setting. Changing it re-anchors its calendar and marks the save changed.

**Option.** `seasons_follow_rlo`, on by default, on the Mod Options page. Off restores Alcware's calendar at once, because nothing is saved.

**Checks.**
- Core rule and tests: the mapping from a season function to Alcware's phase for 8, 60, 61, 120 and 365-day years. Cover winter's wrap, every season boundary, and a phase always in [0, 4).
- Offline: add Seasons to the hook and fingerprint check. Extend `ScenarioCheck` (or a sibling tool) to load the selected RLO DLL, call its `CalendarRules.Season` and compare the cached table for every year length from 8 to 365.
- In game, disposable save with both mods: Day 1 shows Winter on the badge and in RLO's weather line in `Player.log`. Skip to Day 12: Spring on both, snow gone, spring demand. Check the badge count and a save and reload.

**Unchanged.** The series goal stays 15 consecutive days at #1 on the leaderboard, an explicit number of days, not "one season".

## Working assumptions

- **Name:** working title "HART. Mode Patch". This repository, MIT. Built with the .NET SDK's C# compiler against the game's own assemblies (`tools/build.sh`), Harmony 2.4.2 in `Dependencies\`.
- **One patch per target mod, found at runtime.** The patch never references the target DLLs at compile time; it finds their types by name with `AccessTools.TypeByName`, so a viewer missing a target mod loses only that patch.
- **Two levels of trust.** A target whose DLL hash matches a reviewed version is *verified*. A new hash whose hooked methods all still exist with the same signatures is *unverified*: the patch applies, logs a warning and shows the mod as unverified on the options page. A missing method or changed signature skips that target and says so. Target authors update on their own schedule and the Workshop can't pin versions, so this keeps viewers balanced while a new version is reviewed.
- **Every rule has an option** on a native Mod Options page, defaulting to the hard values below, so viewers can turn single rules off.
- **No files written outside the mod folder, no network.**

## Patches

| Target | Change | Hook (decompiled names) |
| --- | --- | --- |
| Hardcore Ambitions | Keep the $75,454 student loan, $551 cash and the vehicle and boat prices. Skip the 23 `GameVariables` writes and the loan-rate recalculation, so the save keeps its custom difficulty. Skip the three investment funds. | Prefix `HardcoreAmbitionsPrototypeMod.EnforceHardcoreGameVariables` → skip. Prefix `EnsureStartingReserve` → set cash to $551.37, set its `StartingReserveInitialized` flag, return true. |
| Hardcore Ambitions | Vehicle prices stop leaking into other saves in the same session. | Prefix `ApplyCatalogPriceTargets`: snapshot catalogue prices before the first write. On loading a save without `HardcoreAmbitions.SaveEnabled`, restore the snapshot. |
| Meaningful Ambience | Decor adds a share of the customers the game sends, not a share of building capacity. Preset 10% means +10% at most, so the save's 0.1 customer promotion, marketing and opening-hour factors carry through. | Postfix `GameBridge.ProjectRetail`: `AmbienceRaw = VanillaRaw × score × bonus`, then recompute `CombinedRaw`, `FinalTarget`, `AddedAfterCapacity` and the blocked counts the same way the method does. Offices already scale with game clients. |
| Bakery | Prices at vanilla bakery margins. | Postfix `ItemsGetter.RegisterModItem` for `bakeryxx:` products: set `wholesalePrice` and `defaultMarketPrice`. Defaults below. |
| Internet Cafe | Off screen, at most 2 customers per PC per hour (30-minute sessions). | Postfix `RetailBusinessSimulator.GetMaxCustomerCapacityThisHour` when the simulator is `InternetCafe.CafeBusinessSimulator`: cap at 2 × the PCs placed in the building. |
| Vending Machines | Placing a machine in a rival building no longer lets you pick up the rival's furniture. | Prefix `ItemController.SecondaryInteract` and `TryToGrabItem`: while Vending's `RivalPlacement._raised` and the game's `isBuildingTemporarilyEditable` are both on in a building the player doesn't rent, allow only `vendingmachines:` items. The developer console's EditBusiness still works on its own. |
| Empire Bank | Withdrawals are taxed at the save's rate when taken. | Prefix `BigAmbitionsPlayerEconomyGateway.TryChangePlayerMoney`: positive amounts whose reason mentions a withdrawal × (1 − tax %). The licence-upgrade rollback refund stays untaxed. The game taxes business income from registrations, not transactions, so re-labelling the transaction would not tax it. |
| Empire Bank | No emergency central-bank borrowing. | Prefix `EmpireBankEmergencyCard.OnRequest` → skip, with a notice. |
| Empire Bank | No 850 minimum-credit trick. | Transpiler on `LoanApplicationService.Create` and `CreditCardApplicationService.CreateApplication`: the `Math.Max(product.MinimumCreditScore, BellCredit(rng))` call becomes a call that keeps the drawn score. |
| Empire Bank | Loan APR ≤ 15%, fee ≤ 3%, term ≥ 6 months; card APR ≤ 30%. | Prefixes on `EmpireBankRuntime.UpdateLoanProduct` and `UpdateProduct` refuse edits beyond the limits with a message; postfix on `AdjustProductRate` clamps the rate. |
| Empire Bank | No contact with the author's servers. | Prefixes skip `EmpireTelemetryClient.FetchStatsIfStale` and `EmpireBankSupportersView.StartRemoteRefresh`. The opt-in heartbeat stays the author's. |

### Bakery prices

Margins per unit close to the vanilla bakery and coffee items (croissant $4.29, cupcake $3.10, donut $2.28, coffee $3.00):

| Item | Wholesale | Market | Margin | Was |
| --- | ---: | ---: | ---: | --- |
| Bread | 3.00 | 7.50 | 4.50 | 12.00 / 50.00 |
| Bun | 2.00 | 5.00 | 3.00 | 8.00 / 28.00 |
| Chocolate cookie | 1.50 | 3.80 | 2.30 | 5.00 / 20.00 |
| Latte | 1.10 | 4.50 | 3.40 | 2.00 / 8.20 |
| Americano | 0.90 | 3.90 | 3.00 | 3.50 / 9.00 |

`ProductMarketEntry` stores only a price index, not prices, so the market reads the patched defaults. A business that already set its own selling prices keeps them (they're per business in the save).

## Not patched

- **Hardcore Ambitions' student loan terms** stay the mod's fixed $16 a day interest and $158 a day payment. The save's bank interest setting doesn't reach it.
- **Bakery's logo copy** into the game install stays. It is the game's intended folder for modded logos; the README tells viewers how to remove the files.
- **Vending Machines' own-building rule** (no fee or share) stays. Machines in your own shop are furniture you paid for.

## Order

1. Scaffold: entry class, target gate, options page, build and install scripts, tests for the pure logic. Done 8 October 2026.
2. Patches in order of risk to the series: Hardcore Ambitions, Meaningful Ambience, Internet Cafe, Bakery, Vending Machines, Empire Bank. All written 8 October 2026; `tools/check-hooks.sh` resolves and applies every hook and checks every body fingerprint against the reviewed DLLs. Not yet run in the game.
3. Review loop with three reviewers until they agree, as for the food truck mod. Round 1 (8 October 2026): all three said fix first; fixed in `c36db9c`. Round 2: one ship, two fix first; fixed in `1f59deb` (accepted negotiations, the sign action, body contracts for every Empire Bank hook and the student-loan helpers, exact member and parameter types, the product-limits toggle, pending applications). Round 3: all three said ship to in-game test. Two follow-ups from it are in the next commit: compiler-generated ordinals are masked before fingerprinting, so an unrelated method added to a target class doesn't switch its patch off, and the product-limits toggle does nothing at the main menu. Accepted limitation: products normalised while a save loads aren't marked as a bank change; they are normalised again on every load.
4. Build on homepc, install, and run the test save (HART repository, `docs/research/modpack-compatibility-2026-10-07.md`, "Test save before Episode 1") with the patch: Hardcore start (loan, $551, no funds, custom values intact in the save), an unstaffed one-PC cafe, a decorated shop's ambience ledger, Bakery prices in the wholesale app, a rival-building vending placement, and a second save loaded in the same session showing vanilla car prices.
5. Message each target mod's author before publishing: what the patch changes and why, and which changes they could make themselves (Empire Bank's consent gate, a Bakery price option, Ambience scaling with demand). Each upstream fix retires a patch. Check the Workshop's and Hovgaard's rules on mods that patch other mods.
6. Publish on the Workshop next to Rival Balance.

## Sharing

- Nothing is pushed to other authors' mods. Viewers subscribe to a Steam collection that links the original mods, Rival Balance, Campaign Objectives and this patch; Steam fetches each from its author, and the patch changes their methods in memory.
- Settings stored per PC (Economy Expansion, Dynamic Rivals, Meaningful Ambience presets) go in the collection description and in a Campaign Objectives campaign that lists the recommended values.
- When a target updates: diff its decompiled code against the reviewed version, then add the new hash to the verified list or fix the patch, and publish a patch update.

## RLO 0.49.0 and the review gate, 22 October 2026

The Workshop serves RLO 0.49.0, and viewers get RLO updates automatically. Reviewed and accepted 0.49.0 (`4f82b4f88f919178`); the member-by-member review is in [RLO compatibility](rlo-compatibility.md). Every RLO feature (factory diplomas, running costs, owner job pay, the hotel "no home" rule) now runs only on a reviewed RLO build: `src/RloGate.cs` decides once per assembly, logs one line, shows one notice on save load and holds a save's diploma records (`Core/DiplomaHold`) until a reviewed build is back. Running costs and owner job pay were name-gated before; they are hash-gated now. Checks: 139 core tests, FactoryCheck 63, RloGateCheck 16, CostCheck 21, ScenarioCheck 38 against 0.49.0. Not run in the game.

## Series settings, 22 October 2026

Peter: HMP owns all modpack changes, including the series settings. A button "Apply HART series settings" writes 74 listed Mod Options values (HMP's own rules, Rival Balance's `rival_preset`, Dynamic Rivals, RLO) the way the game's option controls do, logs each change and skips what it can't find; a default-on toggle prefills the custom game screen. The values, sources, omissions and limits are in [series settings](series-settings.md). `tools/export-series-settings.ps1` dumps the series PC's values for comparison. Checks: 150 core tests, SettingsCheck 26. Not run in the game.

Review round 1 (Opus 5.5, GPT 6 Astra, GPT 6.1 Sol, all "fix"): the diploma hold now works per attempt (course and start day), respects RLO's 4-course and 40-result limits (a pass first, the rest wait), and moves a held course's dates on by the days it was held; 0.41.1 is no longer accepted (running costs were never reviewed on it); one notice per session, only when an RLO rule is on or a save has diplomas to keep; the button no longer re-registers pages, stores defaults explicitly and skips callbacks while a save is loaded; the prefill also postfixes `CustomGamePanel.Start` for either Start order. Checks after the round: 152 core tests, FactoryCheck 67, RloGateCheck 17, SettingsCheck 26, the rest unchanged.

Review round 2 (all three "fix"): Astra and Sol found that same-day attempts still collided and that day-exact date moves broke RLO's weekday timetables and exam days; Opus found that values stored in a loaded save still reached it when the Mods page reopened. Now the hold settles in one step, all records or none, within RLO's limits, and a held course moves by whole weeks, at least the days held; records RLO never rewrote are moved in place; the button works at the main menu only (main menu controller registered). Checks: 156 core tests, FactoryCheck 74.

Review round 3 (all three "fix", same finding): a new HMP enrolment while the hold waited made the settle treat the held records as superseded and drop them. Now the hold never drops a record: under an unreviewed RLO it keeps every held record plus the line's, and under a reviewed one it moves verbatim records in place and gives the rest back all together or keeps them waiting. Checks: 157 core tests, FactoryCheck 76.

Review round 4: Opus approved. Astra and Sol found that identical held records (two same-day withdrawals) were matched as a set, so one line copy cleared both; now matched one for one. Astra's stranded-hold case (two held enrolments of one course) and Opus's re-enrolled-course nit are documented open items in [RLO compatibility](rlo-compatibility.md). Checks: 158 core tests.

Review round 5: Opus 5.5, GPT 6 Astra and GPT 6.1 Sol all approve (049f085), with wording nits on the open items, applied.

## Utility closure descriptions (23 October 2026)

`UtilityClosureReasonPatch` post-processes the game's `TasksUI.GetTodoDescription` for the generic business-closed alert. Default-on option `utility_closure_reasons`; pure reason mapping and expiry policy in `Core/UtilityClosureReason`. Reviewed RLO 0.49.0 only, with shape checks for every read field. The adapter requires the current loaded, enabled, healthy UtilitiesDirector, a player-held closed registration, an unexpired Utilities-owned closure and a recognized recorded reason. It preserves the business-name prefix and another mod's specific message. No save/account/task/closure mutations.
