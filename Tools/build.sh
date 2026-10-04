#!/usr/bin/env bash
# Build the Linux player. Uses the resident Editor if one is serving this project (Tools/editor.sh serve),
# otherwise a one-shot batch Editor (Tools/unity.sh build-linux).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
if unity status --format json 2>/dev/null | grep -q "\"project\": \"$ROOT\""; then
  unity command --result-only editor_stop > /dev/null 2>&1 || true
  unity command --timeout 1800 --result-only eval -- --code 'return AlibiCo.EditorTools.BuildScript.BuildLinuxResident();' --timeout 1800000 | grep -o '\[Build\][^"]*'
else
  LOG="$ROOT/Logs/build.log" "$ROOT/Tools/unity.sh" build-linux; grep -o '\[Build\].*' "$ROOT/Logs/build.log"
fi
