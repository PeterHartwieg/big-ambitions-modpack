# Advanced factory diplomas

> Imported design and historical evidence from HardModePatch 0.5.0. Its implementation now ships in HART. Mod Pack 1.1.1 under `Core/Gameplay` and `src/Gameplay`. Original version, namespace and save-key references below are historical; current migration and acceptance status is in [merge-1.1.0.md](merge-1.1.0.md). City Performance retains performance ownership.

`factory_diplomas` is off by default under Settings > Mods > HART. Mode Patch > Real Life Overhaul: advanced factories. Enable it for diploma requirements on new machine purchases. Machines already owned keep working. The Laser Cutting Machine and Consumer Goods Assembly Machine have no new requirement; the game's own manufacturing course is unchanged.

## Courses

All three are ordinary RLO vocational diplomas at Manhattan Business School: eight four-hour classes, four homework pieces, then RLO's three-hour exam with a 50% pass mark. Their full-time timetables have three classes a week, about three weeks including the exam. RLO also offers its normal part-time Saturday timetable, which takes longer and can clash with another part-time course. There are no prerequisites or distance-study mode. They count as RLO's diploma schooling and use its existing fee, grant, student-loan, learning, attendance, retake and result rules.

| Id | Diploma / machine | Full-time classes | Base fee | Existing skills gained |
| --- | --- | --- | --- | --- |
| 18 | Garment making / Industrial Sewing Machine | Mon, Wed, Fri 09:00–13:00 | $3,600 | Visual +35, Handling +20 |
| 19 | Electronics technician / Kiln Machine | Tue, Thu, Sat 09:00–13:00 | $3,900 | Electrical +45, Troubleshooting +10 |
| 34 | Jewelry and goldsmithing / Polishing Machine | Mon, Wed, Fri 13:00–17:00 | $3,800 | Visual +35, Joinery +20 |

RLO's Electrician diploma is seven four-hour classes, three homework pieces and $3,200. These add a class and homework piece for a longer advanced-production qualification, with a modest fee premium. Garment making costs least; electronics and jewelry cost more for specialist electrical and precision finishing instruction. Joinery is RLO's existing handcraft skill; no new skill ids are introduced. Fees are base prices: RLO applies its usual price and fee adjustments. The three full-time schedules do not overlap and use hours already covered by RLO's school diplomas.

## Runtime integration

HMP adds `RealLifeOverhaulPatch` to its initialization-time assembly watcher. It requires a reviewed RLO build, **0.45.0** or **0.49.0** (SHA-256 prefixes `2f360a365cf7915f`, `4f82b4f88f919178`; 0.41.1 was accepted until 22 October), exact reflected member shapes, and the reviewed game purchase-method bodies. Unknown RLO builds are left unrestricted even if their method names still match; HMP then holds a save's diploma records until a reviewed build is back. See [RLO compatibility](rlo-compatibility.md). No RLO files are modified or referenced at compile time.

`ResolveHooks` lists all three patched methods plus the RLO methods/constructor used by the bridge. The RLO types are under `RealLifeOverhaul.Careers`:

- HMP initializes `StudyCatalogue.All`, constructs three native `CourseDef` objects, then writes one extended array reference to that static readonly field using a checked dynamic setter. It first verifies all ids are free and below `StudyCatalogue.IdsMost` (64). The released binary also leaves 18, 19 and 34–39 unused. Existing definitions, array elements and ordering are preserved; the new definitions are appended. Failure leaves the gate off.
- This happens during HMP's initialization callback or the target's assembly-load callback, before RLO's city-load `CareersDirector` starts. Installation refuses a live director or an already populated `CampusRules._courses`. RLO's `Find` and `OfCert` scan `All`; its Campus app and education rows also enumerate it directly. The only course-index cache found is `CampusRules.Courses`, built lazily for **university** courses; these school additions do not change those rooms. RLO's catalogue static constructor only builds its original array, with no dependent index.
- When a second copy of the pack takes over in the same session (Workshop and `ModsLocal` both on), it finds the catalogue this pack wrote still in place and reuses it: the exact array reference and the three owned definitions must still match. It reinstalls the purchase and offering hooks without writing the catalogue again, so the live director and campus index checks above apply only to the first registration. A catalogue changed since registration, or ids taken by a copy that didn't register them, still leaves the gate off. `tools/FactoryCheck` covers both a takeover before careers start and one after.
- A postfix on `CareersDirector.Offered(CourseDef)` hides the three new enrolments while HMP's option is off. Their definitions remain registered so active courses, exams and saved results remain usable. RLO's own offerings are unaffected.
- `CareersDirector.Instance.Study` supplies the player's current `StudyFile`; `StudyFile.Holds(id)` requires a passed result. HMP does not grant qualifications, advance classes or write separate education records. RLO's `StudyRules.Passed` includes the ids in its native 64-bit mask.
- The Campus text keys are `reallife_study_course_{Key}` and `reallife_study_about_{Key}`; HMP supplies names, descriptions and refusal messages in every shipped locale (currently English only). The course keys start with `hmp_` to avoid conflicting with RLO's own text.

Game hooks:

