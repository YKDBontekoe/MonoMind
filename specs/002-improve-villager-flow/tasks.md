# Tasks: Improved Villager Flow

**Input**: Design documents from `/specs/002-improve-villager-flow/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required. This feature touches villagers, villages, UI, guidance, agent
state, and saves. The final gate includes
`dotnet run --project src/Autonocraft -- --test`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish baseline understanding of Town Board, guidance, and test surface before changing behavior.

- [X] T001 Review Town Board panel architecture, tab routing, and footer actions in `src/Autonocraft/UI/VillageScreen.cs`
- [X] T002 [P] Review current read-model fields and refresh cadence in `src/Autonocraft/UI/Village/VillageViewModel.cs`
- [X] T003 [P] Review guidance hint sources and priority rules in `src/Autonocraft.Village/AI/VillageGuidance.cs`
- [X] T004 [P] Review activity copy and nameplate usage in `src/Autonocraft.Village/Citizens/VillagerActivityText.cs`
- [X] T005 [P] Review existing village layout and guidance tests in `tests/Autonocraft.Tests/Integration/VillageTests.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared domain result types and unified guidance that all user stories depend on.

**CRITICAL**: No user story implementation should begin until this phase is complete.

- [X] T006 Add `JobAssignmentResult`, `RecruitResult`, and stable `reasonCode` values in `src/Autonocraft.Village/Scheduling/JobAssignmentResult.cs`
- [X] T007 Add `SettlementGuidance` output (headline, detail, priority, suggested tab, food risk) in `src/Autonocraft.Village/AI/SettlementGuidance.cs`
- [X] T008 Refactor `JobDispatcher.TryAssignJob` to return `JobAssignmentResult` and map all blocked branches in `src/Autonocraft.Village/Scheduling/JobDispatcher.cs`
- [X] T009 Update `IJobAssignment` and all `TryAssignJob` call sites to use `JobAssignmentResult` in `src/Autonocraft.Village/Scheduling/IJobAssignment.cs`
- [X] T010 Extend `VillageManager.TryAssignJob` and `TryRecruit` to return structured results with remediation text in `src/Autonocraft.Village/VillageManager.cs`
- [X] T011 Extend `VillageViewModel` with dashboard fields (`idleWorkerCount`, `foodRiskLevel`, `nextActionKind`, `activeWorkSummary`, `recruitPreview`) in `src/Autonocraft/UI/Village/VillageViewModel.cs`
- [X] T012 Add `RunSettlementGuidancePriority` integration test for food-vs-idle ordering in `tests/Autonocraft.Tests/Integration/VillageTests.cs`

**Checkpoint**: Domain layer returns structured assign/recruit outcomes; guidance has a single prioritized source.

---

## Phase 3: User Story 1 - Understand Settlement Status at a Glance (Priority: P1) MVP

**Goal**: Opening the Town Board Overview shows population, food risk, idle workers, active work, and one clear next action without tab switching.

**Independent Test**: Load a starter settlement, press **V**, and verify dashboard fields and next action are visible on Overview without opening People or Build tabs.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [X] T013 [P] [US1] Add `RunSettlementDashboardFields` test for Overview read-model population, food risk, idle count, and pending build count in `tests/Autonocraft.Tests/Integration/VillageTests.cs`
- [X] T014 [P] [US1] Extend `RunVillageScreenInputLayout` to assert 1280×720 Overview dashboard elements do not clip in `tests/Autonocraft.Tests/Integration/VillageTests.cs`

### Implementation for User Story 1

