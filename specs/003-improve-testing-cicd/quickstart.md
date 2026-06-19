# Quickstart: Improved Testing and CI/CD Validation

## Prerequisites

- .NET 10 SDK
- Python 3 (for agent E2E scripts)
- GitHub CLI (`gh`) optional for inspecting workflow runs locally after push

## 1. Baseline — Current CI Parity (Pre-Change)

From repo root, run existing documented commands:

```bash
dotnet format Autonocraft.slnx --verify-no-changes
dotnet run --project src/Autonocraft.AtlasBuild -- --check
dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Autonocraft.Tests.Unit"
dotnet run --project src/Autonocraft -c Release -- --test
```

Expected: all exit code 0; integration ends with success message.

## 2. After Implementation — Local Verification Script

```bash
./scripts/verify_local.sh --quick   # format + atlas + unit
./scripts/verify_local.sh --full      # + build + integration (+ optional E2E flag)
```

Expected: script prints tier labels matching [ci-verification-contract.md](contracts/ci-verification-contract.md) categories.

## 3. Agent E2E Smoke Test

Requires Release build:

```bash
dotnet build Autonocraft.slnx -c Release
./scripts/ci_e2e.sh
```

Expected output includes:

```text
ALL E2E TESTS PASSED
```

Artifacts under `test_output/` (game log, live_api, scenarios).

On Linux CI without display:

```bash
USE_XVFB=1 ./scripts/ci_e2e.sh
```

## 4. Controlled Regression — Integration Gate

Introduce a deliberate failure (e.g., temporarily break a known integration test
or assert in `--test`), push to a branch, open PR.

Expected:

- `Integration tests (<platform>)` job fails
- `integration-output.log` artifact uploaded
- Merge blocked until fixed

Revert the break before merge.

## 5. Controlled Regression — Format Gate

Introduce a formatting violation, push PR.

Expected:

- `dotnet format` job fails **before** or **without waiting for** full OS matrix completion when fail-fast tiering is enabled
- Clear category name in PR checks list

## 6. Protected Domain Matrix Audit

Open [protected-domain-coverage.md](contracts/protected-domain-coverage.md).

For each domain row, run the listed check locally and confirm it executes (not skipped).

Expected: 100% rows have at least one automated mapping with passing local run.

## 7. Workflow Gate — Version Bump

On a test branch merged to main (maintainer sandbox):

- Confirm `Version and Changelog` waits for CI **and** Quality **and** CodeQL
- Confirm version bump does **not** run when Quality fails while CI passes

## 8. CI Timing Sample (SC-001)

Record wall-clock time from PR open to all required checks complete on three sample PRs.

Target: ≤20 minutes for 95% of PRs under normal load after pipeline improvements.

## 9. Failure Diagnosis Sample (SC-006)

Collect 10 historical failed runs; time how long it takes a contributor to identify
the failing category from PR UI alone.

Target: category identifiable within one detail view in ≥90% of cases.

## Reference Documents

- [ci-verification-contract.md](contracts/ci-verification-contract.md) — required checks
- [protected-domain-coverage.md](contracts/protected-domain-coverage.md) — domain mapping
- [local-verification-contract.md](contracts/local-verification-contract.md) — local parity
- [data-model.md](data-model.md) — entity definitions

## Validation outcomes (implementation)

| Scenario | Status | Notes |
|----------|--------|-------|
| §1 Baseline commands | Pass | Superseded by `verify_local.sh` |
| §2 Local verification script | Pass | `--quick` and `--full` exit 0 on macOS arm64 (2026-06-19) |
| §3 Agent E2E smoke | Deferred | Requires display/xvfb; validated via `ci_e2e.sh` wiring in CI job |
| §4 Integration regression gate | Documented | Expected: `Integration tests (*)` fails + `integration-output.log` artifact |
| §5 Format regression gate | Documented | Expected: `Fast gates` / `dotnet format` fails before OS matrix |
| §6 Protected domain audit | Pass | Matrix updated against `IntegrationTestRunner` inventory |
| §7 Version bump gate | Implemented | `version.yml` gates on CI + Quality + CodeQL |
| §8 SC-001 timing | Deferred | Baseline measurement requires three post-merge PR samples on GitHub |
| §9 SC-006 diagnosis | Deferred | Requires 10 historical failed runs post-merge |

**SC-001 baseline note:** Pre-change PR wall-clock samples not collected in this worktree.
After merge, record three PRs from Actions → total duration to all required checks complete;
target ≤20 minutes for 95% of PRs.
