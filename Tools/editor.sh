#!/usr/bin/env bash
# Drive the resident Editor (started with `Tools/editor.sh serve`) for quick play-mode checks.
#   Tools/editor.sh serve                 start a headless resident Editor for this project
#   Tools/editor.sh play                  enter play mode and wait until frames advance
#   Tools/editor.sh stop                  exit play mode
#   Tools/editor.sh compile               recompile scripts, print errors
#   Tools/editor.sh eval 'C# code'        run C# in the running game
#   Tools/editor.sh shot name [w h]       render camera + UI to Screenshots/name.png
#   Tools/editor.sh errors                print console errors
#   Tools/editor.sh quit                  close the resident Editor
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
U="unity command --result-only"
case "${1:-}" in
  serve)
    LD_LIBRARY_PATH="$HOME/.local/share/ptt-unity-libs" nohup "$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity" -batchmode \
      -projectPath "$ROOT" -logFile "$ROOT/Logs/serve.log" > /dev/null 2>&1 &
    sleep 20; unity status --until-ready --timeout 600 --format json > /dev/null && echo ready ;;
  play)
    unity status --until-ready --timeout 300 --format json > /dev/null
    $U set_autotick > /dev/null
    $U editor_play
    sleep 4
    unity status --until-ready --timeout 120 --format json > /dev/null
    $U wait_for -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout_s 20 | grep -E '"met"' ;;
  stop) $U editor_stop ;;
  compile)
    unity recompile --format json | python3 -c "import json,sys; d=json.load(sys.stdin)['data']; print('errors:', d['summary']['errors']); [print(e) for e in d['errors']]"
    sleep 2; unity status --until-ready --timeout 300 --format json > /dev/null ;;
  eval) $U eval -- --code "$2" --timeout "${3:-20000}" ;;
  shot) $U eval -- --code "return AlibiCo.DevCapture.Capture(\"$ROOT/Screenshots/$2.png\", ${3:-1920}, ${4:-1080});" --timeout 30000 ;;
  errors) $U console -- --tail "${2:-30}" --level error | python3 -c "
import json,sys
d=json.load(sys.stdin)
for e in d.get('entries',[]): print('-', e['message'][:400]); print('   ', (e.get('stackTrace') or '').strip().replace(chr(10),' | ')[:500])
print(len(d.get('entries',[])), 'error entries')" ;;
  clear) $U clear_console ;;
  # Tools/editor.sh hover-card <cardId>   move the simulated mouse over a card (uses viewport coords)
  hover-card)
    xy=$($U eval -- --code "var v=AlibiCo.GameRoot.I.Session.ViewOf(\"$2\"); var p=AlibiCo.Stage.I.Cam.WorldToViewportPoint(v.transform.position); return (p.x*UnityEngine.Screen.width).ToString(\"0\")+\" \"+(p.y*UnityEngine.Screen.height).ToString(\"0\");" --timeout 10000 | python3 -c "import json,sys; print(json.load(sys.stdin)['result'])")
    $U simulate_pointer -- --x ${xy% *} --y ${xy#* } --action move > /dev/null; echo "$xy" ;;
  pointer) $U simulate_pointer -- --x "$2" --y "$3" --action "${4:-move}" > /dev/null ;;
  quit) $U quit || true ;;
  *) sed -n 2,12p "$0"; exit 2 ;;
esac
