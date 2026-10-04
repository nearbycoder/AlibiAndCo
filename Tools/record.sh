#!/usr/bin/env bash
# Record a gameplay video: the built game plays the cases with simulated mouse input while it
# captures every frame (game time locked to 30 fps), then the soundtrack is rebuilt from the voice
# log and muxed in.
#   Tools/record.sh [out.mp4] [cases]     default Recordings/alibi_gameplay.mp4, all cases
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/Recordings/alibi_gameplay.mp4}"
CASES="${2:-99}"
WORK="$(mktemp -d /tmp/alibi-record.XXXX)"
mkdir -p "$(dirname "$OUT")"
timeout 7200 "$ROOT/Tools/play.sh" -logFile "$WORK/player.log" -alibiRecord "$WORK" -alibiRecordCases "$CASES" > /dev/null 2>&1 || true
grep -E "\[(Showcase|Record)\]|Exception" "$WORK/player.log" || true
[ -s "$WORK/video.mp4" ] || { echo "no video captured (see $WORK/player.log)" >&2; exit 1; }
PY="$ROOT/.venv/bin/python"; [ -x "$PY" ] || PY=python3
"$PY" "$ROOT/Tools/mix_recording.py" "$WORK"
ffmpeg -y -loglevel error -i "$WORK/video.mp4" -i "$WORK/audio.wav" -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart "$OUT"
echo "[record] $OUT ($(du -h "$OUT" | cut -f1)), work files in $WORK"
