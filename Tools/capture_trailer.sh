#!/usr/bin/env bash
# Capture the raw footage for the trailer: the built game plays the first three cases with simulated
# mouse and keyboard input (Showcase in trailer mode), staging the extra beats, logging a marker
# per beat and saving cursor-free 1920x1080 stills. Then the soundtrack is rebuilt as three stems.
#   Tools/capture_trailer.sh [outdir]      default Captures/trailer (git-ignored)
# Output: video.mp4, markers.txt, stills/*.png, audio.wav (full mix), audio_sfx.wav, audio_music.wav
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/Captures/trailer}"
rm -rf "$OUT"; mkdir -p "$OUT"
timeout 7200 "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" -alibiRecord "$OUT" -alibiRecordCases 3 -alibiTrailer > /dev/null 2>&1 || true
grep -E "\[(Showcase|Record)\]|Exception" "$OUT/player.log" || true
[ -s "$OUT/video.mp4" ] || { echo "no video captured (see $OUT/player.log)" >&2; exit 1; }
PY="$ROOT/.venv/bin/python"; [ -x "$PY" ] || PY=python3
for stem in all sfx music; do "$PY" "$ROOT/Tools/mix_recording.py" "$OUT" --stem "$stem"; done
echo "[capture] $(wc -l < "$OUT/markers.txt") markers, $(ls "$OUT/stills" | wc -l) stills -> $OUT"
