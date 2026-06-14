#!/bin/bash
# Launch Autonocraft with --skip-menu for agent/script testing.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../../../" && pwd)"
cd "$ROOT"
export DYLD_LIBRARY_PATH="/opt/homebrew/lib${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"
echo "Starting Autonocraft (skip menu) from $ROOT"
exec dotnet run --project src/Autonocraft -- --skip-menu --agent-port 5001 "$@"
