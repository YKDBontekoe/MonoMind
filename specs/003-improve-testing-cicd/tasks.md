# Tasks: Improved Testing and CI/CD Pipelines

**Input**: Design documents from `/specs/003-improve-testing-cicd/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Meta-feature — validation uses controlled CI regressions and
`specs/003-improve-testing-cicd/quickstart.md` scenarios rather than gameplay
`--test` changes. Run `./scripts/verify_local.sh --full` before opening PR for
this feature branch.

**Organization**: Tasks grouped by user story for independent delivery and verification.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story label (US1–US4)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Baseline review of existing workflows and orchestration scripts before changes.

- [X] T001 Review CI job structure, matrix, and artifacts in `.github/workflows/ci.yml`
- [X] T002 [P] Review quality gates (format, atlas, coverage) in `.github/workflows/quality.yml`
- [X] T003 [P] Review security and release workflows in `.github/workflows/codeql.yml`, `.github/workflows/version.yml`, and `.github/workflows/release.yml`
- [X] T004 [P] Review E2E orchestration and macOS `DYLD_LIBRARY_PATH` handling in `scripts/ci_e2e.sh` and `scripts/ci_e2e.ps1`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Local parity scripts and contributor documentation that all user stories depend on.

**CRITICAL**: No user story implementation should begin until this phase is complete.

- [X] T005 Create `scripts/verify_local.sh` with `--quick` and `--full` profiles per `specs/003-improve-testing-cicd/contracts/local-verification-contract.md`
- [X] T006 [P] Create `scripts/verify_local.ps1` with matching `--quick` and `--full` profiles in `scripts/verify_local.ps1`
- [X] T007 Update required-checks table, local commands, and contract links in AGENTS.md §2b (`AGENTS.md`)
- [X] T008 [P] Add CI verification section with `verify_local` usage and registry link in `README.md`

**Checkpoint**: Contributors can run documented local commands; contracts are linked from runtime docs.

---

## Phase 3: User Story 1 - Trust Automated Verification on Every Change (Priority: P1) MVP

**Goal**: Every required check reports clear pass/fail with category-identifiable names and downloadable failure context; local commands predict CI outcomes.

**Independent Test**: Open PR with known-good and known-bad commits; failing check name identifies category; `./scripts/verify_local.sh --full` passes on good commit.

### Implementation for User Story 1

- [X] T009 [US1] Align CI job display names with verification categories in `.github/workflows/ci.yml` per `specs/003-improve-testing-cicd/contracts/ci-verification-contract.md`
- [X] T010 [P] [US1] Align quality workflow job names with contract categories in `.github/workflows/quality.yml`
- [X] T011 [US1] Standardize unit test TRX paths and `dorny/test-reporter` names per platform in `.github/workflows/ci.yml`
- [X] T012 [US1] Ensure integration `integration-output.log` artifacts upload with `if: always()` on all OS matrix jobs in `.github/workflows/ci.yml`
- [X] T013 [US1] Publish contributor-facing required-check mirror in `docs/ci/required-checks.md` sourced from `specs/003-improve-testing-cicd/contracts/ci-verification-contract.md`
- [X] T014 [US1] Run `./scripts/verify_local.sh --full` and document any parity exceptions in `specs/003-improve-testing-cicd/contracts/local-verification-contract.md`

**Checkpoint**: PR checks map 1:1 to contract categories; local full profile mirrors required automation on primary OS.

---

## Phase 4: User Story 2 - Catch Regressions Before They Reach Players (Priority: P2)

**Goal**: Protected domains mapped to executable checks; agent API E2E promoted to required CI; auxiliary scripts classified.

**Independent Test**: Controlled regression in save or villager domain fails CI; agent E2E job runs on ubuntu and uploads logs on failure.

### Implementation for User Story 2

- [X] T015 [US2] Audit and update protected-domain rows against live `--test` and unit test names in `specs/003-improve-testing-cicd/contracts/protected-domain-coverage.md`
- [X] T016 [US2] Add `agent-e2e` job running `USE_XVFB=1 scripts/ci_e2e.sh` after build on ubuntu-latest in `.github/workflows/ci.yml`
- [X] T017 [US2] Upload `test_output/` artifact on agent-e2e failure with `if: always()` in `.github/workflows/ci.yml`
- [X] T018 [US2] Compare `tests/live_villager_e2e.py` with `.cursor/skills/autonocraft-game-test/scripts/test_live_api.py` and deduplicate or deprecate redundant coverage in `tests/live_villager_e2e.py`
- [X] T019 [P] [US2] Document manual-only auxiliary scripts (owner, trigger, rationale) in `docs/ci/manual-verification.md` per `specs/003-improve-testing-cicd/contracts/protected-domain-coverage.md`

**Checkpoint**: 100% protected domains mapped; agent E2E required on Linux CI; manual scripts documented.

---

## Phase 5: User Story 3 - Get Faster, Actionable Feedback While Developing (Priority: P3)

**Goal**: Fast gates fail early; coverage artifacts published; flake process documented; legacy test noise removed.

**Independent Test**: Format-only PR failure surfaces in Tier 0 before build matrix completes; failed jobs retain downloadable artifacts.

### Implementation for User Story 3

- [X] T020 [US3] Add `fast-gates` ubuntu job (format + atlas) as Tier 0 in `.github/workflows/ci.yml`
- [X] T021 [US3] Wire build matrix `needs: fast-gates` so matrix skips on fast-gate failure in `.github/workflows/ci.yml`
- [X] T022 [US3] Add coverage summary artifact upload step after coverlet collection in `.github/workflows/quality.yml`
- [X] T023 [P] [US3] Document flake identification and quarantine process in AGENTS.md CI section (`AGENTS.md`)
- [X] T024 [US3] Remove or archive obsolete `tests/test_glfw/` after confirming no references in workflows or docs

**Checkpoint**: Obvious defects fail in <3 minutes on ubuntu; coverage and integration artifacts always available on failure.

---

## Phase 6: User Story 4 - Release and Maintain with Predictable Automation (Priority: P4)

**Goal**: Version bump waits for CI, Quality, and CodeQL; release still validates published binaries.

**Independent Test**: Version workflow does not run when Quality fails while CI passes; `release.yml` still executes `--test` on publish output.

### Implementation for User Story 4

- [X] T025 [US4] Gate `version.yml` on successful CI, Quality, and CodeQL workflow runs for the commit in `.github/workflows/version.yml`
- [X] T026 [US4] Confirm headless integration step on published binaries remains in `.github/workflows/release.yml`
- [X] T027 [P] [US4] Document mainline gate order and failure blocking behavior in `docs/ci/release-gate.md`

**Checkpoint**: No version bump or tag on partial validation; release path documented.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: End-to-end validation and success-criteria measurement across all stories.

- [X] T028 Run controlled format regression scenario from `specs/003-improve-testing-cicd/quickstart.md` §5 and record outcome in PR notes
- [X] T029 Run controlled integration regression scenario from `specs/003-improve-testing-cicd/quickstart.md` §4 and record outcome in PR notes
- [X] T030 [P] Measure PR wall-clock time on three sample runs and note SC-001 baseline in `specs/003-improve-testing-cicd/quickstart.md`
- [X] T031 Complete full validation checklist in `specs/003-improve-testing-cicd/quickstart.md` and mark any deferred items with rationale

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **blocks all user stories**
- **User Story 1 (Phase 3)**: Depends on Phase 2 — MVP deliverable
- **User Story 2 (Phase 4)**: Depends on Phase 2; benefits from US1 job naming (T009–T012) before adding agent-e2e (T016)
- **User Story 3 (Phase 5)**: Depends on Phase 2; `fast-gates` (T020–T021) should follow US1 CI naming (T009)
- **User Story 4 (Phase 6)**: Depends on Phase 2; logically after US1–US3 workflows stabilize
- **Polish (Phase 7)**: Depends on US1–US4 completion

### User Story Dependencies

| Story | Depends On | Can Parallelize With |
|-------|------------|----------------------|
| US1 (P1) | Phase 2 | — (MVP first) |
| US2 (P2) | Phase 2, US1 naming | US3 after T020 prerequisites |
| US3 (P3) | Phase 2, US1 CI structure | US2 (different workflow sections) |
| US4 (P4) | Phase 2, stable workflow names | US2/US3 late tasks |

### Within Each User Story

- Contract/docs alignment before workflow YAML edits where noted
- Workflow changes validated via quickstart scenarios before story checkpoint
- Do not weaken or skip required checks to pass tasks (FR-014)

### Parallel Opportunities

- **Phase 1**: T002, T003, T004 in parallel after T001
- **Phase 2**: T006 parallel with T005; T008 parallel with T007 after T005 starts
- **US1**: T010 parallel with T009; T013 parallel after T009–T012
- **US2**: T019 parallel with T015–T018
- **US3**: T023 parallel with T020–T022
- **US4**: T027 parallel with T025–T026
- **Polish**: T030 parallel with T028–T029

---

## Parallel Example: User Story 1

```bash
# After Phase 2 completes, launch naming/artifact tasks together:
# T010 — quality.yml job names
# T011 — unit TRX/reporter in ci.yml (coordinate file sections with T009 owner)

