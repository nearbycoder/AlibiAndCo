#!/usr/bin/env bash
# Runs the built game (Tools/play.sh, so any self-test) inside a private nested KWin: its own Wayland
# socket, D-Bus session and config folder, closed afterwards. The window is certainly on screen there,
# so it runs at its real frame rate (a hidden or covered window on a shared desktop can be throttled
# to about 10 fps), it can't go fullscreen on the real desktop, and the real pointer can't reach it.
# Player prefs land in the scratch config folder too, never in ~/.config/unity3d.
#
#   Tools/nested.sh [--size WxH] [--keep] [play.sh args...]
#   Tools/nested.sh -alibiPadTest "$PWD/Captures/pad" -logFile "$PWD/Logs/pad.log"
#   Tools/nested.sh --size 1280x800 -screen-width 1280 -screen-height 800 -alibiResolution 1280x800 -alibiKeysTest ...
#
# --size is the nested desktop's size (default 1920x1080). --keep leaves the scratch folder
# (Captures/nested-<pid>/, KWin's log) for a look afterwards. The exit code is the game's. Only the
# processes started here are stopped, by PID.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SIZE=1920x1080; KEEP=0
while [ $# -gt 0 ]; do
  case "$1" in
    --size) SIZE="$2"; shift 2 ;;
    --keep) KEEP=1; shift ;;
    *) break ;;
  esac
done
for tool in kwin_wayland dbus-run-session; do
  command -v "$tool" > /dev/null || { echo "nested.sh: needs $tool (KDE's KWin and D-Bus)" >&2; exit 2; }
done
[ -n "${XDG_RUNTIME_DIR:-}" ] || { echo "nested.sh: XDG_RUNTIME_DIR isn't set" >&2; exit 2; }

SCRATCH="$ROOT/Captures/nested-$$"
rm -rf "$SCRATCH"; mkdir -p "$SCRATCH/config"
export ALIBI_NESTED_SOCK="alibi-nested-$$" ALIBI_NESTED_DIR="$SCRATCH" ALIBI_ROOT="$ROOT"
export ALIBI_NESTED_W="${SIZE%x*}" ALIBI_NESTED_H="${SIZE#*x}"
# Everything inside (KWin's settings, the player's prefs) reads and writes the scratch folder.
export XDG_CONFIG_HOME="$SCRATCH/config"

set +e
dbus-run-session -- bash -c '
  kwin_wayland --virtual --socket "$ALIBI_NESTED_SOCK" --width "$ALIBI_NESTED_W" --height "$ALIBI_NESTED_H" \
    --no-lockscreen --no-global-shortcuts > "$ALIBI_NESTED_DIR/kwin.log" 2>&1 & KW=$!
  trap "kill \$KW 2>/dev/null; wait \$KW 2>/dev/null" EXIT
  for i in $(seq 40); do [ -S "$XDG_RUNTIME_DIR/$ALIBI_NESTED_SOCK" ] && break; sleep 0.25; done
  [ -S "$XDG_RUNTIME_DIR/$ALIBI_NESTED_SOCK" ] || { echo "nested.sh: KWin did not start (see $ALIBI_NESTED_DIR/kwin.log)" >&2; exit 3; }
  echo "[nested] kwin $KW on $ALIBI_NESTED_SOCK (${ALIBI_NESTED_W}x$ALIBI_NESTED_H)"
  WAYLAND_DISPLAY="$ALIBI_NESTED_SOCK" "$ALIBI_ROOT/Tools/play.sh" "$@" & G=$!
  echo "[nested] game $G"
  wait $G; code=$?
  echo "[nested] game exited $code"
  exit $code
' nested "$@"
code=$?
set -e
# Nothing started here may outlive it.
left=$(pgrep -f "$ALIBI_NESTED_SOCK" || true)
[ -z "$left" ] || echo "[nested] warning: still running with this session's socket: $left" >&2
[ "$KEEP" = 1 ] || rm -rf "$SCRATCH"
exit $code
