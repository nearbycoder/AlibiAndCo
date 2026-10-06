#!/usr/bin/env bash
# Launches Alibi & Co. On a Wayland desktop it uses Unity's native Wayland backend: the default
# X11/XWayland path can hang at startup on some desktops. Extra arguments are passed through
# (for example: ./AlibiAndCo.sh -screen-fullscreen 0).
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
args=()
if [ -n "${WAYLAND_DISPLAY:-}" ] && [ -z "${ALIBI_X11:-}" ]; then args+=(-force-wayland); fi
exec "$HERE/AlibiAndCo.x86_64" "${args[@]}" "$@"
