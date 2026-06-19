# Contract: Required CI Verification Checks

## Purpose

Define the authoritative list of required automated checks for pull requests and
mainline integration. Check names MUST match GitHub Actions job names for SC-003.

## Pull Request Required Checks

| Category | Job / Check Name | Platform | Blocks Merge | Local Parity Command |
|----------|------------------|----------|--------------|----------------------|
| Format | `dotnet format` | ubuntu | Yes | `dotnet format Autonocraft.slnx --verify-no-changes` |
| Atlas | `Atlas validation` | ubuntu | Yes | `dotnet run --project src/Autonocraft.AtlasBuild -- --check` |
| Build | `Build (Linux)` | ubuntu | Yes | `dotnet build Autonocraft.slnx -c Release` |
| Build | `Build (Windows)` | windows | Yes | Same (on Windows) |
| Build | `Build (macOS)` | macos | Yes | Same (on macOS) |
| Unit | `Unit tests (Linux)` | ubuntu | Yes | See unit command below |
| Unit | `Unit tests (Windows)` | windows | Yes | See unit command below |
| Unit | `Unit tests (macOS)` | macos | Yes | See unit command below |
| Integration | `Integration tests (Linux)` | ubuntu | Yes | See integration command below |
| Integration | `Integration tests (Windows)` | windows | Yes | See integration command below |
| Integration | `Integration tests (macOS)` | macos | Yes | See integration command below |
| Coverage | `Code coverage` | ubuntu | Yes (artifact) | Part of `scripts/verify_local.sh --full` |
| Security | `Analyze` (CodeQL) | ubuntu | Yes | N/A (CI-only) |
| Agent E2E | `Agent E2E (Linux)` | ubuntu | Yes | `scripts/ci_e2e.sh` after Release build |

**Unit command (canonical)**

```bash
dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Autonocraft.Tests.Unit"
```

**Integration command (canonical)**

```bash
dotnet run --project src/Autonocraft -c Release -- --test
```

## Mainline Gate (before version bump)

All of the following workflows MUST complete successfully for the triggering commit:

- `CI`
- `Quality`
- `CodeQL`

`Version and Changelog` MUST NOT run when any required workflow fails.

## Failure Reporting Requirements

Each failing job MUST:

1. Exit with non-zero status
2. Upload artifacts on failure (`if: always()` where applicable)
3. Include category keyword in job name (Build, Unit tests, Integration tests, etc.)
4. For unit tests: publish TRX via test-reporter
5. For integration: upload `integration-output.log`
6. For agent E2E: upload `test_output/` directory

## Scheduled Health Runs

Cron schedules (06:00 UTC CI/Quality, Monday 07:00 UTC CodeQL) MUST remain
enabled on `main`. Failures MUST appear in GitHub Actions notifications (default
repo watch behavior).

## Exception Process

Required checks MUST NOT be disabled to merge. Temporary exceptions require:

- Issue or PR comment with reason, scope, and remediation date
- Maintainer approval
- Follow-up task to restore the check
