#!/bin/bash
# Build, start game, run live API tests and JSON scenarios.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../../../../" && pwd)"
SCRIPTS="$ROOT/.cursor/skills/autonocraft-game-test/scripts"
OUTPUT="$ROOT/test_output"
PORT="${PORT:-5001}"

cd "$ROOT"
export DYLD_LIBRARY_PATH="/opt/homebrew/lib${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"

mkdir -p "$OUTPUT"

echo "==> Building Autonocraft..."
dotnet build src/Autonocraft -p:SkipMonoGameContent=true -v q

echo "==> Stopping any existing game instance..."
pkill -f "dotnet run --project src/Autonocraft" 2>/dev/null || true
sleep 1

echo "==> Starting game (--skip-menu)..."
dotnet run --project src/Autonocraft --no-build -- --skip-menu --agent-port "$PORT" >"$OUTPUT/game.log" 2>&1 &
GAME_PID=$!
echo "    PID=$GAME_PID log=$OUTPUT/game.log"

cleanup() {
  echo "==> Shutting down game (pid $GAME_PID)..."
  kill "$GAME_PID" 2>/dev/null || true
  wait "$GAME_PID" 2>/dev/null || true
}
trap cleanup EXIT

echo "==> Running live API tests..."
python3 "$SCRIPTS/test_live_api.py" --port "$PORT" --output-dir "$OUTPUT/live_api" --wait 180
LIVE_RESULT=$?

echo "==> Running JSON scenarios..."
python3 "$SCRIPTS/run_scenario.py" --all --port "$PORT" --output-dir "$OUTPUT/scenarios"
SCENARIO_RESULT=$?

if [[ "$LIVE_RESULT" -eq 0 && "$SCENARIO_RESULT" -eq 0 ]]; then
  echo "ALL GAME SCRIPT TESTS PASSED"
  exit 0
fi

echo "SOME TESTS FAILED (live=$LIVE_RESULT scenarios=$SCENARIO_RESULT)"
exit 1
