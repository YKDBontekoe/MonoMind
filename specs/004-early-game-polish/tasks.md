# Tasks: Early Game Polish

**Input**: Design documents from `/specs/004-early-game-polish/`

**Prerequisites**: `plan.md` (required), `spec.md` (required for user stories), `research.md`, `data-model.md`, `quickstart.md`

**Tests**: Include test tasks for each gameplay change. Because this feature touches gameplay startup, world generation, villagers, UI, and rendering-adjacent presentation, the required headless integration suite must run before completion: `dotnet run --project src/Autonocraft -- --test`.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with other tasks that do not depend on it
- **[Story]**: Which user story the task belongs to (`US1`, `US2`, `US3`)
- Include exact file paths in every task description

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create shared test scaffolding for the early-game feature

- [X] T001 [P] Add reusable early-game integration test helpers in `tests/Autonocraft.Tests/Integration/EarlyGameTestHelpers.cs`
- [X] T002 [P] Add a dedicated early-game integration suite scaffold in `tests/Autonocraft.Tests/Integration/EarlyGameTests.cs` so the new scenarios are discovered by the integration runner

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish shared opening-flow behavior used by all user stories

- [X] T003 [P] Add shared first-goal text, completion checks, and repeat-suppression helpers in `src/Autonocraft.Core/Game/EarlyGameGuide.cs`
- [X] T004 [P] Add a starter-area curation hook in `src/Autonocraft.Village/Founding/VillageFoundingService.cs` so the opening area can be shaped consistently before story-specific tuning

**Checkpoint**: Early-game flow helpers are in place and user story work can proceed independently

---

## Phase 3: User Story 1 - Give New Players a Clear First Goal (Priority: P1) 🎯 MVP

**Goal**: Show a concise first objective on a fresh world, keep it dismissible, and acknowledge when the player completes it

**Independent Test**: Start a new world, confirm the opening goal appears in plain language, dismiss it, and verify the next step or reward appears after the player reaches the first milestone

### Tests for User Story 1

- [X] T005 [P] [US1] Add integration coverage for first-goal display, dismissal, and progression in `tests/Autonocraft.Tests/Integration/EarlyGameTests.cs`

### Implementation for User Story 1

- [X] T006 [P] [US1] Update `src/Autonocraft/Game/GameStateMachine.cs` to trigger the opening guidance at the correct new-world entry point and keep it non-blocking during spawn warmup
- [X] T007 [P] [US1] Update `src/Autonocraft.Core/Game/GameSession.cs` to route first-goal completion through the existing HUD/toast flow without duplicate reminders

**Checkpoint**: User Story 1 should now be fully functional and testable on its own

---

## Phase 4: User Story 2 - Make the Starting Area Feel Worth Exploring (Priority: P2)

**Goal**: Make the opening area visually and mechanically interesting enough that the player immediately has a reason to look around

**Independent Test**: Start a fresh world, look around the spawn area, and verify that a clear landmark is visible or discoverable nearby and that enough starter resources are reachable without long searching

### Tests for User Story 2

- [X] T008 [P] [US2] Add integration coverage for spawn-area landmark visibility and nearby starter resources in `tests/Autonocraft.Tests/Integration/EarlyGameSpawnTests.cs`

### Implementation for User Story 2

- [X] T009 [P] [US2] Update `src/Autonocraft.Village/Founding/VillageFoundingService.cs` to seed the starter settlement with a clearer landmark and a more obvious nearby resource cache
- [X] T010 [P] [US2] Update `src/Autonocraft.Village/Founding/VillageFoundingService.cs` to keep the immediate spawn region coherent around the starter area
- [X] T011 [US2] Update `src/Autonocraft/Game/GameStateMachine.cs` to orient the player and camera toward the starter landmark at new-world start

**Checkpoint**: User Story 2 should now be independently testable and visually distinct

---

## Phase 5: User Story 3 - Keep the Opening Polished and Non-Intrusive (Priority: P3)

**Goal**: Make the opening sequence feel smooth, readable, and consistent without trapping the player in repeated onboarding

**Independent Test**: Review the opening flow at supported window sizes, confirm prompts are readable and non-overlapping, and verify returning players do not get forced through repeated first-run messaging

### Tests for User Story 3

- [X] T012 [P] [US3] Add integration coverage for returning-player behavior and opening-layout readability in `tests/Autonocraft.Tests/Integration/EarlyGamePresentationTests.cs`

### Implementation for User Story 3

- [X] T013 [P] [US3] Refine opening prompt copy and status wording in `src/Autonocraft/UI/LoadingScreen.cs` and `src/Autonocraft.Core/Game/EarlyGameGuide.cs` so the opening flow reads as one cohesive experience
- [X] T014 [P] [US3] Tune the HUD treatment for opening guidance in `src/Autonocraft.Engine/Hud/HudToast.cs` so the prompt stays readable without crowding the hotbar
- [X] T015 [US3] Update `src/Autonocraft.Core/Player/PlayerStatistics.cs` and `src/Autonocraft.Core/Game/EarlyGameGuide.cs` so completed opening stages stay suppressed for returning players in the same world

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation sync, and regression checks

- [X] T016 [P] Update `specs/004-early-game-polish/quickstart.md` with any final manual verification steps needed for the polished opening flow
- [X] T017 Run `dotnet run --project src/Autonocraft -- --test` and confirm the early-game integration coverage in `tests/Autonocraft.Tests/Integration/EarlyGameTests.cs`, `tests/Autonocraft.Tests/Integration/EarlyGameSpawnTests.cs`, and `tests/Autonocraft.Tests/Integration/EarlyGamePresentationTests.cs`
- [X] T018 Validate the first-world experience with `tests/interact.py` and the screenshot capture path documented in `specs/004-early-game-polish/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1) has no dependencies and can start immediately
- Foundational (Phase 2) depends on Setup completion and blocks all user stories
- User Stories (Phase 3+) depend on Foundational completion
- Polish (Phase 6) depends on all desired user stories being complete

### User Story Dependencies

- User Story 1 (P1) can start after Foundational and does not depend on US2 or US3
- User Story 2 (P2) can start after Foundational and does not depend on US1
- User Story 3 (P3) can start after Foundational and does not depend on US1 or US2

### Within Each User Story

- Tests should be added before the implementation tasks for that story
- Shared helpers or staged state should be introduced before the story-specific feature code
- Story completion should be verified before moving to the next priority

## Parallel Opportunities

- Setup tasks T001 and T002 can run in parallel
- Foundational tasks T003 and T004 can run in parallel
- For US1, T006 and T007 can run in parallel after T005 is in place
- For US2, T009 and T010 can run in parallel after T008 is in place
- For US3, T013 and T014 can run in parallel after T012 is in place
- The final validation tasks T016, T017, and T018 can be scheduled together once code changes are complete

## Implementation Strategy

### MVP First

1. Complete Setup and Foundational work
2. Deliver User Story 1
3. Run `dotnet run --project src/Autonocraft -- --test`
4. Stop and validate the opening goal experience before expanding the start area polish

### Incremental Delivery

1. Add the first-goal flow and make it non-blocking
2. Add the starter-area landmark and resource curation
3. Polish the presentation and suppress repetitive onboarding for returning players
4. Finish with the integration suite, manual walkthrough, and screenshot validation

### Parallel Team Strategy

With multiple developers:

1. Developer A owns US1 first-goal guidance
2. Developer B owns US2 starter-area curation
3. Developer C owns US3 presentation polish and repeat suppression
4. One developer handles the final validation pass once the story work lands
