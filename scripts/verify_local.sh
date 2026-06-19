#!/usr/bin/env bash
# Local verification parity with required CI checks.
# See specs/003-improve-testing-cicd/contracts/local-verification-contract.md
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

PROFILE="${1:-}"
if [[ "$PROFILE" != "--quick" && "$PROFILE" != "--full" ]]; then
  echo "Usage: $0 --quick|--full"
  echo ""
  echo "  --quick  format, atlas validation, unit tests"
  echo "  --full   quick profile + Release build + headless integration (--test)"
  exit 1
fi

run_step() {
  local category="$1"
  shift
  echo ""
  echo "==> [$category] $*"
  "$@"
}

if [[ "$(uname)" == "Darwin" && -z "${GITHUB_ACTIONS:-}" && "${CI:-}" != "true" ]]; then
  if [[ -d /opt/homebrew/lib ]]; then
    export DYLD_LIBRARY_PATH="/opt/homebrew/lib${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"
  fi
fi

run_step "Format" dotnet format Autonocraft.slnx --verify-no-changes
run_step "Atlas" dotnet run --project src/Autonocraft.AtlasBuild -- --check
run_step "Unit tests" dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Autonocraft.Tests.Unit"

if [[ "$PROFILE" == "--full" ]]; then
  run_step "Build" dotnet build Autonocraft.slnx -c Release
  run_step "Integration tests" dotnet run --project src/Autonocraft -c Release -- --test
fi

echo ""
echo "ALL LOCAL VERIFICATION PASSED ($PROFILE)"
