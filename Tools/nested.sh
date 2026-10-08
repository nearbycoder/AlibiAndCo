#!/usr/bin/env bash
# Runs the built game (Tools/play.sh, so any self-test) inside a private nested KWin: its own Wayland
# socket, D-Bus session and config folder, closed afterwards. The window is certainly on screen there,
# so it runs at its real frame rate (a hidden or covered window on a shared desktop can be throttled
# to about 10 fps), it can't go fullscreen on the real desktop, and the real pointer can't reach it.
# Player prefs land in the scratch config folder too, never in ~/.config/unity3d.
#
#   Tools/nested.sh [--size WxH] [--keep] [--config DIR] [play.sh args...]
#   Tools/nested.sh -alibiPadTest "$PWD/Captures/pad" -logFile "$PWD/Logs/pad.log"
#   Tools/nested.sh --size 1280x800 -screen-width 1280 -screen-height 800 -alibiResolution 1280x800 -alibiKeysTest ...
#
# --size is the nested desktop's size (default 1920x1080). --keep leaves the scratch folder
# (Captures/nested-<pid>/, KWin's log) for a look afterwards. --config uses DIR (a scratch folder, never
# ~/.config) as the config folder instead of a fresh one, so two runs can share the player's prefs (a setting
# saved in one and read back in the next). The exit code is the game's. Only the
# processes started here are stopped: KWin by PID, and afterwards any helper the session woke up
# (ksecretd, a portal, PipeWire...) that is still running with this session's private D-Bus address
# or scratch config folder in its environment. Nothing else carries either, so nothing else is touched.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SIZE=1920x1080; KEEP=0; CONFIG=
while [ $# -gt 0 ]; do
  case "$1" in
    --size) SIZE="$2"; shift 2 ;;
    --keep) KEEP=1; shift ;;
    --config) CONFIG="$(realpath -m "$2")"; shift 2 ;;
    *) break ;;
  esac
done
for tool in kwin_wayland dbus-run-session; do
  command -v "$tool" > /dev/null || { echo "nested.sh: needs $tool (KDE's KWin and D-Bus)" >&2; exit 2; }
done
[ -n "${XDG_RUNTIME_DIR:-}" ] || { echo "nested.sh: XDG_RUNTIME_DIR isn't set" >&2; exit 2; }

SCRATCH="$ROOT/Captures/nested-$$"
rm -rf "$SCRATCH"; mkdir -p "$SCRATCH/config"
case "${CONFIG:-}" in
  "") CONFIG="$SCRATCH/config" ;;
  "$HOME/.config"|"$HOME/.config/"*) echo "nested.sh: --config must be a scratch folder, not $CONFIG" >&2; exit 2 ;;
  *) mkdir -p "$CONFIG" ;;
esac
export ALIBI_NESTED_SOCK="alibi-nested-$$" ALIBI_NESTED_DIR="$SCRATCH" ALIBI_ROOT="$ROOT"
export ALIBI_NESTED_W="${SIZE%x*}" ALIBI_NESTED_H="${SIZE#*x}"
# Everything inside (KWin's settings, the player's prefs) reads and writes the scratch folder.
export XDG_CONFIG_HOME="$CONFIG"

set +e
dbus-run-session -- bash -c '
  kwin_wayland --virtual --socket "$ALIBI_NESTED_SOCK" --width "$ALIBI_NESTED_W" --height "$ALIBI_NESTED_H" \
    --no-lockscreen --no-global-shortcuts > "$ALIBI_NESTED_DIR/kwin.log" 2>&1 & KW=$!
  trap "kill \$KW 2>/dev/null; wait \$KW 2>/dev/null" EXIT
  echo "$DBUS_SESSION_BUS_ADDRESS" > "$ALIBI_NESTED_DIR/bus"
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
# Nothing started here may outlive it. Helpers activated over the private bus (ksecretd above all)
# don't exit when that bus goes away, so find them by the session's markers and stop them.
BUS=$(cat "$SCRATCH/bus" 2>/dev/null || true)
unset XDG_CONFIG_HOME   # so the scan's own tr and grep don't carry the marker they look for
ours() {
  local p env
  for p in /proc/[0-9]*; do
    [ -O "$p" ] || continue
    env=$({ tr '\0' '\n' < "$p/environ"; } 2>/dev/null) || continue
    if { [ -n "$BUS" ] && grep -qxF "DBUS_SESSION_BUS_ADDRESS=$BUS" <<< "$env"; } \
       || grep -qxF "XDG_CONFIG_HOME=$CONFIG" <<< "$env" \
       || grep -qxF "WAYLAND_DISPLAY=$ALIBI_NESTED_SOCK" <<< "$env"; then
      [ "${p#/proc/}" = "$$" ] || echo "${p#/proc/}"
    fi
  done
}
helpers=$(ours | tr '\n' ' ')
if [ -n "${helpers// /}" ]; then
  for p in $helpers; do echo "[nested] stopping helper $p ($(cat /proc/$p/comm 2>/dev/null))"; done
  kill $helpers 2>/dev/null || true
  for i in $(seq 20); do left=$(ours | tr '\n' ' '); [ -z "${left// /}" ] && break; sleep 0.1; done
  [ -z "${left// /}" ] || kill -9 $left 2>/dev/null || true
fi
left=$(ours | tr '\n' ' ')
[ -z "${left// /}" ] || echo "[nested] warning: still running from this session: $left" >&2
[ "$KEEP" = 1 ] || rm -rf "$SCRATCH"
exit $code
