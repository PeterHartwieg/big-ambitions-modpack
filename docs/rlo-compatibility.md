# Real Life Overhaul compatibility

> Imported design and historical evidence from HardModePatch 0.5.0. Its implementation now ships in HART. Mod Pack 1.1.0 under `Core/Gameplay` and `src/Gameplay`. Original version, namespace and save-key references below are historical; current migration and acceptance status is in [merge-1.1.0.md](merge-1.1.0.md). City Performance retains performance ownership.

Viewers get each Real Life Overhaul release from the Workshop the day it ships, and HART. Mode Patch can't hold them back. So everything HMP does to or with RLO runs only on an RLO build whose code was reviewed for it, and any other build turns those features off.

## Reviewed builds

| RLO | DLL SHA-256 | Source tag | Reviewed |
| --- | --- | --- | --- |
| 0.45.0 | `2f360a365cf7915f…` | v0.45.0 | 21 October 2026 factory diplomas and running costs; owner job pay and the hotel rule by the 0.49.0 review, their RLO code being the same in both |
| 0.49.0 | `4f82b4f88f9191782d315f97ac0c33f1bda57e8022c072ce281857c762a8191b` | v0.49.0 (commit `63a614a`) | 22 October 2026, all RLO features |

0.41.1 was reviewed for factory diplomas only and is no longer accepted: running costs were never checked against it.

The prefixes are in `Core/Targets.cs`. A new build needs a review and a new prefix there.

## What an unreviewed build does

`src/RloGate.cs` hashes the RLO assembly once when it loads. For a build that isn't in the list:

- One `Player.log` warning, for example: `[HART. Mode Patch] Real Life Overhaul 0.50.0 (1234abcd5678ef90) is a version HART. Mode Patch hasn't reviewed. Factory diplomas, running costs, owner job pay and the hotel 'no home' rule are off until HART. Mode Patch is updated; RLO runs as its author published it. A save's factory diploma records are kept for that update.`
- No Harmony patch on RLO or on the game's machine purchase methods, and HMP's three courses are not added to RLO's catalogue. Machine sales are unrestricted, RLO's own utility bills and owner pay cut apply, and "no home" keeps the game's value. Rent, happiness, the indebted start and the other target patches are unaffected.
- The options page shows Real Life Overhaul as "Not patched: this version hasn't been reviewed yet", and the running costs and owner job pay sections show their "unavailable" line.
- One in-game notice per session when a save's city has loaded (`hardmode_rlo_unreviewed`), if one of those rules is switched on or the save has diploma records to keep. A player who uses none of them sees only the log line.

Such an RLO doesn't know HMP's course ids 18, 19 and 34. Its careers reader drops their records, and its next save would lose a player's diploma enrolments and results. Before RLO first reads the save (RLO reads it from a timer once loading has finished, HMP acts in a game-loaded prefix), HMP copies those records to its own save line, `hardmodepatch:factory_diplomas_held`, with the game day it saw them. RLO's own line is never changed under such a build. Ordinary courses, skills and student debt stay RLO's. The same happens under a reviewed RLO if HMP's courses couldn't be added to its catalogue that session.

While RLO can't read HMP's courses, the hold keeps every record it has and adds HMP's records from RLO's line, so it never drops a record.

When a reviewed RLO and HMP's courses are loaded again, the same prefix settles the hold before RLO reads the save:

- Held records that RLO's line still has as they were held (RLO never rewrote them, for example with its study switched off) are moved on in place. Records are matched one for one, so two identical records, such as two withdrawals from the same course on the same day, stay two.
- The other held records go back together, moved on, if RLO's reader will keep them all: at most 4 courses under way, one per course, and 40 results (read from RLO, 0.49.0 values as a fallback). RLO's reader checks nothing else for a course it knows, and every record already in the line counts, so the check errs only towards waiting. If any one doesn't fit, none goes back: they wait in the hold, unchanged, and the log says why. All or nothing means two attempts at the same course, even on the same day, are never told apart or split. A new HMP course the player starts meanwhile is RLO's own record and doesn't touch the hold.

The hold is removed once nothing waits. A course under way moves on by whole weeks, at least the days it was held: its start, last counted class and excused day while studying, or its exam window during an exam or retake. Whole weeks keep the course's weekdays, so RLO counts no more classes missed than when it was held and leaves at least the exam days it had left. The course can wait up to six days longer than it was held before its next class. Finished results come back unchanged. Restoring needs RLO's careers line format version to match the hold's (version 1 in 0.45.0 to 0.49.0).

Limits, all with every record kept:

- The hold works only while HMP is installed.
- A full results list blocks the return while the hold still has a finished result to add: RLO 0.49.0 never shrinks a list of 40 results, so such a save keeps those records waiting for good, even a single pass, and HMP logs that on each load. Making room would mean HMP removing one of RLO's own results, which it doesn't do. Held courses under way alone can still come back.
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

The new display adapter is separately restricted to the exact reviewed 0.49.0 hash prefix `4f82b4f88f919178`; 0.45.0's existing features remain supported, but its closure presentation is unchanged. Reviewed against the released DLL: `UtilitiesDirector.Instance`, `_save`, `_loaded`, `_failed`, `_toldClosed`; `Settings.Utilities`; `BusinessState.Closures`; `Closure.Place/Day/Hour/Source`, with `ClosureSource.Utilities = 1`. `BusinessState.PlaceKey` matches the existing escaped street/number key. `TellClosed` records recognized reasons only after `BusinessDirector.CloseFor` accepts utility ownership; manual closures are refused, and utility ownership can coexist with an issue. Release, expiry, disabled utilities, a failed director and another save must not inherit a stale description. The game hook is `TasksUI.GetTodoDescription(TodoTask, bool)`; only its generic alert key is replaced. Native field/guard regression checks are in CostCheck; unknown-hash refusal is in RloGateCheck.
