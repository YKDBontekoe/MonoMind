# Required CI Checks

Contributor-facing mirror of the authoritative check registry in
[`specs/003-improve-testing-cicd/contracts/ci-verification-contract.md`](../../specs/003-improve-testing-cicd/contracts/ci-verification-contract.md).

## Pull request required checks

| Category | Job / check name | Platform | Blocks merge | Local parity |
|----------|------------------|----------|--------------|--------------|
| Format (fail-fast) | `Fast gates` → `dotnet format` | ubuntu | Yes | `./scripts/verify_local.sh --quick` |
| Format | `dotnet format` | ubuntu | Yes | Same |
| Atlas (fail-fast) | `Fast gates` → `Atlas validation` | ubuntu | Yes | Same |
| Atlas | `Atlas validation` | ubuntu | Yes | Same |
| Build | `Build (Linux)` | ubuntu | Yes | `./scripts/verify_local.sh --full` |
| Build | `Build (Windows)` | windows | Yes | Windows build locally |
| Build | `Build (macOS)` | macos | Yes | macOS build locally |
| Unit | `Unit tests (Linux)` | ubuntu | Yes | `--quick` or `--full` |
| Unit | `Unit tests (Windows)` | windows | Yes | Windows unit tests |
| Unit | `Unit tests (macOS)` | macos | Yes | macOS unit tests |
| Integration | `Integration tests (Linux)` | ubuntu | Yes | `--full` |
| Integration | `Integration tests (Windows)` | windows | Yes | Windows `--test` |
| Integration | `Integration tests (macOS)` | macos | Yes | macOS `--test` |
| Agent E2E | `Agent E2E (Linux)` | ubuntu | Yes | `scripts/ci_e2e.sh` after Release build |
| Coverage | `Code coverage` | ubuntu | Yes (artifact) | Part of `--full` locally without upload |
| Security | `Analyze` (CodeQL) | ubuntu | Yes | CI-only |

## Local verification entry points

```bash
./scripts/verify_local.sh --quick   # format + atlas + unit
./scripts/verify_local.sh --full    # + Release build + headless integration
```

Windows: `pwsh scripts/verify_local.ps1 --quick` / `--full`.

Agent HTTP workflows: `scripts/ci_e2e.sh` (Linux/macOS) or `scripts/ci_e2e.ps1` (Windows).

## Failure artifacts

| Job | Artifact |
|-----|----------|
| Unit tests | `unit-test-results-<os>` (TRX) |
| Integration tests | `integration-log-<os>` |
| Agent E2E | `agent-e2e-output` (`test_output/`) |
| Code coverage | `coverage-report` + `coverage-summary.txt` |

## Branch protection

After merging workflow changes, maintainers should ensure GitHub branch protection
rules require the job names above (especially new `Fast gates` and `Agent E2E (Linux)`).

## Mainline gate (version bump)

`Version and Changelog` runs only after **CI**, **Quality**, and **CodeQL** all
succeed for the same commit on `main`. See [release-gate.md](release-gate.md).
