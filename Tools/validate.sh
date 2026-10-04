#!/usr/bin/env bash
# Standalone case validator: compiles Assets/Scripts/Logic with the .NET 8 SDK that ships inside
# the Unity Editor (no system dotnet needed) and proves every case airtight.
#   Tools/validate.sh [--verbose]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Data/DotNetSdk/dotnet")}"
[ -x "$DOTNET" ] || { echo "no dotnet found (set DOTNET=/path/to/dotnet)" >&2; exit 1; }
# Keep the SDK's first-run state inside the project's ignored build folder.
export DOTNET_CLI_HOME="$ROOT/Tools/CaseValidator/obj/dotnet-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
exec "$DOTNET" run --project "$ROOT/Tools/CaseValidator" -- "$@"
