# Tasks: Villager Onboarding

**Input**: Design documents from `/specs/005-villager-onboarding/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Include test tasks for every behavior change. For villagers, villages, and UI, include the headless integration suite: `dotnet run --project src/Autonocraft -- --test`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Shared test fixtures and starter-flow helpers used across user stories

- [X] T001 [P] Add reusable starter-settlement test helpers for recruit and recovery scenarios in `tests/Autonocraft.Tests/Integration/VillageTests.SimulationHelpers.cs`
- [X] T002 [P] Add reusable village onboarding assertion helpers for UI state snapshots in `tests/Autonocraft.Tests/Integration/VillageTests.Ui.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared state and copy plumbing that all user stories depend on

**⚠️ CRITICAL**: No user story work should begin until this phase is complete

- [X] T003 [P] Extend the village read model to carry starter-flow summary, blocked-state, and recruit-preview fields in `src/Autonocraft/UI/Village/VillageViewModel.cs`
- [X] T004 [P] Normalize starter-step selection and guidance copy so settlement state, next action, and blocker text come from one source in `src/Autonocraft.Village/AI/SettlementGuidance.cs` and `src/Autonocraft.Core/Game/EarlyGameGuide.cs`
- [X] T005 [P] Mirror the additive onboarding state for scripted tooling in `src/Autonocraft.Core/Agent/Serialization/AgentStateSerializer.cs` and `src/Autonocraft.Core/Agent/Handlers/StateHandler.cs`

**Checkpoint**: Starter flow summary, guidance, and agent-visible state are aligned

---

## Phase 3: User Story 1 - Start the Village Without Guessing (Priority: P1) 🎯 MVP

**Goal**: Make the opening villager flow understandable so players know what to do first

**Independent Test**: Open a fresh or empty settlement and verify that the flow shows the current state, the next action, and a clear first-step explanation without requiring outside guidance

### Tests for User Story 1 ⚠️

- [X] T006 [P] [US1] Add integration coverage for starter-flow visibility and first-step guidance in `tests/Autonocraft.Tests/Integration/VillageTests.Founding.cs`
- [X] T007 [P] [US1] Add integration coverage for the empty-settlement dashboard and next-action cards in `tests/Autonocraft.Tests/Integration/VillageTests.Ui.cs`

### Implementation for User Story 1

- [X] T008 [US1] Update the Overview panel to surface the current starter state, next action, and status hierarchy in `src/Autonocraft/UI/VillagePanels/OverviewPanel.cs`
- [X] T009 [P] [US1] Update founding and Town Board entry hints so the opening villager flow uses consistent onboarding language in `src/Autonocraft/UI/VillageScreen.Helpers.cs` and `src/Autonocraft/UI/VillageScreen.Founding.cs`

**Checkpoint**: Players can open the village flow and understand the first step

---

## Phase 4: User Story 2 - Add Villagers Reliably (Priority: P1)

**Goal**: Make recruit and summon actions dependable, with clear success and failure feedback

**Independent Test**: Attempt a valid recruit or summon action and confirm success updates the roster; attempt an invalid action and confirm the blocker reason and remediation are shown on the same screen

### Tests for User Story 2 ⚠️

- [X] T010 [P] [US2] Add integration coverage for recruit success, housing cap failure, and missing-materials failure in `tests/Autonocraft.Tests/Integration/VillageTests.Jobs.cs`
- [X] T011 [P] [US2] Add agent and console parity coverage for starter recruitment and blocked reasons in `tests/Autonocraft.Tests/Integration/VillageTests.Agent.cs`

### Implementation for User Story 2

- [X] T012 [US2] Refine recruit success and failure messaging in `src/Autonocraft.Village/VillageManager.cs` and `src/Autonocraft.Village/Scheduling/JobAssignmentResult.cs`
- [X] T013 [P] [US2] Surface recruit preview and blocked-remediation text in the onboarding UI and village console entry points in `src/Autonocraft/UI/VillagePanels/OverviewPanel.cs` and `src/Autonocraft.Core/DevCommands/Commands/VillageCommands.cs`

**Checkpoint**: Recruit and summon flows are usable and explain blockers clearly

---

## Phase 5: User Story 3 - Make the Villager UI Clear and Usable (Priority: P2)

**Goal**: Improve readability, hierarchy, and layout so the starter UI feels finished rather than buggy

**Independent Test**: Open the villager UI at supported resolutions and verify that text, roster rows, and controls remain readable without overlap, clipping, or stale state

### Tests for User Story 3 ⚠️

- [X] T014 [P] [US3] Extend 1280×720 layout assertions for the onboarding dashboard and roster presentation in `tests/Autonocraft.Tests/Integration/VillageTests.Ui.cs`
- [X] T015 [P] [US3] Add UI regression coverage for roster readability, stable refresh, and warning hierarchy in `tests/Autonocraft.Tests/Integration/VillageTests.Ui.cs`