- Prefix `Controllers.PlayerItemPurchaser.GrabItem()` refuses collecting a sale item without its matching diploma, before unpaid cargo is created. `ItemController.Interact` routes sale displays here. The three exact `ba:itemname_...` keys identify the Factory Supply Depot's machines at 2 25th Street.
- Prefix `UI.Purchase.PurchaseUI.PlaceOrder()` checks the actual selected basket immediately before it invokes the shop's order callback. It handles normal baskets and `SingleToggle` quantities, ignores unselected entries, and leaves tax/ticket modes alone. A refusal leaves money, cargo and queue/order state unchanged. This also catches a machine collected before enabling the option. It does not hook machine placement or production.

The checkout goes through `CashRegisterController.OnPlaceOrder` and eventually `Order.Pay`. A prefix on `Order.Pay` alone would be insufficient: some callers continue updating the purchase without checking its return value. The earlier `PurchaseUI.PlaceOrder` hook keeps both the purchase and its UI state unplaced. An order already submitted before enabling the option is not retroactively cancelled.

## Fail-safe and saves

The gate requires the option, intact injection, RLO careers and study options, an unfaulted director and a live study record. Missing RLO, a changed shape/binary, occupied id, late injection, lost catalogue, stopped study or reflection failure leaves sales unrestricted. Each runtime reason is logged once under `[HART. Mode Patch]`; compatibility/install failures use HMP's existing one-time target status/log path. A reflection failure latches the gate off for the session. Option and RLO-setting changes are read at each purchase; dependency recovery does not require restarting unless installation itself failed or the bridge faulted.

RLO's `CareersStore` serializes courses and results by id under `reallife:careers`. Its reader resolves an enrolment through `StudyCatalogue.Find`, so injection precedes save reading. The ordinary course/result/fee format, student debt and save version are unchanged.

- **Option off, HMP still installed:** the gate opens immediately and new enrolments are hidden. Definitions remain, so enrolled students can finish and active/completed records survive save/load. Re-enabling restores the requirement and offerings.
- **HMP unloaded while the process runs:** the gate is removed; definitions remain until process exit so live RLO records remain valid. Restart for a clean catalogue.
- **Unreviewed RLO build, HMP installed:** the records are copied to `hardmodepatch:factory_diplomas_held` before RLO reads the save and given back when a reviewed RLO is loaded again ([RLO compatibility](rlo-compatibility.md)).
- **HMP removed before starting the game:** unmodified RLO safely skips the unknown course records when reading. Ordinary courses, skills and student debt remain readable. Once RLO resaves, the skipped diploma enrolments/results are lost; reinstalling HMP can only recover them from a save that still contains them. Tuition is not refunded and outstanding student debt is not forgiven. Removing RLO itself leaves HMP's gate off and does not make HMP write RLO data.

## Validation and playtest

`FactoryCheck` loads the released RLO binary and real game assemblies without launching Unity. It verifies course shapes and free ids, exact hooks/contracts, native enrolment eligibility, class accounting/exam/result, masks, active and completed save-codec roundtrips, option changes, mixed/quantity-selected checkout, the actual patched pickup and checkout refusals, and fail-open states. Its notification sink replaces display only. Engine objects are inert fixtures, not a live save. Core tests cover definitions, schedules, collisions, every dependency combination and the gate decision table.

```bash
tools/build.sh
dotnet test tests/HardModePatch.Core.Tests.csproj
dotnet run --project tools/FactoryCheck -- build/game-dlls build/HardModePatch/HardModePatch.dll /path/to/RealLifeOverhaul.dll
tools/check-hooks.sh /path/to/target-dlls
dotnet run --project tools/ScenarioCheck -- build/game-dlls build/HardModePatch/HardModePatch.dll /path/to/RealLifeOverhaul.dll
dotnet run --project tools/PerformanceCheck -- build/game-dlls build/HardModePatch/HardModePatch.dll /path/to/target-dlls
```

Offline results: `tools/build.sh` and the core test assembly build completed with **0 warnings and 0 errors**. All **108 core cases** passed through the existing in-process runner; ordinary `dotnet test` was attempted but VSTest could not bind its communication socket in the sandbox. **57 FactoryCheck**, **38 ScenarioCheck** (RLO 0.41.1) and **49 PerformanceCheck** checks passed. HookCheck resolved/applied all hooks and matched all contracts for the five available targets (Bakery, Hyperreal, Internet Cafe, Meaningful Ambience and RLO, including RLO's 10/10 members). Hardcore Ambitions, Empire Bank and Vending Machines DLLs were unavailable and explicitly skipped. No installed mod or real save was changed.

Pending in a disposable in-game save: enable the option; confirm all three Campus names, descriptions, tuition and timetables; enrol and physically attend school classes/homework/exam; save/reload mid-course and after passing; attempt each depot purchase before/after its qualification, including a mixed basket collected before enabling; check laser purchases and an already-owned advanced workstation; switch HMP and RLO study/careers off/on. Check the log for early registration before RLO study loads. The dynamic readonly-field write has passed offline .NET execution; the game's Mono lifecycle, live localization, notification, school access and desk activity still need this playtest. No claim of in-game validation is made.
