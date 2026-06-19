# Tasks: Revamped Main Menu UI

**Input**: Design documents from `/specs/003-revamp-main-menu/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required. This feature touches UI, settings persistence, save/load entry
paths, and navigation. The final gate includes
`dotnet run --project src/Autonocraft -- --test`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish baseline understanding of the current pre-game menu flow before changing behavior.

- [X] T001 Review current menu routing in `src/Autonocraft/ScreenManager.cs` and `src/Autonocraft/Game/GameStateMachine.cs`
- [X] T002 [P] Review orphaned root screen and save browser in `src/Autonocraft/UI/MainMenuScreen.cs` and `src/Autonocraft/UI/SaveSlotScreen.cs`
- [X] T003 [P] Review shared UI primitives in `src/Autonocraft.Engine/Ui/UiTheme.cs`, `src/Autonocraft.Engine/Ui/MenuBackdrop.cs`, and `src/Autonocraft.Engine/Ui/UiTransition.cs`
- [X] T004 [P] Review save slot metadata and list APIs in `src/Autonocraft.Core/World/WorldSaveManager.cs` and `src/Autonocraft.Domain/Persistence/WorldSaveDtos.cs`
- [X] T005 [P] Review contracts in `specs/003-revamp-main-menu/contracts/menu-ui-contract.md` and `specs/003-revamp-main-menu/contracts/menu-navigation-contract.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared navigation model, menu helpers, and test scaffold that all user stories depend on.

**⚠️ CRITICAL**: No user story implementation should begin until this phase is complete.

- [X] T006 Create `MenuNavigationState` layer enum and transition helpers in `src/Autonocraft/UI/Menu/MenuNavigationState.cs`
- [X] T007 [P] Create `MenuFocusList` keyboard/mouse focus helper in `src/Autonocraft/UI/Menu/MenuFocusList.cs`
- [X] T008 [P] Create `MenuChrome` shared backdrop/scrim draw helper in `src/Autonocraft/UI/Menu/MenuChrome.cs`
- [X] T009 Add `GetMostRecentSaveSlot` (or equivalent) helper for Continue eligibility in `src/Autonocraft.Core/World/WorldSaveManager.cs`
- [X] T010 Integrate `MenuNavigationState` into `ScreenManager` with initial `RootHub` layer in `src/Autonocraft/ScreenManager.cs`
- [X] T011 Instantiate `MainMenuScreen` in `ScreenManager.Initialize` in `src/Autonocraft/ScreenManager.cs`
- [X] T012 Create `MenuTests.cs` scaffold with test host setup in `tests/Autonocraft.Tests/Integration/MenuTests.cs`
- [X] T013 Register menu integration tests in `tests/Autonocraft.Tests/IntegrationTestRunner.cs`

**Checkpoint**: Navigation state, shared helpers, and test harness exist; `MainMenuScreen` is wired into `ScreenManager`.

---

## Phase 3: User Story 1 - Arrive at a Clear, Polished Entry Screen (Priority: P1) 🎯 MVP

**Goal**: Launching the game opens a welcoming root hub with obvious primary actions operable via mouse and keyboard.

**Independent Test**: Launch without `--skip-menu`, verify title/tagline/primary actions on Root Hub; keyboard-only navigation reaches every action; Escape does not silently quit.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [X] T014 [P] [US1] Add `RunMenuInitialLayerIsRootHub` navigation invariant test in `tests/Autonocraft.Tests/Integration/MenuTests.cs`
- [X] T015 [P] [US1] Add `RunMainMenuRootHubLayoutBounds` test at 1280×720 and 800×600 in `tests/Autonocraft.Tests/Integration/MenuTests.cs`

### Implementation for User Story 1

- [X] T016 [US1] Extend `MainMenuScreen` with Continue, Play/Browse, New World, Settings, Structure Gallery, and Quit actions in `src/Autonocraft/UI/MainMenuScreen.cs`
- [X] T017 [US1] Wire `MenuFocusList` keyboard navigation (Up/Down/Tab + Enter) into `MainMenuScreen` in `src/Autonocraft/UI/MainMenuScreen.cs`
- [X] T018 [US1] Remove hidden Escape-to-quit on root; require explicit Quit affordance in `src/Autonocraft/UI/MainMenuScreen.cs`
- [X] T019 [US1] Implement Continue action using most-recent save metadata in `src/Autonocraft/UI/MainMenuScreen.cs`
- [X] T020 [US1] Route `UpdateMainMenu` for `RootHub` layer and action requests in `src/Autonocraft/Game/GameStateMachine.cs`
- [X] T021 [US1] Update `DrawMainMenu` to render `RootHub` vs `SaveBrowser` per navigation state in `src/Autonocraft/ScreenManager.cs`
- [X] T022 [US1] Apply `MenuChrome` backdrop and title hierarchy to root hub draw path in `src/Autonocraft/UI/MainMenuScreen.cs`

