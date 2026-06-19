# Contract: Local Verification Parity

## Purpose

Define how contributors reproduce required CI checks locally before pushing.
Satisfies FR-004 and SC-004.

## Entry Points

| Profile | Script | When to Use |
|---------|--------|-------------|
| Quick | `scripts/verify_local.sh --quick` | Every commit touching C# |
| Full | `scripts/verify_local.sh --full` | Before opening PR; after gameplay/simulation changes |
| E2E only | `scripts/ci_e2e.sh` | After agent API or HTTP workflow changes |

Windows equivalents: `scripts/verify_local.ps1`, `scripts/ci_e2e.ps1`.

## Quick Profile MUST Run

1. `dotnet format Autonocraft.slnx --verify-no-changes`
2. `dotnet run --project src/Autonocraft.AtlasBuild -- --check`
3. `dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Autonocraft.Tests.Unit"`

Expected: all exit code 0.

## Full Profile MUST Run (in order)

All quick profile steps, then:

4. `dotnet build Autonocraft.slnx -c Release`
5. `dotnet run --project src/Autonocraft -c Release -- --test`

Optional on developer machine (recommended before agent PRs):

6. `scripts/ci_e2e.sh` (requires Release build from step 4)

## Platform Notes

| Environment | Note |
|-------------|------|
| macOS (local) | `DYLD_LIBRARY_PATH=/opt/homebrew/lib` may be required for `dotnet run`; see README |
| macOS (CI) | Do NOT set `DYLD_LIBRARY_PATH` — causes SIGBUS on arm64 runners |
| Linux CI E2E | Use `USE_XVFB=1 scripts/ci_e2e.sh` when no display server |
| Windows E2E | Use `scripts/ci_e2e.ps1`; game starts hidden |

## Parity Exceptions (documented)

- **CodeQL**: no local equivalent; rely on CI
- **Multi-OS matrix**: local full matrix not required; primary dev OS + CI matrix covers SC-004
- **Coverage upload**: local run collects coverage only when explicitly invoked; CI always uploads `coverage-report` + `coverage-summary.txt`
- **Agent E2E**: not part of `--full` by default; run `scripts/ci_e2e.sh` separately after Release build (required on Linux CI as `Agent E2E (Linux)`)
- **Fast gates duplication**: `dotnet format` and atlas run in CI `Fast gates` before the OS matrix and again in Quality workflow — intentional fail-fast tiering

## Validated parity (2026-06-19, macOS arm64)

`./scripts/verify_local.sh --quick` and `--full` both exit 0 on feature branch `003-improve-testing-cicd`.
Full profile predicts CI success for format, atlas, unit, build, and integration categories on this platform.

## Success Criteria

A passing `--full` run on the contributor's primary OS predicts CI success in ≥90%
of cases when no platform-specific code changed.