- [X] T015 [US1] Add idle-worker badge, food-risk indicator, and active-work summary line to `src/Autonocraft/UI/VillagePanels/OverviewPanel.cs`
- [X] T016 [US1] Add pinned next-action call-to-action that deep-links to People or Build tab in `src/Autonocraft/UI/VillagePanels/OverviewPanel.cs`
- [X] T017 [US1] Wire `SettlementGuidance` into `VillageViewModel.Build` for `NextAction` and dashboard fields in `src/Autonocraft/UI/Village/VillageViewModel.cs`
- [X] T018 [US1] Route `EarlyGameGuide.GetGuidanceHint` through `SettlementGuidance` headline when settlement is active in `src/Autonocraft.Core/Game/EarlyGameGuide.cs`
- [X] T019 [US1] Align HUD `guidanceHint` display with settlement guidance priority in `src/Autonocraft.Engine/Hud/HudRenderer.cs`
- [X] T020 [US1] Unify founding quick-start copy with established settlement vocabulary in `src/Autonocraft/UI/VillagePanels/FoundingPanel.cs`

**Checkpoint**: User Story 1 is complete when Overview is a readable dashboard and HUD hint does not contradict Town Board next action.

---

## Phase 4: User Story 2 - Assign and Track Villager Work Easily (Priority: P2)

**Goal**: Players assign jobs quickly, see plain-language activity per villager, and get inline blocked-action feedback; in-world affordance opens citizen detail.

**Independent Test**: Select a villager on People, assign a job, verify activity text updates; attempt Mine without quarry and verify inline remediation on the same screen.

### Tests for User Story 2

- [X] T021 [P] [US2] Add `RunJobAssignmentBlockedReasons` tests for Mine-without-quarry, Farm-without-plot, and Build-without-site in `tests/Autonocraft.Tests/Integration/VillageTests.cs`
- [X] T022 [P] [US2] Add `RunVillagerActivityTextContext` tests for coordinates, site names, and haul progress in `tests/Autonocraft.Tests/Integration/VillageTests.cs`

### Implementation for User Story 2

- [X] T023 [US2] Extend `VillagerActivityText` with village/world context overloads for targets and building sites in `src/Autonocraft.Village/Citizens/VillagerActivityText.cs`
- [X] T024 [US2] Show `activity`, `progress`, and attention flags in citizen list rows in `src/Autonocraft/UI/VillagePanels/PeoplePanel.cs`
- [X] T025 [US2] Show activity, progress, and inline assign success/failure messages in citizen detail pane in `src/Autonocraft/UI/VillagePanels/PeoplePanel.cs`
- [X] T026 [US2] Route job-button clicks through `JobAssignmentResult` and display remediation in `src/Autonocraft/UI/VillageScreen.Input.cs`
- [X] T027 [US2] Update assign-job handling in `src/Autonocraft/Game/GameOverlayRouter.cs` to surface `JobAssignmentResult` feedback
- [X] T028 [US2] Add in-world villager manage prompt and open-People-tab-with-selection flow in `src/Autonocraft/Game/GameInputRouter.cs`
- [X] T029 [US2] Use enriched activity text in world nameplates in `src/Autonocraft.Engine/Visuals/VillagerNameplateRenderer.cs`

**Checkpoint**: User Story 2 is complete when job assignment is fast, activity is readable in UI and world, and blocked assigns explain remediation inline.

---

## Phase 5: User Story 3 - Grow and Care for the Settlement Over Time (Priority: P3)

**Goal**: Recruit, housing, food, and well-being feel like one connected progression with proactive requirements and synchronized guidance.

**Independent Test**: Attempt recruit at housing cap and with missing planks; verify preview text and blocked reasons match footer actions and Overview guidance.

### Tests for User Story 3

- [X] T030 [P] [US3] Add `RunRecruitPreviewBlockedReason` tests for housing cap and missing materials in `tests/Autonocraft.Tests/Integration/VillageTests.cs`
- [X] T031 [P] [US3] Add `RunSettlementWellBeingWarnings` tests for low food and idle crisis banners in `tests/Autonocraft.Tests/Integration/VillageTests.cs`

### Implementation for User Story 3