### Implementation for User Story 3

- [X] T016 [P] [US3] Rework villager roster and detail spacing to keep the early village lists readable in `src/Autonocraft/UI/VillagePanels/PeoplePanel.cs` and `src/Autonocraft/UI/VillagePanels/BuildPanel.cs`
- [X] T017 [US3] Tighten panel chrome, spacing, and on-screen action hierarchy in `src/Autonocraft/UI/VillageScreen.cs` and `src/Autonocraft/UI/VillageScreen.Chrome.cs`

**Checkpoint**: The village UI reads cleanly and stays usable at supported sizes

---

## Phase 6: User Story 4 - Recover From Failed or Partial Starting States (Priority: P2)

**Goal**: Make the starter flow recover cleanly after partial setup, retries, reloads, or state mismatches

**Independent Test**: Save, reload, and reopen the villager flow after a partial or failed setup and verify that the latest state is restored without duplicated villagers or stale counts

### Tests for User Story 4 ⚠️

- [X] T018 [P] [US4] Add save/reopen recovery coverage for starter-state transitions in `tests/Autonocraft.Tests/Integration/VillageTests.Founding.cs`
- [X] T019 [P] [US4] Add agent and reopen-state refresh coverage for in-progress village flows in `tests/Autonocraft.Tests/Integration/VillageTests.Agent.cs`

### Implementation for User Story 4

- [X] T020 [US4] Keep the latest starter state in sync across reopen and reload paths in `src/Autonocraft/UI/VillageScreen.Helpers.cs` and `src/Autonocraft.Core/Game/GameSession.cs`
- [X] T021 [P] [US4] Harden recovery and re-synchronization paths so partial starter states and repeated actions do not duplicate villagers or stale counts in `src/Autonocraft.Village/VillageManager.cs`

**Checkpoint**: Reopening the flow restores the current state and recovers from partial failures

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final verification, documentation alignment, and cross-story validation

- [X] T022 Run the mandatory headless integration suite and record the result in `dotnet run --project src/Autonocraft -- --test`
- [X] T023 Verify the onboarding validation steps in `specs/005-villager-onboarding/quickstart.md` and confirm recruit, blocked-state, layout, and recovery behavior still match the documented flow

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - blocks all user stories
- **User Stories (Phase 3+)**: Depend on Foundational completion
  - User stories can then proceed in priority order or in parallel where files do not overlap
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational; no dependency on other stories
- **User Story 2 (P1)**: Starts after Foundational; may reuse Story 1 UI but remains independently testable
- **User Story 3 (P2)**: Starts after Foundational; can build on Story 1 and Story 2 outputs
- **User Story 4 (P2)**: Starts after Foundational; validates recovery against the implemented starter flow

### Within Each User Story

- Tests should be written and fail before implementation where applicable
- Shared model or guidance updates should happen before UI refinements that depend on them
- Story work should complete before moving to the next priority unless explicitly parallelized

### Parallel Opportunities

- T001 and T002 can run in parallel during Setup
- T003, T004, and T005 can run in parallel during Foundational work if split by file ownership
- T006 and T007 can run in parallel once the foundational state model is in place
- T010 and T011 can run in parallel once recruit-result behavior is stable
- T014 and T015 can run in parallel because they cover different layout assertions in the same UI test area
- T018 and T019 can run in parallel because they verify save/reopen and agent refresh behavior separately

---

## Parallel Example: User Story 1

```bash
# Run starter-flow tests together:
Task: "Add integration coverage for starter-flow visibility and first-step guidance in `tests/Autonocraft.Tests/Integration/VillageTests.Founding.cs`"
Task: "Add integration coverage for the empty-settlement dashboard and next-action cards in `tests/Autonocraft.Tests/Integration/VillageTests.Ui.cs`"

# Update the UI and founding hints together:
Task: "Update the Overview panel to surface the current starter state, next action, and status hierarchy in `src/Autonocraft/UI/VillagePanels/OverviewPanel.cs`"
Task: "Update founding and Town Board entry hints so the opening villager flow uses consistent onboarding language in `src/Autonocraft/UI/VillageScreen.Helpers.cs` and `src/Autonocraft/UI/VillageScreen.Founding.cs`"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. Stop and validate the starter flow before expanding scope

### Incremental Delivery

1. Complete Setup + Foundational
2. Deliver User Story 1 so the flow is understandable
3. Deliver User Story 2 so starting villagers becomes reliable
4. Deliver User Story 3 so the UI becomes clear and polished
5. Deliver User Story 4 so retries and reloads recover cleanly
6. Finish with the mandatory integration suite and quickstart verification

### Parallel Team Strategy

With multiple developers:

1. One developer owns the shared state and guidance work
2. One developer owns recruit/recovery logic and blocker messages
3. One developer owns the UI layout and roster polish
4. A fourth developer can extend the integration tests and validation coverage
