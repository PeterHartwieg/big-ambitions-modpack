# Real Life Overhaul compatibility

> Imported design and historical evidence from HardModePatch 0.5.0. Its implementation now ships in HART. Mod Pack 1.1.0 under `Core/Gameplay` and `src/Gameplay`. Original version, namespace and save-key references below are historical; current migration and acceptance status is in [merge-1.1.0.md](merge-1.1.0.md). City Performance retains performance ownership.

Supports reviewed RLO 0.50.0. Everything HART. Mod Pack does to or with RLO runs only on a reviewed binary and resolved targets. Earlier dated reviews below describe the original separate patch.

## Reviewed builds

| RLO | DLL SHA-256 | Source tag | Reviewed |
| --- | --- | --- | --- |
| 0.45.0 | `2f360a365cf7915f…` | v0.45.0 | 21 October 2026 factory diplomas and running costs; owner job pay and the hotel rule by the 0.49.0 review, their RLO code being the same in both |
| 0.49.0 | `4f82b4f88f9191782d315f97ac0c33f1bda57e8022c072ce281857c762a8191b` | v0.49.0 (commit `63a614a`) | 22 October 2026, all RLO features |
| 0.50.0 | `f339319c9ec1ad65d4b3fa028d673f48e83fd075dff18606f91a662fbae54f5d` | v0.50.0 (commit `850e278`) | 23 October 2026, every integrated RLO feature, BizMan and night crickets |

0.41.1 was reviewed for factory diplomas only and is no longer accepted: running costs were never checked against it.

The prefixes are in `Core/Gameplay/Targets.cs`. A new build needs a review and a new prefix there. BizMan presentation and the new sound adapter accept 0.49.0 and 0.50.0 only; the earlier 0.45.0 gameplay support is retained.

## Review of the companion candidate (23 October 2026)

The local RLO 0.50.0 companion candidate at source commit `631479763e9c9b22956e395d4adb1e75f0bbd213` has DLL SHA-256 `43c4eeed466c714920bc3657c9aeca7bceb4b7244ce6974f07f21fe49469fbce`. This is a separate exact binary approval, not approval of arbitrary builds sharing version 0.50.0. The official release and earlier reviewed versions remain accepted.

The [complete inventory](reviews/rlo-companion-targets.md) compares all 697 previously audited signatures, field shapes, constants and normalized method bodies: **697 unchanged, zero changed or removed, three added**. The additions are `UtilitiesGame.CompanionOutside`, `CompanionTaxLabel` and `CompanionCompanyTaxLabel`; source review confirms they project transaction observations or localized deduction labels without changing utility state. Existing tuition, school routes, catalogue slots, codec limits, utility accrual, outage-start-day exemption, pay, hotel and sound bodies remain unchanged. Existing utility source changed only to a partial type; new owner projections are separate files. The save-hook addition brackets publication with its saving flag and preserves existing handler catches and final cleanup.

The main, utility-closure and night-sound hash lists accept only this reviewed candidate. Intermediate candidate hashes and the same-shape unreviewed fixture remain refused. Producer tests cover read-only projections; the pack's offline compatibility harness separately exercises actual Harmony hooks, courses and held records, billing and IT, save publication and legacy/duplicate takeover. These are offline checks against native binaries; live gameplay, hearing the sound, and a real save/load cycle remain pending.

Candidate validation passed: 222 Core tests; 274/274 API references on both Windows and Mac; 31 accepted and 25 refused companion checks; all eight target hook sets; scenario 38; factory/school 88; costs/BizMan 44; night sound 16; compatibility 30; unknown gate 21; settings 27; legacy-first 12, legacy-late 18 and pack-only/duplicate takeover 14; 47 imported source pins and package integrity. The resulting HART DLL SHA-256 is `847637d73e1e4eb0d04910d990848abfeaa0179e4752a4766b7b7ae79dd26600`. No release version was changed.

## Review of 0.50.0 (23 October 2026)

