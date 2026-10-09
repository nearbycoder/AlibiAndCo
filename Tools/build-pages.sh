#!/usr/bin/env bash
# Builds the browser version and lays it out as a static site for GitHub Pages in Builds/Pages/
# (gitignored): index.html at the root, Build/ beside it, and a .nojekyll file. Nothing is pushed.
#
#   Tools/build-pages.sh [--no-build] [outdir]
#
# --no-build repackages the last Builds/WebGL instead of running Tools/unity.sh build-webgl (which
# validates the cases first; its log goes to Logs/build-pages.log). The site uses only relative
# URLs, so it plays from any folder (https://nearbycoder.github.io/AlibiAndCo/), and needs no server
# headers: the Brotli files carry Unity's decompression fallback, so the page unpacks them itself
# when the server doesn't send Content-Encoding. Check it with Tools/check-pages.mjs <url>.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
build=1
if [ "${1:-}" = "--no-build" ]; then build=0; shift; fi
OUT="${1:-$ROOT/Builds/Pages}"
SRC="$ROOT/Builds/WebGL"

if [ $build -eq 1 ]; then
  mkdir -p "$ROOT/Logs"
  echo "== building WebGL (log: Logs/build-pages.log)"
  LOG="$ROOT/Logs/build-pages.log" nice -n 10 "$ROOT/Tools/unity.sh" build-webgl > /dev/null \
    || { grep -h "\[Build\]\|error" "$ROOT/Logs/build-pages.log" | tail -20 >&2; exit 1; }
  grep -h "\[Build\]" "$ROOT/Logs/build-pages.log" | tail -1
fi
[ -f "$SRC/index.html" ] && [ -d "$SRC/Build" ] || { echo "no WebGL build in $SRC" >&2; exit 1; }

rm -rf "$OUT"
mkdir -p "$OUT"
cp -r "$SRC/." "$OUT/"
touch "$OUT/.nojekyll"
chmod -R a+rX,go-w "$OUT"   # Unity writes some files 0600; a static server needs to read them

# GitHub refuses files over 100 MB; keep each under 50 MB.
big="$(find "$OUT" -type f -size +50M)"
[ -z "$big" ] || { echo "files over 50 MB:" >&2; echo "$big" >&2; exit 1; }

echo "== $OUT"
( cd "$OUT" && find . -type f -printf '%s\t%p\n' | sort -rn | awk -F'\t' '{ printf "%9.1f MB  %s\n", $1 / 1048576, $2 }' )
du -sb "$OUT" | awk '{ printf "total %.1f MB\n", $1 / 1048576 }'
