#!/usr/bin/env bash
# Resolves every patch's hooks against real target mod DLLs, offline. Run tools/build.sh first.
# Usage: tools/check-hooks.sh <folder with target mod DLLs, e.g. a copy of the Workshop content folder>
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
dotnet run --project "$REPO/tools/HookCheck" -- "$1"
