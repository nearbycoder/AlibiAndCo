#!/usr/bin/env bash
# Runs the Unity 6.6 editor against this project.
#
# The editor links against libxml2.so.2, but this machine (CachyOS) ships libxml2.so.16, so the
# loader is pointed at a copy of libxml2.so.2 in ~/.local/share/ptt-unity-libs (the proper fix
# is `sudo pacman -S libxml2-legacy`).
#
#   Tools/unity.sh                 open the project in the editor (GUI)
#   Tools/unity.sh setup           batch: materials, fonts, URP settings, scene (ProjectSetup.Apply)
#   Tools/unity.sh validate        batch: run the case validator inside Unity
#   Tools/unity.sh test            batch: EditMode tests (validator + solution replay)
#   Tools/unity.sh build-linux     batch: Builds/Linux/AlibiAndCo.x86_64
set -euo pipefail
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export LD_LIBRARY_PATH="$HOME/.local/share/ptt-unity-libs${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
LOG="${LOG:--}"
batch() { "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$LOG" "$@"; }

case "${1:-open}" in
  open)        exec "$UNITY" -projectPath "$PROJECT" ;;
  setup)       batch -executeMethod AlibiCo.EditorTools.ProjectSetup.Apply ;;
  validate)    batch -executeMethod AlibiCo.EditorTools.BuildScript.ValidateCases ;;
  test)        "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode -testResults "$PROJECT/Logs/editmode-results.xml" -logFile "$LOG" ;;
  build-linux) "$UNITY" -batchmode -quit -projectPath "$PROJECT" -logFile "$LOG" -executeMethod AlibiCo.EditorTools.BuildScript.BuildLinux ;;
  *) echo "usage: $0 [open|setup|validate|test|build-linux]" >&2; exit 2 ;;
esac
