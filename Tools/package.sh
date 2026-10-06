#!/usr/bin/env bash
# Builds and zips release archives into Builds/Release/, with a SHA256SUMS file. Nothing is uploaded.
#
#   Tools/package.sh [--no-build] [version] [linux] [mac] [windows] [webgl]
#
# The version defaults to PlayerSettings' bundleVersion; platforms default to "linux mac". Each
# platform is built with Tools/unity.sh (which validates the cases first) unless --no-build is given.
#   AlibiAndCo-<v>-linux-x86_64.zip     AlibiAndCo/ with the player and AlibiAndCo.sh (Wayland-aware launcher)
#   AlibiAndCo-<v>-macos-universal.zip  AlibiAndCo.app (Intel + Apple Silicon, ad-hoc signed, not notarized)
#   AlibiAndCo-<v>-windows-x64.zip      needs Unity's Windows Build Support module
#   AlibiAndCo-<v>-web.zip              the WebGL build, to be served over HTTP
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
build=1
if [ "${1:-}" = "--no-build" ]; then build=0; shift; fi
VERSION="$(sed -n 's/^  bundleVersion: //p' "$ROOT/ProjectSettings/ProjectSettings.asset")"
if [[ "${1:-}" =~ ^[0-9] ]]; then VERSION="$1"; shift; fi
platforms=("$@")
[ ${#platforms[@]} -gt 0 ] || platforms=(linux mac)
OUT="$ROOT/Builds/Release"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$OUT"
zipdir() { python3 "$ROOT/Tools/release/zipdir.py" "$@"; }   # OUT.zip DIR ENTRY... (keeps modes and symlinks)

run_build() {   # unity.sh target, log name
  [ $build -eq 1 ] || return 0
  echo "== building $1"
  LOG="$ROOT/Logs/package-$1.log" nice -n 10 "$ROOT/Tools/unity.sh" "$1" > /dev/null
  grep -h "\[Build\]" "$ROOT/Logs/package-$1.log" | tail -1
}

notices() { cp "$ROOT/THIRD_PARTY_NOTICES.md" "$1/"; }

for p in "${platforms[@]}"; do
  case "$p" in
    linux)
      run_build build-linux
      src="$ROOT/Builds/Linux"
      [ -x "$src/AlibiAndCo.x86_64" ] || { echo "no Linux build in $src" >&2; exit 1; }
      d="$STAGE/linux/AlibiAndCo"; mkdir -p "$d"
      (cd "$src" && tar --exclude='*_BackUpThisFolder_ButDontShipItWithYourGame' -cf - .) | (cd "$d" && tar -xf -)
      install -m 755 "$ROOT/Tools/release/AlibiAndCo.sh" "$d/AlibiAndCo.sh"
      notices "$d"
      zipname="AlibiAndCo-$VERSION-linux-x86_64.zip"
      zipdir "$OUT/$zipname" "$STAGE/linux" AlibiAndCo
      ;;
    mac)
      run_build build-mac
      src="$ROOT/Builds/macOS/AlibiAndCo.app"
      [ -d "$src" ] || { echo "no macOS build at $src" >&2; exit 1; }
      d="$STAGE/mac"; mkdir -p "$d"
      cp -a "$src" "$d/"
      notices "$d"
      zipname="AlibiAndCo-$VERSION-macos-universal.zip"
      zipdir "$OUT/$zipname" "$d" AlibiAndCo.app THIRD_PARTY_NOTICES.md
      ;;
    windows)
      run_build build-windows
      src="$ROOT/Builds/Windows"
      [ -f "$src/AlibiAndCo.exe" ] || { echo "no Windows build in $src" >&2; exit 1; }
      d="$STAGE/windows/AlibiAndCo"; mkdir -p "$d"
      (cd "$src" && tar --exclude='*_BackUpThisFolder_ButDontShipItWithYourGame' --exclude='*_BurstDebugInformation_DoNotShip' -cf - .) | (cd "$d" && tar -xf -)
      notices "$d"
      zipname="AlibiAndCo-$VERSION-windows-x64.zip"
      zipdir "$OUT/$zipname" "$STAGE/windows" AlibiAndCo
      ;;
    webgl)
      run_build build-webgl
      src="$ROOT/Builds/WebGL"
      [ -f "$src/index.html" ] || { echo "no WebGL build in $src" >&2; exit 1; }
      zipname="AlibiAndCo-$VERSION-web.zip"
      zipdir "$OUT/$zipname" "$src" $(cd "$src" && ls -A)
      ;;
    *) echo "unknown platform: $p (linux, mac, windows, webgl)" >&2; exit 2 ;;
  esac
  echo "== $OUT/$zipname ($(du -h "$OUT/$zipname" | cut -f1))"
done

(cd "$OUT" && sha256sum AlibiAndCo-"$VERSION"-*.zip > SHA256SUMS && cat SHA256SUMS)
