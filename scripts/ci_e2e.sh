#!/usr/bin/env bash
# Cross-platform E2E orchestration for CI and local runs (Linux/macOS).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SCRIPTS="$ROOT/.cursor/skills/autonocraft-game-test/scripts"
OUTPUT="$ROOT/test_output"
PORT="${PORT:-5001}"

cd "$ROOT"
mkdir -p "$OUTPUT"

if [[ "$(uname)" == "Darwin" ]]; then
  export DYLD_LIBRARY_PATH="/opt/homebrew/lib${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"
fi

GAME_CMD=(dotnet run --project src/Autonocraft --configuration Release --no-build -- --skip-menu --agent-port "$PORT")

echo "==> Starting game on port $PORT..."
if [[ -n "${USE_XVFB:-}" ]]; then
  xvfb-run -a "${GAME_CMD[@]}" >"$OUTPUT/game.log" 2>&1 &
else
  "${GAME_CMD[@]}" >"$OUTPUT/game.log" 2>&1 &
fi
GAME_PID=$!
echo "    PID=$GAME_PID log=$OUTPUT/game.log"

cleanup() {
  echo "==> Shutting down game (pid $GAME_PID)..."
  kill "$GAME_PID" 2>/dev/null || true
  wait "$GAME_PID" 2>/dev/null || true
}
trap cleanup EXIT

echo "==> Running live API tests..."
python3 "$SCRIPTS/test_live_api.py" --port "$PORT" --wait 180 --output-dir "$OUTPUT/live_api"
LIVE_RESULT=$?

echo "==> Running JSON scenarios..."
python3 "$SCRIPTS/run_scenario.py" --all --port "$PORT" --output-dir "$OUTPUT/scenarios"
SCENARIO_RESULT=$?

if [[ "$LIVE_RESULT" -eq 0 && "$SCENARIO_RESULT" -eq 0 ]]; then
  echo "ALL E2E TESTS PASSED"
  exit 0
fi

echo "E2E FAILED (live=$LIVE_RESULT scenarios=$SCENARIO_RESULT)"
exit 1
