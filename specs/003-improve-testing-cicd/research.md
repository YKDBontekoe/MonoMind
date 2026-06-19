# Research: Improved Testing and CI/CD Pipelines

## Decision: Keep GitHub Actions as the Single Automation Platform

**Rationale**: The repository already runs build, unit, integration, quality,
security, versioning, and release workflows on GitHub Actions with multi-OS
matrices. Incremental improvement preserves contributor familiarity and avoids
migration cost.

**Alternatives considered**: External CI (Buildkite, CircleCI); self-hosted
runners only. Rejected for v1 because they add operational overhead without
addressing the primary gaps (check ordering, coverage mapping, E2E promotion).

## Decision: Introduce a Documented Required-Check Registry

**Rationale**: FR-001 and SC-003 require one authoritative list of required
checks with visible categories. Today checks are spread across `ci.yml`,
`quality.yml`, and `codeql.yml` with no single mapping document. A registry in
`specs/003-improve-testing-cicd/contracts/ci-verification-contract.md` (and a
machine-readable mirror under `docs/ci/required-checks.json` if needed) gives
contributors and maintainers a single source of truth.

**Alternatives considered**: Rely on GitHub branch protection UI only. Insufficient
because category names and local parity commands are not visible there.

## Decision: Fail-Fast Tiering Within and Across Workflows

**Rationale**: SC-001 targets 95% of PRs completing within 20 minutes. Current
layout runs three full OS matrices for build, unit, and integration in parallel
after build, while `quality.yml` runs independently. Reorder so Ubuntu fast
gates (format, atlas validation) run first and can fail before expensive
matrices. Keep integration on all three OSes (FR-007) but gate version bump on
CI **and** quality **and** CodeQL success.

**Alternatives considered**: Single-OS integration only. Rejected — release
quality depends on cross-platform headless `--test` today.

**Implementation pattern**:

1. **Tier 0 (fast, ubuntu)**: `dotnet format --verify-no-changes`, atlas `--check`
2. **Tier 1 (build matrix)**: solution build on Linux/Windows/macOS
3. **Tier 2 (unit matrix)**: xUnit unit filter with TRX + test-reporter
4. **Tier 3 (integration matrix)**: '**: headless `--test` with log artifacts
5. **Tier 4 (optional extended, ubuntu)**: agent API E2E via `scripts/ci_e2e.sh`
   with `USE_XVFB=1` on Linux

## Decision: Promote Agent API E2E as a Required Ubuntu Job (Phase 1 Slice)

**Rationale**: `scripts/ci_e2e.sh` and `scripts/ci_e2e.ps1` already orchestrate
live API tests (`test_live_api.py`, 22 tests) and JSON scenarios (5 scenarios)
but are **not wired into any workflow**. These cover agent workflows, HTTP
readiness, screenshots, movement, and village actions — a blind spot for
headless `--test`. Run on ubuntu-latest with xvfb; add Windows/macOS E2E as
follow-up if stable (documented exception per Assumptions).

**Alternatives considered**:

- Leave E2E manual only — rejected; violates FR-006 for release-critical agent paths.
- Run E2E on all three OSes immediately — deferred; window/display variance increases flake risk (SC-007).

## Decision: Classify Auxiliary Python Scripts by Automation Tier

**Rationale**: FR-006 requires each auxiliary script to be promoted or
documented.

| Script | Validates | Decision |
|--------|-----------|----------|
| `.cursor/skills/autonocraft-game-test/scripts/test_live_api.py` | Agent HTTP API | **Required** via `ci_e2e` |
| `.cursor/skills/autonocraft-game-test/scripts/run_scenario.py` | JSON scenarios | **Required** via `ci_e2e` |
| `tests/live_villager_e2e.py` | Villager HTTP flow | **Merge or alias** into live API suite; deprecate duplicate if redundant |
| `tests/verify_terrain_slabs.py` | Terrain slab visuals | **Manual/scheduled** — needs window; document in contributor guide |
| `tests/capture_structure_gallery.py` | Screenshot gallery | **Manual** — visual inspection asset |
| `tests/interact.py` | Ad-hoc CLI | **Documented manual** helper |
| `tests/test_glfw/` | Legacy Vulkan/GLFW | **Remove or archive** — obsolete per AGENTS.md |

## Decision: Protected-Domain Coverage Matrix as a Maintained Artifact

**Rationale**: SC-002 requires 100% of policy protected domains map to at least
one executable check. AGENTS.md lists domains requiring `--test`. Map each to
unit tests, integration tests (`GameIntegrationTests` / `--test`), or E2E agent
tests in `contracts/protected-domain-coverage.md`.

**Alternatives considered**: Implicit coverage via test file names. Rejected —
maintainers cannot verify SC-008 without an explicit matrix.

## Decision: Publish Coverage Artifacts Without Enforcing Threshold (v1)

**Rationale**: Spec Assumptions defer hard coverage gates. Keep `quality.yml`
coverlet collection; add summary step (e.g., report generator or cobertura
artifact upload) so FR-012 is satisfied. Threshold enforcement is a future policy
change.

**Alternatives considered**: Codecov with required threshold. Deferred to avoid
blocking merges while baseline is established.

## Decision: Local Parity Script at Repository Root

**Rationale**: FR-004 and SC-004 require documented local commands mirroring CI.
Add `scripts/verify_local.sh` (and `scripts/verify_local.ps1`) that runs the
same tiers in order with flags to skip slow steps (`--quick` = format + atlas +
unit; `--full` = + integration + optional E2E).

**Alternatives considered**: Document commands only in README. Insufficient for
90% parity predictability — a script reduces invocation errors.

## Decision: Gate Version Bump on All Required Workflows

**Rationale**: FR-010 and SC-005 — `version.yml` currently triggers only on
`CI` workflow success. Quality and CodeQL can fail while CI passes. Change to
`workflow_run` listening for all required workflows or use a concluding
`ci-gate` job that aggregates results.

**Alternatives considered**: Merge everything into one mega-workflow. Rejected —
separate workflows keep concerns isolated; aggregation job links them.

## Decision: Standardize Failure Artifacts and Check Names

**Rationale**: FR-003 and SC-006 — use consistent job names matching registry
categories (`Build`, `Unit tests`, `Integration tests`, `Format`, `Atlas`,
`Code coverage`, `Agent E2E`, `CodeQL`). Upload logs/TRX/coverage on `always()`
for failed jobs. Integration logs already uploaded; extend to E2E `test_output/`.

## Decision: No Gameplay Source Changes in This Feature Slice

**Rationale**: Pipeline improvements should not require simulation changes unless
adding tests exposes gaps. New tests may be added under `tests/Autonocraft.Tests`
or agent scenarios, but core gameplay code stays untouched unless a protected
domain lacks any check.

**Alternatives considered**: Broad new integration tests for every domain in v1.
Scoped to mapping + filling highest-risk gaps (agent API E2E) first.
