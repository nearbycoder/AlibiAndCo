#!/usr/bin/env bash
# Run the built Linux player. On this machine the X11/XWayland path can hang at startup, so use
# Unity's native Wayland backend when a Wayland session is available.
set -euo pipefail
GAME="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Builds/Linux/AlibiAndCo.x86_64"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }
args=(-screen-fullscreen 0 -screen-width 1920 -screen-height 1080)
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
exec "$GAME" "${args[@]}" "$@"