# Documentation in parallel once workflows updated:
# T013 — docs/ci/required-checks.md
# T014 — local parity validation + contract update
```

---

## Parallel Example: User Story 2

```bash
# Agent E2E wiring (sequential):
# T016 → T017 in ci.yml

# In parallel with E2E work:
# T019 — docs/ci/manual-verification.md
# T015 — protected-domain-coverage.md audit
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (verify_local + docs)
3. Complete Phase 3: User Story 1 (check names, artifacts, registry)
4. **STOP and VALIDATE**: quickstart §1 + §2 + controlled bad commit on PR
5. Merge MVP if US1 independent test passes

### Incremental Delivery

1. Setup + Foundational → local parity ready
2. US1 → trusted PR checks (MVP)
3. US2 → agent E2E + domain matrix
4. US3 → fail-fast + coverage artifacts + flake docs
5. US4 → version/release gate hardening
6. Polish → quickstart +os and full validation

### Parallel Team Strategy

1. Team completes Phase 1–2 together
2. Then split:
   - **Dev A**: US1 (T009–T014)
   - **Dev B**: US2 (T015–T19) after T009 lands
   - **Dev C**: US3 (T020–T24) after T009 lands
3. **Dev A or lead**: US4 (T025–T27) after workflow churn settles
4. All: Phase 7 validation

---

## Notes

- No gameplay source changes expected unless protected-domain audit (T015) finds unmapped gaps requiring new tests
- macOS CI: never set `DYLD_LIBRARY_PATH` on GitHub Actions (see `scripts/ci_e2e.sh`)
- Windows agent E2E matrix deferred per plan; document in `docs/ci/manual-verification.md` if needed
- Branch protection rule updates (GitHub UI) may be required after T013 — document in PR description, not code
- Task count: **31** total — Setup 4, Foundational 4, US1 6, US2 5, US3 5, US4 3, Polish 4