Official [v0.50.0 release](https://github.com/AdultCudi/real-life-overhaul/releases/tag/v0.50.0), ZIP SHA-256 `8a2bf1737e664622d5f0459b302a701ca62a510505a0d4a58f1f6b2a54675a57`; DLL SHA-256 above, file version 0.50.0.0 and informational version `0.50.0+850e278`. The locally supplied ZIP and DLL hashes match. Source tags resolve to `63a614a94ff107409a0c780fe95eec581ba02d71` and `850e2781b644cee7edea2bd97b11357c8d36bbd4`. The reviewed source is upstream's tagged source, and the tests load the supplied release DLL, not a rebuild.

`tools/RloAudit` compares names, types, field attributes, enum/constant values, static/instance flags, return/parameter types and names, and normalized IL fingerprints. The [complete per-member inventory](reviews/rlo-0.50.0-targets.md) lists every one of its 697 entries. The 0.49.0 DLL had 693: 690 unchanged, three indirect school-offer bodies changed, four school-decision helpers added. Every existing hook and reflection target is unchanged. The tag diff also leaves the complete utility, codec, catalogue, pay, hotel and sound source files unchanged; changed school callers were read separately. No existing gameplay algorithm needed adaptation. Only the main reviewed-hash list and BizMan's narrower allowlist needed the new hash. 0.49.0 remains supported.

Names below start with `RealLifeOverhaul.` unless a game type is explicitly named. Each listed field/method was checked, including the indirect consumers that determine the patch's meaning.

| Feature | Exact target or contract | 0.49.0 to 0.50.0 |
| --- | --- | --- |
| Equipment power/water | `Utilities.UtilitiesRules.AddDay(Account a, long use, int level, int day): void`, static | Signature, parameter names and body unchanged. Prefix still changes only the business use argument. |
| Office IT, $150/computer/month at starting prices (RLO level 1000) | `Utilities.InternetRules.AddDay(Account a, int monthDays, bool outage): void`, static | Signature and body unchanged; daily accrual and outage exclusion preserved. |
| Offices need internet | `Utilities.UtilitiesDirector.NeedsNet(PlaceInfo): bool`, static; `InternetCatalogue.Needs: static string[]` | Unchanged; exactly one load of Needs remains, so the existing transpiler is valid. |
| Utility account reflection | `Account.Place: string`, `Service: Service`, `Kind: PlaceKind`, `State: SupplyState`, `Accrued: int`, all instance fields | Names, types and fields unchanged. Enum values still Power=0, Water=1, Internet=2, Home=0, Active=1. |
| Utility prices, addresses and settlement | `Prices.get_Level(): int`, static; `UtilitiesGame.Place(BuildingRegistration): string`; `UtilitiesDirector.Day(int): void` | Unchanged. Address encoding and one daily call per service/connection remain intact; equipment/opening-hours and IT formulas in the pack are unchanged. |
| BizMan director | `UtilitiesDirector.get_Instance(): UtilitiesDirector`, static; `_save: GameInstance`, `_loaded: bool`, `_failed: bool`, `_toldClosed: Dictionary<string,string>`, instance; `Settings.Utilities: static bool` | All signatures/fields and getter body unchanged. |
| BizMan ownership | `BusinessState.Closures: static List<Closure>`; `Closure.Place: string`, `Day: int`, `Hour: int`, `Source: ClosureSource`; enum Utilities=1; `BusinessState.PlaceKey`, `UtilitiesDirector.TellClosed`, `BusinessDirector.CloseFor` | Unchanged fields, enum values and bodies. Manual/expired/other-save closures remain excluded. |
| BizMan game hook | `UI.Tasks.TasksUI.GetTodoDescription(TodoTask, bool): LanguageChangeEventDataHolder` | Same build 3682 game target, resolved and patched offline; only the generic closure label is replaced. |
| Diploma catalogue | `Careers.StudyCatalogue.All: static readonly CourseDef[]`, `Find(int): CourseDef`, static; `IdsMost: const int`; type initializer | Unchanged. IDs 18, 19, 34 remain free and within the 64-bit mask. |
| Diploma definition | `CourseDef.Id: int`, `Key: string`; constructor `(int,string,CourseKind,Campus,int,int,int,int,int,int,int,int,int,int,int,int,bool,int[],bool)` | All 19 constructor parameters and body unchanged. `CourseKind.Vocational`, `Campus.School`, `Skills.Handling/Electrical/Joinery` and `TalentCatalogue.Visual/Troubleshooting/Diploma` constants unchanged. |
| Diploma availability | `CareersDirector.Offered(CourseDef): bool`, static; `get_Instance(): CareersDirector`, static; `get_Study(): StudyFile`, instance; `_broken: bool`, instance; `Settings.Careers/CareersStudy: static bool` | All unchanged. Existing Offered postfix still hides new enrolments only while the rule is off. |
| School index | `Careers.CampusRules._courses: static int[]`, `get_Courses(): int[]`, static | Unchanged; injection still occurs before the school index or careers singleton is created. |
| Qualification and persistence | `StudyFile.Holds(int): bool`, instance; `StudyRules.LiveMost=4`, `StudyFile.ResultsMost=40`; `CareersStore.Read/Write`, `StudyRules`, `Enrolment`; `TechWriter`, `TechReader`, `TechLines`, `Fields` (source file TechCodec.cs) | All existing signatures, limits, constants and codec/rule bodies unchanged, format v1. Historical shorthand “Tech.TechCodec” names a source file, not a runtime type. |
| School class/exam route | `ClassPlayRules.AtSchool(Enrolment,bool)`, `CareersDirector.AtTheSchool`, `CanAttend`, `CanSit` | Unchanged. All three injected diplomas still route both classes and exams to School. |
| Indirect school offers | `CareersDirector.DeskOfferOf()`, `DeskOfferNow()`, `ToSchoolDesk(Enrolment,SittingKind)` | Signatures unchanged; bodies now use the common desk decision and respect availability/busy-seat reasons. Added `ClassPlayRules.AtDesk`, `Excused`, `BeginWhy`, `BeginSpan`. Compatible with injected school courses; no override needed. |
| Machine purchase game hooks | `Controllers.PlayerItemPurchaser.GrabItem(): void`, `UI.Purchase.PurchaseUI.PlaceOrder(): void`; `_cargoInstances: List<CargoInstance>`, `_cargoInstancesAmounts: Dictionary<int,int>`, `_mode: Mode`, `_type: Type` | Same game build; both fingerprints (`280b3531bdd6f0c5`, `a363d5c1244b1fbe`), reflected field shapes and Harmony injection contracts pass. |
| Owner job pay | `CareerCatalogue.OwnerFactor(int): float`, public static | Signature, parameter name and body unchanged. Full-pay prefix remains valid. |
| Hotel happiness | `TravelState.get_HasRoom(): bool`, static | Unchanged; existing hotel exception remains valid. |
| RloGate | Assembly name `RealLifeOverhaul`, hash prefix; `StudyRules.LiveMost`, `StudyFile.ResultsMost`; game `GlobalEvents.InvokeOnGameLoaded()` and diploma-hold save format | New official hash accepted; unknown hashes still refused before patching. Limits and save shape unchanged. Gate message also names night mute and utility closure reasons. |
| Night sound | `Leisure.LeisureSound.Tick(float): void`, private instance; `_layer: readonly AudioSource[]`, instance; `_instance: LeisureSound`, static; `Crickets=1`, `Layers=4`; `Loop`, `Build`, `Source`, `CricketsLoop`, `Chirp`, `Make`, `OnUpdate` | All signatures and bodies unchanged, including Tick IL fingerprint `b75a3410ca56958a`. New adapter adds a postfix only to Tick. |

Validation against the official 0.50.0 DLL: **210 Core tests**, **262/262 API references on both Windows and Mac**, all eight target mods, scenario **38**, factory/school **82**, cost/BizMan **44**, compatibility **30**, unreviewed gate **21**, night sound **16**, series settings **27** and legacy load-order/unload checks **11/17/13** all pass. Separate 0.49.0 regression runs pass scenario **38**, factory **79**, cost/BizMan **44**, night sound **16**. The settings harness checks RLO option IDs and its explicit missing-live-page skip; it does not claim a live RLO settings-page test. All of these checks are offline.

Reproduce the member inventory by running `dotnet run --project tools/RloAudit -- <Managed> <HARTModPack.dll> <RLO.dll> <snapshot.json>` once per version, in separate processes, then `python3 tools/compare-rlo.py <old.json> <new.json> <report.md>`. Local snapshots and the decompiled cricket class are in `~/Coding_Projects/.agent-logs/rlo-0.50.0/audit/`; upstream source and binaries are not committed.

### What the night chirping is

`LeisureSound.CricketsLoop()` synthesizes the mono clip `RealLifeOverhaul Ambience Crickets`: four seconds at 22,050 Hz, with 4,200 Hz and 3,650 Hz carriers chirping at 2 and 1.5 cycles/second. `Build(1)` assigns it to `_layer[1]`, a looping AudioSource on the child GameObject `RealLifeOverhaul Sound RealLifeOverhaul Ambience Crickets`, routed through the city mixer (foley fallback). `Tick(float)` runs every 0.25 seconds and raises this layer outdoors, without rain, at hours >=21 or <5, using `0.15 * Settings.AmbientNight / 100` and the vehicle cabin factor. This directly identifies the night cricket/chirp signal described by the setting and player complaint; it is unrelated to the smoke-detector text event. Source and official DLL agree. No native listening test is claimed.

**Mute RLO night chirping** defaults on and is explicitly set on by Apply HART series settings. After the normal Tick, the adapter checks the exact array shape and clip name, then sets only that source's `mute` bit. It leaves RLO's volume/fade, playback, saved `ambient_night` value, other ambience layers, cues, engines and all game audio alone. Muting the source also covers RLO's sound-test audition without altering that coroutine. Turning the option off immediately restores the mute value captured before this pack muted it; the same restoration runs on unload, source replacement or legacy-patch stand-down. RLO's changing volume remains current throughout, so there is no stale-volume restoration.

The feature requires a reviewed 0.49.0/0.50.0 binary and exact target shapes. Unknown versions use the shared single RloGate warning. A missing/changed target or clip causes one feature warning, removes its own hook and restores its mute. No repeated attempt/log per tick. Core tests exercise preservation/restoration and source replacement; NightSoundCheck resolves real binaries, rejects six altered shapes and a missing class, applies real Harmony and verifies the sole owner/target and cleanup. Unreviewed-binary refusal is also tested.

## What an unreviewed build does

`src/Gameplay/RloGate.cs` hashes the RLO assembly once when it loads. For a build that isn't in the list:

- One `Player.log` warning, for example: `[HART. Mod Pack] Real Life Overhaul 0.51.0 (1234abcd5678ef90) is a version HART. Mod Pack hasn't reviewed. Factory diplomas, running costs, owner job pay, utility closure reasons, night chirping mute and the hotel 'no home' rule are off until HART. Mod Pack is updated; RLO runs as its author published it. A save's factory diploma records are kept for that update.`
- No Harmony patch on RLO or on the game's machine purchase methods, and HMP's three courses are not added to RLO's catalogue. Machine sales are unrestricted, RLO's own utility bills and owner pay cut apply, and "no home" keeps the game's value. Rent, happiness, the indebted start and the other target patches are unaffected.
- The options page shows Real Life Overhaul as "Not patched: this version hasn't been reviewed yet", and the running costs and owner job pay sections show their "unavailable" line.
- One in-game notice per session when a save's city has loaded (`hardmode_rlo_unreviewed`), if one of those rules is switched on or the save has diploma records to keep. A player who uses none of them sees only the log line.

Such an RLO doesn't know HMP's course ids 18, 19 and 34. Its careers reader drops their records, and its next save would lose a player's diploma enrolments and results. Before RLO first reads the save (RLO reads it from a timer once loading has finished, HMP acts in a game-loaded prefix), HMP copies those records to its own save line, `hardmodepatch:factory_diplomas_held`, with the game day it saw them. RLO's own line is never changed under such a build. Ordinary courses, skills and student debt stay RLO's. The same happens under a reviewed RLO if HMP's courses couldn't be added to its catalogue that session.

While RLO can't read HMP's courses, the hold keeps every record it has and adds HMP's records from RLO's line, so it never drops a record.

When a reviewed RLO and HMP's courses are loaded again, the same prefix settles the hold before RLO reads the save:

- Held records that RLO's line still has as they were held (RLO never rewrote them, for example with its study switched off) are moved on in place. Records are matched one for one, so two identical records, such as two withdrawals from the same course on the same day, stay two.
- The other held records go back together, moved on, if RLO's reader will keep them all: at most 4 courses under way, one per course, and 40 results (read from RLO, unchanged 0.50.0 values as a fallback). RLO's reader checks nothing else for a course it knows, and every record already in the line counts, so the check errs only towards waiting. If any one doesn't fit, none goes back: they wait in the hold, unchanged, and the log says why. All or nothing means two attempts at the same course, even on the same day, are never told apart or split. A new HMP course the player starts meanwhile is RLO's own record and doesn't touch the hold.

The hold is removed once nothing waits. A course under way moves on by whole weeks, at least the days it was held: its start, last counted class and excused day while studying, or its exam window during an exam or retake. Whole weeks keep the course's weekdays, so RLO counts no more classes missed than when it was held and leaves at least the exam days it had left. The course can wait up to six days longer than it was held before its next class. Finished results come back unchanged. Restoring needs RLO's careers line format version to match the hold's (version 1 in 0.45.0 through 0.50.0).

Limits, all with every record kept:

- The hold works only while HMP is installed.
- A full results list blocks the return while the hold still has a finished result to add: RLO 0.50.0 never shrinks a list of 40 results, so such a save keeps those records waiting for good, even a single pass, and HMP logs that on each load. Making room would mean HMP removing one of RLO's own results, which it doesn't do. Held courses under way alone can still come back.
- A hold with two courses under way of the same course waits for good. That needs the player to restart the course under a reviewed RLO while an older attempt is held, and then to run an unreviewed RLO again.
- A held course under way waits while the same course is under way again in RLO's line, until that attempt ends. If it ends with a pass, the held attempt comes back as a second, live attempt at a course already passed.
- A brand-new record identical field for field to a held one is taken for the held copy: the save keeps one of the two, and that one is moved on by the weeks held. That needs the same course started on the same day as the held attempt.

## Review of 0.49.0 (22 October 2026)

Release `v0.49.0` from github.com/AdultCudi/real-life-overhaul: `RealLifeOverhaul-0.49.0.zip`, SHA-256 `8855279ebb94057850adf0a6c9463ed3f07d90cd98509299c010ab673712f697`, matching the digest GitHub publishes for the asset. The DLL inside is the one hashed above. The source was read at tag `v0.49.0`, diffed against `v0.45.0` (983 files changed); the release DLL was not rebuilt from source.

Every RLO member HMP touches, and what changed:

| Feature | RLO members | 0.45.0 to 0.49.0 |
| --- | --- | --- |
| Factory diplomas | `Careers.StudyCatalogue` (`All`, `Find`, `IdsMost`, `Skills`), `CourseDef` constructor, `StudyFile.Holds`, `StudyRules`, `CampusRules._courses`/`Courses`, `CareersStore` (save codec), `Tech.TechCodec` | No change. Ids 18, 19, 34 are still free. |
| | `CareersDirector.Offered`, `Instance`, `Study`, `_broken`; `Settings.Careers`, `CareersStudy` | Unchanged members. The director now builds the Campus app only once the phone exists and adds an interview visit; neither touches the catalogue or study record. |
| | `Tech.TalentCatalogue` constants | One new role constant (`FinancialAnalyst = 29`); the skill and `Diploma` constants HMP reads are unchanged. |
| Running costs | `Utilities.UtilitiesRules.AddDay`, `InternetRules.AddDay`, `Account` fields, `InternetCatalogue.Needs`, `Prices.Level`, `UtilitiesGame.Place` | Code unchanged apart from comments and line wrapping. `InternetRules.Down` now reports a closed plan as a closed account rather than "no plan"; HMP doesn't use it. |
| | `UtilitiesDirector.NeedsNet` and the nightly billing loop | Moved to the partial file `UtilitiesDirector.Internet.cs`; same body, same single read of `Needs`. The loop still calls `UtilitiesRules.AddDay` once per place and service a day and `InternetRules.AddDay` once per connection. |
| Owner job pay | `CareerCatalogue.OwnerFactor(int)` | No change. |
| Happiness | `TravelState.HasRoom` | No change. |
| Indebted start | none patched; RLO's loan tracking is read by `ScenarioCheck` | Finance apps changed only their text box call. |

Offline checks against the 0.49.0 DLL, all passing: HookCheck 10/10 RLO members and both purchase contracts, ScenarioCheck 38, FactoryCheck 76 (the hold settled through RLO's own reader, writer and study rules: classes counted missed and exam days left after holds of 1 to 56 days, records RLO never rewrote, a full results list with two same-day attempts waiting, a new HMP enrolment while they wait under a reviewed and an unreviewed RLO, then all given back), CostCheck 21. RloGateCheck 17 against the same DLL with one byte appended, so every shape matches and only the review is missing. None of this ran in the game.

## Upstream

The data loss on an unknown course id is RLO's reader dropping records it can't place. A PR description for RLO to keep them is in [rlo-upstream-unknown-course-records.md](rlo-upstream-unknown-course-records.md); it isn't opened.

## Utility closure presentation review (23 October 2026)

The original display adapter was separately restricted to the exact reviewed 0.49.0 hash prefix `4f82b4f88f919178`; 0.45.0's existing features remain supported, but its closure presentation is unchanged. Reviewed against the released DLL: `UtilitiesDirector.Instance`, `_save`, `_loaded`, `_failed`, `_toldClosed`; `Settings.Utilities`; `BusinessState.Closures`; `Closure.Place/Day/Hour/Source`, with `ClosureSource.Utilities = 1`. `BusinessState.PlaceKey` matches the existing escaped street/number key. `TellClosed` records recognized reasons only after `BusinessDirector.CloseFor` accepts utility ownership; manual closures are refused, and utility ownership can coexist with an issue. Release, expiry, disabled utilities, a failed director and another save must not inherit a stale description. The game hook is `TasksUI.GetTodoDescription(TodoTask, bool)`; only its generic alert key is replaced. Native field/guard regression checks are in CostCheck; unknown-hash refusal is in RloGateCheck.
