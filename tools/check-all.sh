#!/usr/bin/env bash
# Builds, then runs the core tests and every offline check. Run before a release.
# Usage: tools/check-all.sh <target DLL folder> <unreviewed RealLifeOverhaul.dll> <DynamicRivals.dll> <HARTRivalBalance.dll>
#   The target folder holds the reviewed RealLifeOverhaul.dll and whichever of Bakery, PolyModsHyperreal, InternetCafe,
#   MeaningfulAmbience, HardcoreAmbitions, VendingMachines and EmpireBank are available (HookCheck skips the rest).
#   The unreviewed RLO is a reviewed release with one byte appended: same shapes, unknown hash.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
[ $# -eq 4 ] || { sed -n 3,6p "$0" >&2; exit 2; }
TARGETS="$1"; UNREVIEWED="$2"; DR="$3"; RB="$4"
G="${BA_GAME_DLLS:-$REPO/../big-ambitions-hard-mode-patch/build/game-dlls}"; D="$REPO/build/HARTModPack/HARTModPack.dll"; R="$TARGETS/RealLifeOverhaul.dll"
export BA_GAME_DLLS="$G"
"$REPO/tools/build.sh"
"$REPO/tools/check-api.sh"
BA_COMPANION_RLO="$R" dotnet run --project "$REPO/tools/CompanionCheck" -- "$G" "$D"
BA_COMPANION_RLO="$UNREVIEWED" BA_COMPANION_EXPECT_REFUSED=1 dotnet run --project "$REPO/tools/CompanionCheck" -- "$G" "$D"
"$REPO/tools/check-hooks.sh" "$TARGETS"
for check in ScenarioCheck FactoryCheck CostCheck NightSoundCheck; do dotnet run --project "$REPO/tools/$check" -- "$G" "$D" "$R"; done
dotnet run --project "$REPO/tools/CompatibilityCheck" -- "$G" "$D" "$TARGETS"
dotnet run --project "$REPO/tools/RloGateCheck" -- "$G" "$D" "$UNREVIEWED"
dotnet run --project "$REPO/tools/SettingsCheck" -- "$G" "$D" "$R" "$DR" "$RB" "$REPO/tools/export-series-settings.ps1"

LEGACY="${BA_LEGACY_PATCH:-$REPO/../big-ambitions-hard-mode-patch/build/HardModePatch/HardModePatch.dll}"
for order in legacy-first pack-first pack-only; do
  dotnet run --project "$REPO/tools/MergeCheck" -- "$G" "$D" "$LEGACY" "$order"
done
python3 "$REPO/tools/check-merge.py" "${BA_LEGACY_REPO:-$REPO/../big-ambitions-hard-mode-patch}"
