#!/usr/bin/env bash
# Self-test: the built game plays all three cases through the real session code using the solver's
# moves, writes screenshots to ${1:-/tmp/alibi-autoplay} and prints PASS/FAIL lines.
#   Tools/autoplay.sh [outdir]          screenshots + log
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-/tmp/alibi-autoplay}"
rm -rf "$OUT"; mkdir -p "$OUT"
timeout 900 "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" -alibiCapture "$OUT" > /dev/null 2>&1 || true
grep -E "\[AutoPilot\] (PASS|FAIL|done)|Exception" "$OUT/player.log" || true
! grep -qE "\[AutoPilot\] FAIL|Exception" "$OUT/player.log" && grep -q "\[AutoPilot\] done" "$OUT/player.log"