- [X] T032 [US3] Add recruit preview with housing, cost, and storage requirements in footer chrome in `src/Autonocraft/UI/VillageScreen.Chrome.cs`
- [X] T033 [US3] Surface `RecruitResult` remediation on blocked recruit attempts in `src/Autonocraft/UI/VillageScreen.Input.cs`
- [X] T034 [US3] Add prioritized well-being and crisis banners to Overview when food or morale risk is high in `src/Autonocraft/UI/VillagePanels/OverviewPanel.cs`
- [X] T035 [US3] Refresh guidance and dashboard fields when food stock changes after rations withdraw in `src/Autonocraft/UI/VillageScreen.Helpers.cs`
- [X] T036 [US3] Suppress duplicate assign-failure toasts while Town Board is open in `src/Autonocraft.Village/VillageEvents.cs`

**Checkpoint**: User Story 3 is complete when growth gates are visible before failure and food/rations changes keep guidance in sync.

---

## Phase 6: User Story 4 - Relate to Villagers as Characters (Priority: P4)

**Goal**: Citizens feel distinct; optional AI chat is reachable from People tab without dead ends for non-AI players.

**Independent Test**: Open People with two settlers; distinguish by name, role, and activity; verify Talk opens chat when AI enabled and is disabled with explanation when AI off.

### Tests for User Story 4

- [X] T037 [P] [US4] Add `RunPeopleTabCitizenDifferentiation` test for distinct rows and attention flags in `tests/Autonocraft.Tests/Integration/VillageTests.cs`

### Implementation for User Story 4

- [X] T038 [US4] Enhance citizen list styling for attention, role color, and brief status summary in `src/Autonocraft/UI/VillagePanels/PeoplePanel.cs`
- [X] T039 [US4] Gate Talk button on `playWithAi` with disabled-state explanation in `src/Autonocraft/UI/VillagePanels/PeoplePanel.cs`
- [X] T040 [US4] Open `VillageChatScreen` from People tab preserving villager name and id in `src/Autonocraft/Game/GameOverlayRouter.cs`
- [X] T041 [US4] Show persona trait and morale cues prominently in citizen detail in `src/Autonocraft/UI/VillagePanels/PeoplePanel.cs`

**Checkpoint**: User Story 4 is complete when villagers read as individuals and chat integration respects AI settings.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Agent parity, documentation, and constitution-mandated verification.

- [X] T042 [P] Add additive `nextAction`, `idleWorkers`, and `foodRisk` fields to village DTO in `src/Autonocraft.Core/Agent/Serialization/AgentStateSerializer.cs`
- [X] T043 [P] Add `activity`, `progress`, and `needsAttention` to villager DTOs in `src/Autonocraft.Core/Agent/Serialization/AgentStateSerializer.cs`
- [X] T044 Return actionable `assign_job` and recruit failure messages from agent actions in `src/Autonocraft.Core/Agent/BasicAgentActions.cs`
- [X] T045 [P] Update Town Board and villager flow notes in `docs/ARCHITECTURE.md`
- [X] T046 [P] Update test list, HTTP state fields, and Town Board workflow in `AGENTS.md`
- [X] T047 Run full integration suite `dotnet run --project src/Autonocraft -- --test` and fix regressions
- [X] T048 Execute manual validation checklist in `specs/002-improve-villager-flow/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **blocks all user stories**
- **User Story 1 (Phase 3)**: Depends on Foundational — MVP
- **User Story 2 (Phase 4)**: Depends on Foundational; benefits from US1 guidance wiring but testable independently
- **User Story 3 (Phase 5)**: Depends on Foundational; uses US1 Overview surfaces
- **User Story 4 (Phase 6)**: Depends on Foundational; uses US2 People panel enhancements
- **Polish (Phase 7)**: Depends on desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: After Phase 2 — no dependency on US2–US4
- **US2 (P2)**: After Phase 2 — uses `JobAssignmentResult` from Phase 2; Overview optional for testing
- **US3 (P3)**: After Phase 2 — recruit preview uses `RecruitResult`; crisis banners best on US1 Overview
- **US4 (P4)**: After Phase 2 — builds on People panel from US2

### Within Each User Story

- Tests before implementation (marked stories)
- Domain types (Phase 2) before UI consumption
- ViewModel fields before panel rendering
- Story checkpoint before next priority

### Parallel Opportunities

- Phase 1: T002–T005 in parallel
- Phase 2: T006–T007 in parallel after T001 review; T012 after T007
- US1: T013–T014 in parallel; T015–T016 in parallel after T017
- US2: T021–T022 in parallel; T024–T025 sequential on same file
- US3: T030–T031 in parallel
- US4: T037 standalone before T038–T041
- Polish: T042–T043, T045–T046 in parallel

---

## Parallel Example: User Story 1

```bash
# Tests first (parallel):
# T013 RunSettlementDashboardFields in VillageTests.cs
# T014 Extend RunVillageScreenInputLayout in VillageTests.cs

