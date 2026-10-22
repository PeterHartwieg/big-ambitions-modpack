# HART. Mod Pack

The Workshop item for the HART modded series pack. It finds the pack members, turns on the ones the game left off and reports what's missing or untested. Read `README.md` first; the release plan is `../hart-group/docs/series-2/modpack-release-plan.md` and the member data is `../hart-group/mods/modpack/modpack.json`.

## Layout

- `Core/`: pure C#, netstandard2.0, no game or Unity types. Member list, tested versions, turn-on rules, status, notice texts and policy.
- `tests/`: xUnit tests for Core.
- `src/`: thin game adapters: the mod entry, the Workshop step, the scan, the Mods page and the notices.
- `config/Locales/en.json`: the fixed option and notice keys. The status lines are written at runtime.
- `tools/`: `build.sh` (test, compile, package), `package.py`, `check-api.sh` with `ApiCheck/`, and `VersionProbe/`.
- `workshop/WORKSHOP.md`: paste-ready Mod Creator fields and the Required items checklist. Not packaged.

## Rules

- Build with `tools/build.sh`, then run `tools/check-api.sh`. Test with `dotnet test tests/HARTModPack.Core.Tests.csproj`. Both must pass before a commit that changes code.
- Never commit game DLLs, other authors' mod DLLs, decompiled game code or save files. The build references the game DLLs by path only.
- No Harmony. Use the game's public classes. Every new game call gets a row in the README's API table with its update risk.
- Find members by assembly simple name, never by Workshop ID. Confirm a new member's name with `tools/VersionProbe` on an installed copy.
- Never switch a mod off, never write to `ModsLocal` or a Workshop folder, and never turn on Persistent City or an optional member.
- Core decides, the game only shows. Pure logic and every player-facing sentence go in `Core/` with a test.
- For each pack release, update `Pack.Version`, the assembly attributes in `src/HARTModPackMod.cs`, `Core/TestedVersions.cs` and its pinning test, `CHANGELOG.md` and `workshop/WORKSHOP.md` together. `package.py` refuses mismatched versions.
- Mark evidence in the README as checked (seen in game), pending or unverified. Code reading and offline checks are not checks. The Steam paths can't be tested on the Mac rig.
- Plain words, sentence-case headings, active voice, no em dashes, in docs and in shipped text.
