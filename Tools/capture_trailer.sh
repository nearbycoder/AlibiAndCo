#!/usr/bin/env bash
# Capture the raw footage for the trailer: the built game plays the first three cases and a Daily
# Docket with simulated mouse and keyboard input (Showcase in trailer mode), staging the extra beats,
# logging a marker per beat and saving cursor-free 1920x1080 stills. Then the soundtrack is rebuilt
# as three stems.
#   Tools/capture_trailer.sh [outdir]      default Captures/trailer (git-ignored)
#   FIDELITY=3 DOCKET=2026-10-08 Tools/capture_trailer.sh
# FIDELITY is the graphics fidelity step to record at (0 Low .. 3 Ultra, default 3): the recorder steps
# the game's clock one frame at a time, so Ultra costs capture time, not smoothness. DOCKET is the day
# whose Daily Docket is played (default 2026-10-08), so the capture doesn't change with the date.
# It runs inside a private nested KWin (Tools/nested.sh), never on the desktop it's started from.
# Output: video.mp4, markers.txt, stills/*.png, audio.wav (full mix), audio_sfx.wav, audio_music.wav
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/Captures/trailer}")"
FIDELITY="${FIDELITY:-3}"
DOCKET="${DOCKET:-2026-10-08}"
rm -rf "$OUT"; mkdir -p "$OUT"
timeout 7200 "$ROOT/Tools/nested.sh" -logFile "$OUT/player.log" -alibiFidelity "$FIDELITY" -alibiDocketDate "$DOCKET" \
  -alibiRecord "$OUT" -alibiRecordCases 3 -alibiTrailer > "$OUT/nested.log" 2>&1 || true
grep -E "\[(Showcase|Record)\]|Exception" "$OUT/player.log" || true
grep -E "\[Fidelity\]" "$OUT/player.log" || true
[ -s "$OUT/video.mp4" ] || { echo "no video captured (see $OUT/player.log and $OUT/nested.log)" >&2; exit 1; }
PY="$ROOT/.venv/bin/python"; [ -x "$PY" ] || PY=python3
for stem in all sfx music; do "$PY" "$ROOT/Tools/mix_recording.py" "$OUT" --stem "$stem"; done
echo "[capture] $(wc -l < "$OUT/markers.txt") markers, $(ls "$OUT/stills" | wc -l) stills -> $OUT"