# After T017 ViewModel wiring (parallel UI):
# T015 OverviewPanel dashboard badges
# T016 OverviewPanel next-action CTA
# T020 FoundingPanel copy alignment
```

---

## Parallel Example: User Story 2

```bash
# Tests first (parallel):
# T021 RunJobAssignmentBlockedReasons
# T022 RunVillagerActivityTextContext

# Domain then UI:
# T023 VillagerActivityText context overloads
# T024–T025 PeoplePanel list and detail (same file — sequential)
# T028 GameInputRouter in-world affordance (parallel with T029 nameplates)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Overview dashboard + guidance sync per quickstart.md §3
5. Demo improved “what do I do next?” experience

### Incremental Delivery

1. Setup + Foundational → structured results and unified guidance
2. US1 → settlement dashboard (MVP)
3. US2 → job assign clarity and activity tracking
4. US3 → recruit/growth loop transparency
5. US4 → character and chat polish
6. Polish → agent parity and full `--test` gate

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. After Foundational:
   - Developer A: US1 (Overview + guidance sync)
   - Developer B: US2 (activity text + assign feedback)
   - Developer C: US3 (recruit preview + well-being)
3. US4 and Polish after core flows land

---

## Notes

- Do not change save format version; assignments and food stock must round-trip unchanged
- Preserve **V** Town Board, **C** chat, **R** recruit, and hotbar 0–8 conventions
- `PeoplePanel.cs` is touched by US2 and US4 — coordinate or merge sequentially
- `OverviewPanel.cs` is touched by US1 and US3 — US3 banners build on US1 dashboard
- All `[P]` tasks must target different files or non-overlapping concerns

---

## Phase 8: Convergence

**Purpose**: Close remaining gaps between spec/plan/contracts and the current implementation.

- [X] T049 Fix PeoplePanel Talk affordance copy and steward entry per FR-011 and US4/AC2 (partial): change misleading "Open steward chat" hint in `src/Autonocraft/UI/VillagePanels/PeoplePanel.cs` to villager-specific copy, and add a Town Board steward chat entry (Overview footer or header) when AI is enabled that opens `VillageChatScreen` in steward mode via `src/Autonocraft/Game/GameOverlayRouter.cs`
- [X] T050 Reconcile HUD and Town Board guidance when early-game overrides apply per FR-010 (partial): when `EarlyGameGuide.GetGuidanceHint` returns survival/craft stage hints, ensure Town Board Overview shows a non-contradicting settlement banner or defers to `SettlementGuidance` headline in `src/Autonocraft.Core/Game/EarlyGameGuide.cs` and `src/Autonocraft/UI/VillagePanels/OverviewPanel.cs`
- [X] T051 Add agent `/state` guidance parity integration test per agent-state-contract (partial): assert `guidanceHint` and `village.nextAction` describe the same priority issue in `tests/Autonocraft.Tests/Integration/VillageTests.Ui.cs` and register in `tests/Autonocraft.Tests/IntegrationTestRunner.cs`
- [X] T052 Document split village integration test files in agent codebase map per plan maintenance (partial): update `AGENTS.md` codebase map to list `VillageTests.*.cs` partials under `tests/Autonocraft.Tests/Integration/`