**Checkpoint**: User Story 1 is complete when Root Hub is the launch screen and all primary actions work via mouse and keyboard.

---

## Phase 4: User Story 2 - Manage Saves and Start Worlds with Confidence (Priority: P2)

**Goal**: Save selection, world creation, and special modes remain fully functional with clearer organization and safe delete/load flows.

**Independent Test**: From Root Hub, browse saves, load/rename/delete with confirmation, create a new world, cancel back, and recover from a failed load with inline error.

### Tests for User Story 2

- [X] T023 [P] [US2] Add `RunSaveBrowserBackReturnsToRootHub` test in `tests/Autonocraft.Tests/Integration/MenuTests.cs`
- [X] T024 [P] [US2] Add `RunDeleteRequiresTwoStepConfirmation` test in `tests/Autonocraft.Tests/Integration/MenuTests.cs`

### Implementation for User Story 2

- [X] T025 [US2] Add Back to Root Hub action and `MenuNavigationState` transition in `src/Autonocraft/UI/SaveSlotScreen.cs`
- [X] T026 [US2] Add empty save list guided copy toward New World in `src/Autonocraft/UI/SaveSlotScreen.cs`
- [X] T027 [US2] Refine save browser visual hierarchy (sidebar, detail pane, action row) per `menu-ui-contract.md` in `src/Autonocraft/UI/SaveSlotScreen.cs`
- [X] T028 [US2] Preserve and surface load failure inline error banner with remediation text in `src/Autonocraft/UI/SaveSlotScreen.cs`
- [X] T029 [US2] Wire New World entry from Root Hub directly to `GameState.NewWorldSetup` in `src/Autonocraft/Game/GameStateMachine.cs`
- [X] T030 [US2] Record back target (RootHub vs SaveBrowser) when opening new world setup in `src/Autonocraft/Game/GameStateMachine.cs`
- [X] T031 [US2] Validate seed input rejection with plain-language inline message in `src/Autonocraft/UI/NewWorldSetupScreen.cs`
- [X] T032 [US2] Return to Save Browser with error state after failed or timed-out load in `src/Autonocraft/Game/GameStateMachine.cs` and `src/Autonocraft/Game/GamePersistenceCoordinator.cs`

**Checkpoint**: User Story 2 is complete when save management and new world flows are safe, informative, and reachable from the hub.

---

## Phase 5: User Story 3 - Adjust Settings and View Stats Without Getting Lost (Priority: P3)

**Goal**: Settings and player statistics open from the menu with consistent overlay navigation and no dead-end traps.

**Independent Test**: Open Settings from Root Hub, change render distance, save and confirm persistence; cancel unsaved edits; open stats from save browser and return to prior layer.

### Tests for User Story 3

- [X] T033 [P] [US3] Add `RunSettingsCancelDoesNotPersist` test in `tests/Autonocraft.Tests/Integration/MenuTests.cs`
- [X] T034 [P] [US3] Add `RunSettingsOverlayBlocksBaseInput` test in `tests/Autonocraft.Tests/Integration/MenuTests.cs`

### Implementation for User Story 3

- [X] T035 [US3] Open Settings overlay from Root Hub and Save Browser via `MenuNavigationState` in `src/Autonocraft/Game/GameStateMachine.cs`
- [X] T036 [US3] Implement overlay draw stack (base layer + scrim + settings) in `src/Autonocraft/ScreenManager.cs`
- [X] T037 [US3] Route Stats overlay entry from Save Browser only and restore layer on close in `src/Autonocraft/Game/GameStateMachine.cs`
- [X] T038 [US3] Verify graphics/audio/AI sections and Save/Back behavior unchanged in `src/Autonocraft/UI/MainMenuSettingsScreen.cs`
- [X] T039 [US3] Add compact or scrollable layout for settings at 800×600 in `src/Autonocraft/UI/MainMenuSettingsScreen.cs`
- [X] T040 [US3] Restore previous menu layer when closing `PlayerDashboardScreen` in `src/Autonocraft/UI/PlayerDashboardScreen.cs` and `src/Autonocraft/Game/GameStateMachine.cs`

**Checkpoint**: User Story 3 is complete when settings and stats overlays open, save/cancel correctly, and return to the expected menu layer.

---

## Phase 6: User Story 4 - Experience Cohesive Visual and Motion Design (Priority: P4)

**Goal**: All pre-game menu screens share visual language, brief transitions, and visible focus feedback.

**Independent Test**: Step through Root Hub → Save Browser → New World → Settings → back; observe consistent backdrop, button styles, hint footers, and sub-1s transitions.

### Implementation for User Story 4

