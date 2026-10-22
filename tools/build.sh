#!/usr/bin/env bash
# Builds HART. Mod Pack with the .NET SDK's C# compiler against the game's own assemblies, runs the Core tests
# and packages the result.
# Usage: tools/build.sh   Output: build/HARTModPack/ (the folder that goes into ModsLocal or the Mod Creator),
# build/HARTModPack-<version>.zip, build/HARTModPack.sha256 and build/HARTModPack-<version>.zip.sha256.
# Game DLLs: BA_GAME_DLLS, else the HART. Mode Patch checkout's build/game-dlls (filled by its tools/fetch-game-dlls.sh).
# The game's DLLs are never committed or packaged. No Harmony.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
GAME_DLLS="${BA_GAME_DLLS:-$REPO/../big-ambitions-hard-mode-patch/build/game-dlls}"
OUT="$REPO/build/HARTModPack"

[ -f "$GAME_DLLS/BigAmbitions.dll" ] && [ -f "$GAME_DLLS/mscorlib.dll" ] || { echo "Game DLLs not found in $GAME_DLLS; set BA_GAME_DLLS" >&2; exit 1; }

dotnet test "$REPO/tests/HARTModPack.Core.Tests.csproj" --nologo -v quiet

# The newest installed SDK's Roslyn compiler.
SDK_DIR="$(dotnet --list-sdks | tail -1 | sed -E 's/^([^ ]+) \[(.*)\]$/\2\/\1/')"
CSC="$SDK_DIR/Roslyn/bincore/csc.dll"
[ -f "$CSC" ] || { echo "C# compiler not found at $CSC" >&2; exit 1; }

rm -rf "$OUT"
mkdir -p "$OUT/Locales"
REFS=()
for dll in mscorlib System System.Core netstandard BigAmbitions BigAmbitions.ModAPI BigAmbitions.ModsInternal HGPlugins UnityEngine.CoreModule; do
  REFS+=("-reference:$GAME_DLLS/$dll.dll")
done
# The game runs on Mono with its own mscorlib, so nothing from the SDK's framework is referenced.
dotnet "$CSC" -nologo -noconfig -nostdlib+ -target:library -langversion:9 -optimize+ -warn:4 -warnaserror+ -deterministic+ \
  -out:"$OUT/HARTModPack.dll" "${REFS[@]}" "$REPO"/Core/*.cs "$REPO"/src/*.cs

python3 "$REPO/tools/package.py"
