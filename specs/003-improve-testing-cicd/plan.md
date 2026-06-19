# Implementation Plan: Improved Testing and CI/CD Pipelines

**Branch**: `003-improve-testing-cicd` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-improve-testing-cicd/spec.md`

## Summary

Strengthen contributor confidence in automated verification by reorganizing GitHub
Actions for fail-fast feedback, wiring existing agent E2E orchestration into CI,
publishing a required-check registry with protected-domain coverage mapping, adding
local parity scripts, and gating version bumps on all quality workflows. Work
primarily touches `.github/workflows/`, `scripts/`, and documentation — not gameplay
simulation. Headless `--test` and xUnit unit suites remain the core regression
backbone; agent API E2E closes the largest automation gap.

## Technical Context

**Language/Version**: YAML (GitHub Actions), Bash/PowerShell orchestration, Python 3
(E2E scripts), C# test projects on .NET 10

**Primary Dependencies**: GitHub Actions, `actions/setup-dotnet@v5`, `dorny/test-reporter@v3`,
coverlet (existing), xvfb (Linux E2E), existing `.cursor/skills/autonocraft-game-test/scripts`

**Storage**: N/A — CI artifacts (logs, TRX, coverage) retained via Actions artifacts

**Testing**: Meta-feature improving verification itself. Validation via controlled
regressions, timing samples, and [quickstart.md](quickstart.md) scenarios. No change
to `--test` semantics unless new tests fill mapped gaps.

**Target Platform**: CI runners — ubuntu-latest, windows-latest, macos-latest;
local parity on contributor primary OS

**Project Type**: Desktop game repository with multi-workflow CI/CD pipeline

**Performance Goals**: ≥95% of PRs complete all required checks within 20 minutes
(SC-001). Tier 0 checks (format, atlas) complete in <3 minutes on ubuntu.

**Constraints**: Must not weaken existing gates (FR-014). Multi-OS integration
matrix retained. macOS CI must not set `DYLD_LIBRARY_PATH` on Actions (SIGBUS).

**Scale/Scope**: ~5 workflow files, 2–4 new/updated shell scripts, 3 contract docs,
AGENTS.md/README CI section updates, optional removal of `tests/test_glfw/` legacy

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Code Quality and Architecture**: PASS. Changes confined to `.github/workflows/`,
  `scripts/`, `docs/`, and `specs/003-*`. No new `src/Autonocraft.*` projects.
  Reuse existing `ci_e2e.sh`, `ci_e2e.ps1`, and game-test skill scripts.
- **Testing Evidence**: PASS (meta). This feature defines verification evidence
  for all other work. Implementation MUST dogfood: every workflow change validated
  via [quickstart.md](quickstart.md) controlled regressions. Gameplay `--test`
  suite unchanged unless adding coverage for unmapped domains.
- **User Experience Consistency**: PASS — no player-facing or agent API behavior
  changes. E2E tests exercise existing HTTP contract only.
- **Performance Budget**: PASS — CI wall-clock and runner cost are the budgets.
  Fail-fast tiering and concurrency groups reduce wasted matrix time on obvious
  failures. No runtime frame-time impact.

### Post-Design Re-check (Phase 1)

All gates remain PASS. Contracts limit scope to infrastructure; protected-domain
matrix references existing tests without mandating gameplay rewrites.

## Project Structure

### Documentation (this feature)

```text
specs/003-improve-testing-cicd/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── ci-verification-contract.md
│   ├── protected-domain-coverage.md
│   └── local-verification-contract.md
└── tasks.md             # Phase 2 (/speckit-tasks) — not created by /speckit-plan
```

### Source Code (repository root)

```text
.github/workflows/
├── ci.yml              # Reorder tiers; optional agent-e2e job; artifact standards
├── quality.yml         # May merge fast gates into ci or stay parallel with gate job
├── codeql.yml          # Unchanged behavior; included in version gate
├── version.yml         # Gate on CI + Quality + CodeQL success
└── release.yml         # Existing publish + --test on artifact

scripts/
├── ci_e2e.sh           # Existing — wire into CI
├── ci_e2e.ps1          # Existing — future Windows E2E
├── verify_local.sh     # NEW — local parity (--quick / --full)
├── verify_local.ps1    # NEW — Windows parity
└── release_bump.py     # Existing — no change

tests/
├── Autonocraft.Tests/  # Unit xUnit (unchanged structure)
└── (integration via src/Autonocraft -- --test)

.cursor/skills/autonocraft-game-test/scripts/
├── test_live_api.py    # Promoted to required E2E via ci_e2e
└── run_scenario.py     # Promoted to required E2E via ci_e2e

AGENTS.md               # Update CI section + SPECKIT plan pointer
README.md               # Link verify_local and required checks registry
```

**Structure Decision**: Infrastructure-only diff. Primary implementation surface is
GitHub Actions YAML and orchestration scripts; contracts under `specs/003-*` are the
authoritative check registry until optionally mirrored to `docs/ci/`.

## Implementation Phases (for /speckit-tasks)

### Phase A — Registry and Documentation (P1)

- Publish contracts (done in plan phase)
- Add `scripts/verify_local.sh` / `.ps1` per [local-verification-contract.md](contracts/local-verification-contract.md)
- Update AGENTS.md §2b and README with required checks table and local commands

### Phase B — Fail-Fast CI Restructure (P1/P3)

- Add `fast-gates` job (ubuntu): format + atlas before build matrix
- Make build matrix `needs: fast-gates`
- Standardize job names per [ci-verification-contract.md](contracts/ci-verification-contract.md)
- Ensure failure artifacts on all matrix jobs

### Phase C — Agent E2E in CI (P2)

- Add `agent-e2e` job (ubuntu, `needs: build`) running `USE_XVFB=1 scripts/ci_e2e.sh`
- Upload `test_output/` artifact on failure
- Audit `tests/live_villager_e2e.py` vs `test_live_api.py` for deduplication

### Phase D — Release Gate Hardening (P4)

- Update `version.yml` to require Quality + CodeQL workflow success (workflow_run aggregation or check suite)
- Verify `release.yml` still runs `--test` on published binaries

### Phase E — Coverage and Flake Hygiene (P3)

- Add coverage summary artifact step in quality workflow
- Document flake quarantine process in AGENTS.md
- Remove or archive `tests/test_glfw/` if confirmed obsolete

## Complexity Tracking

No constitution violations. Optional follow-ups (not v1 blockers):

| Deferred Item | Why Deferred |
|---------------|--------------|
| Windows/macOS agent E2E matrix | Display/window flake risk; ubuntu+xvfb first |
| Hard coverage threshold | Spec assumes trend-only for v1 |
| Codecov integration | Requires org token setup |

## Phase 0 Output

See [research.md](research.md) — all technical unknowns resolved.

## Phase 1 Output

- [data-model.md](data-model.md)
- [contracts/](contracts/)
- [quickstart.md](quickstart.md)
- Agent context updated → `specs/003-improve-testing-cicd/plan.md`