- [X] T041 [P] [US4] Apply `MenuChrome` to save browser draw path in `src/Autonocraft/UI/SaveSlotScreen.cs`
- [X] T042 [P] [US4] Apply `MenuChrome` to new world setup draw path in `src/Autonocraft/UI/NewWorldSetupScreen.cs`
- [X] T043 [US4] Align menu screen fade timings to 0.2–0.3s `UiTransition` in `src/Autonocraft/ScreenManager.cs`
- [X] T044 [US4] Add visible keyboard focus indicators to `MenuFocusList` rendering in `src/Autonocraft/UI/Menu/MenuFocusList.cs`
- [X] T045 [US4] Unify window title suffixes per `menu-navigation-contract.md` in `src/Autonocraft/Game/GameStateMachine.cs`
- [X] T046 [US4] Standardize hint footer styling across `MainMenuScreen`, `SaveSlotScreen`, and `NewWorldSetupScreen` in `src/Autonocraft/UI/`

**Checkpoint**: User Story 4 is complete when menu screens feel like one cohesive product with consistent motion and focus states.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Regression gates, bypass paths, and manual validation.

- [X] T047 Run focused menu test filter and fix failures in `tests/Autonocraft.Tests/Integration/MenuTests.cs`
- [X] T048 Run required integration suite `dotnet run --project src/Autonocraft -- --test` and record exit code 0
- [ ] T048b Run unit test gate `dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Unit"` and record exit code 0
- [X] T049 [P] Verify `--skip-menu` bypass unchanged in `src/Autonocraft/Game/GameStateMachine.cs`
- [X] T050 [P] Verify `--structure-gallery` bypass unchanged in `src/Autonocraft/Program.cs`
- [X] T051 Execute manual validation checklist in `specs/003-revamp-main-menu/quickstart.md`
- [X] T052 Update menu flow description in `README.md` if player-facing entry behavior changed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **blocks all user stories**
- **User Story 1 (Phase 3)**: Depends on Foundational — MVP
- **User Story 2 (Phase 4)**: Depends on Foundational; integrates with US1 hub navigation
- **User Story 3 (Phase 5)**: Depends on Foundational; integrates with US1/US2 layers
- **User Story 4 (Phase 6)**: Depends on US1–US3 screens existing; polish pass across all
- **Polish (Phase 7)**: Depends on desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Independent after Foundational — delivers MVP root hub
- **User Story 2 (P2)**: Uses US1 hub entry; save browser independently testable once hub exists
- **User Story 3 (P3)**: Uses US1/US2 layers for overlay entry/exit; settings logic mostly existing
- **User Story 4 (P4)**: Cross-cutting polish; best after US1–US3 functional paths exist

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Navigation state before screen-specific actions
- Screen draw/update before GameStateMachine routing refinements
- Story checkpoint before next priority

### Parallel Opportunities

- Phase 1: T002–T005 in parallel after T001
- Phase 2: T007–T008 in parallel; T012–T013 after scaffold design
- US1: T014–T015 in parallel; T016–T018 can start in parallel on `MainMenuScreen.cs` after T006–T008
- US2: T023–T024 in parallel; T025–T028 touch `SaveSlotScreen.cs` sequentially
- US3: T033–T034 in parallel
- US4: T041–T042 in parallel
- Polish: T049–T050 in parallel

---

## Parallel Example: User Story 1

```bash
# Tests first (parallel):
Task T014: RunMenuInitialLayerIsRootHub in tests/Autonocraft.Tests/Integration/MenuTests.cs
Task T015: RunMainMenuRootHubLayoutBounds in tests/Autonocraft.Tests/Integration/MenuTests.cs

# Implementation (after tests fail):
Task T016: Extend MainMenuScreen actions in src/Autonocraft/UI/MainMenuScreen.cs
Task T017: Wire MenuFocusList in src/Autonocraft/UI/MainMenuScreen.cs
Task T018: Remove hidden Escape quit in src/Autonocraft/UI/MainMenuScreen.cs
```

---

## Parallel Example: User Story 4

```bash
# Visual cohesion (parallel, different files):
Task T041: MenuChrome on SaveSlotScreen in src/Autonocraft/UI/SaveSlotScreen.cs
Task T042: MenuChrome on NewWorldSetupScreen in src/Autonocraft/UI/NewWorldSetupScreen.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Launch game, verify Root Hub and keyboard navigation
5. Demo if ready

### Incremental Delivery

1. Setup + Foundational → shared navigation ready
2. User Story 1 → Root Hub MVP
3. User Story 2 → save browser polish and world setup paths
4. User Story 3 → settings/stats overlay navigation
5. User Story 4 → visual cohesion pass
6. Polish → integration suite + quickstart

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (MVP)
   - Developer B: User Story 2 (after US1 hub routing lands)
   - Developer C: User Story 3 overlays
3. User Story 4 as final cohesion pass across all screens

---

## Notes

- [P] tasks = different files, no dependencies on incomplete sibling tasks
- [Story] label maps task to user story for traceability
- Do not remove existing save browser capabilities (rename, delete confirm, structure gallery, lifetime stats)
- `MainMenuScreen` already exists but is unused — extend and wire rather than duplicate
- Avoid scope creep into pause menu (`src/Autonocraft/UI/PauseMenuScreen.cs`) unless sharing `MenuChrome` without layout changes
