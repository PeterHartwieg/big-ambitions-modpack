#!/usr/bin/env bash
# Resolves every game type and member HARTModPack.dll uses against game assemblies, without running the game.
# Usage: tools/check-api.sh [Managed folder ...]   Default: the Windows build 3682 DLLs the mod compiles against
# and, when present, this Mac's Steam install. Run tools/build.sh first.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
FOLDERS=("$@")
if [ ${#FOLDERS[@]} -eq 0 ]; then
  FOLDERS=("${BA_GAME_DLLS:-$REPO/../big-ambitions-hard-mode-patch/build/game-dlls}")
  MAC="$HOME/Library/Application Support/Steam/steamapps/common/Big Ambitions/Big Ambitions.app/Contents/Resources/Data/Managed"
  [ -d "$MAC" ] && FOLDERS+=("$MAC")
fi
for folder in "${FOLDERS[@]}"; do
  dotnet run --project "$REPO/tools/ApiCheck" -- "$folder" | tail -1
done
