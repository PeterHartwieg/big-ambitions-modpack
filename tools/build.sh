#!/usr/bin/env bash
# Builds HART. Mod Pack with the .NET SDK's C# compiler against the game's own assemblies, runs the Core tests
# and packages the result.
# Usage: tools/build.sh   Output: build/HARTModPack/ (the folder that goes into ModsLocal or the Mod Creator),
# build/HARTModPack-<version>.zip, build/HARTModPack.sha256 and build/HARTModPack-<version>.zip.sha256.
# Game DLLs: BA_GAME_DLLS, else the HART. Mode Patch checkout's build/game-dlls (filled by its tools/fetch-game-dlls.sh).
# Game DLLs are never committed or packaged. Harmony is pinned and packaged under Dependencies/.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
GAME_DLLS="${BA_GAME_DLLS:-$REPO/../big-ambitions-hard-mode-patch/build/game-dlls}"
HARMONY_URL="https://api.nuget.org/v3-flatcontainer/lib.harmony/2.4.2/lib.harmony.2.4.2.nupkg"
HARMONY_SHA256="d64592e53090464559fce48612c9ca7c8dc73113841376b7aa3455f46fc5d579"
HARMONY_DLL_SHA256="7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c"
DEPS="$REPO/build/deps"
OUT="$REPO/build/HARTModPack"

[ -f "$GAME_DLLS/BigAmbitions.dll" ] && [ -f "$GAME_DLLS/mscorlib.dll" ] || { echo "Game DLLs not found in $GAME_DLLS; set BA_GAME_DLLS" >&2; exit 1; }

dotnet test "$REPO/tests/HARTModPack.Core.Tests.csproj" --nologo -v quiet

# Harmony, checksum-pinned.
mkdir -p "$DEPS"
if [ ! -f "$DEPS/0Harmony.dll" ]; then
  tmp="$(mktemp -d)"
  curl -fsSL "$HARMONY_URL" -o "$tmp/harmony.nupkg"
  echo "$HARMONY_SHA256  $tmp/harmony.nupkg" | shasum -a 256 -c - >/dev/null
  unzip -q -o "$tmp/harmony.nupkg" 'lib/net472/0Harmony.dll' LICENSE -d "$tmp"
  cp "$tmp/lib/net472/0Harmony.dll" "$DEPS/0Harmony.dll"
  cp "$tmp/LICENSE" "$DEPS/Harmony-LICENSE.txt"
  rm -rf "$tmp"
fi
echo "$HARMONY_DLL_SHA256  $DEPS/0Harmony.dll" | shasum -a 256 -c - >/dev/null || { echo "Cached 0Harmony.dll does not match the pinned hash; delete it and rebuild" >&2; exit 1; }

# The newest installed SDK's Roslyn compiler.
SDK_DIR="$(dotnet --list-sdks | tail -1 | sed -E 's/^([^ ]+) \[(.*)\]$/\2\/\1/')"
CSC="$SDK_DIR/Roslyn/bincore/csc.dll"
[ -f "$CSC" ] || { echo "C# compiler not found at $CSC" >&2; exit 1; }

rm -rf "$OUT"
mkdir -p "$OUT/Locales" "$OUT/Dependencies"
REFS=()
for dll in mscorlib System System.Core netstandard BigAmbitions BigAmbitions.Items DayNightCycle BigAmbitions.ModAPI BigAmbitions.ModsInternal HGExtensions HGPlugins UnityEngine.CoreModule UnityEngine.AudioModule Unity.TextMeshPro; do
  REFS+=("-reference:$GAME_DLLS/$dll.dll")
done
REFS+=("-reference:$DEPS/0Harmony.dll")
# The game runs on Mono with its own mscorlib, so nothing from the SDK's framework is referenced.
dotnet "$CSC" -nologo -noconfig -nostdlib+ -target:library -langversion:9 -optimize+ -warn:4 -warnaserror+ -deterministic+ \
  -out:"$OUT/HARTModPack.dll" "${REFS[@]}" "$REPO"/Core/*.cs "$REPO"/Core/Gameplay/*.cs "$REPO"/src/*.cs "$REPO"/src/Gameplay/*.cs

python3 "$REPO/tools/package.py"
