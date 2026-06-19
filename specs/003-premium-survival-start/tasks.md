# Tasks: Premium Survival Start & Recipe Book

**Input**: Design documents from `/specs/003-premium-survival-start/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required. This feature touches player inventory, crafting, early guidance, UI,
and saves. The final gate includes `dotnet run --project src/Autonocraft -- --test`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish baseline understanding of player bootstrap, recipe book, crafting unlock paths, and test registration before changing behavior.

- [X] T001 Review starter hotbar assignment and inventory slots in `src/Autonocraft.Core/Player/Player.cs`
- [X] T002 [P] Review recipe book visibility and `"???"` rendering in `src/Autonocraft.Crafting/Recipes/RecipeBookResolver.cs` and `src/Autonocraft/UI/RecipeBookPanel.cs`
- [X] T003 [P] Review journal-gated crafting in `src/Autonocraft.Crafting/Grid/GridCrafting.cs`, `src/Autonocraft.Crafting/Recipes/CraftRecipeRegistry.cs`, and `src/Autonocraft.Crafting/CraftingSystem.cs`
- [X] T004 [P] Review early guide stages and HUD hints in `src/Autonocraft.Core/Game/EarlyGameGuide.cs`
- [X] T005 [P] Review integration test registration in `tests/Autonocraft.Tests/IntegrationTestRunner.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared crafting read model and decouple recipe visibility/matching from `DiscoveryJournal` unlocks — required before User Stories 3 and 4.

**CRITICAL**: User Story 3 and User Story 4 must not begin until this phase is complete. User Story 1 may proceed in parallel after Phase 1.

- [X] T006 Add `RecipeBookEntry`, `RecipeBookCraftability`, and formatting helpers in `src/Autonocraft.Crafting/Recipes/RecipeBookEntry.cs`
- [X] T007 Add `RecipeBookFormatter` to build entries from `CraftRecipe`, inventory, grid size, and environment in `src/Autonocraft.Crafting/Recipes/RecipeBookFormatter.cs`
- [X] T008 Change `RecipeBookResolver.GetVisibleRecipes` to return all `CraftRecipeRegistry.ForStation` recipes (no journal filter) in `src/Autonocraft.Crafting/Recipes/RecipeBookResolver.cs`
- [X] T009 Change `GridCrafting.FindMatch` to match against unfiltered station recipes in `src/Autonocraft.Crafting/Grid/GridCrafting.cs`
- [X] T010 Change `CraftingSystem.TryTransmute` and `TryTransmuteToContainer` to use unfiltered station recipe lists in `src/Autonocraft.Crafting/CraftingSystem.cs`
- [X] T011 Remove or gate `UnlockDefaultToolRecipes()` on fresh journal init in `src/Autonocraft.Crafting/CraftingSystem.cs`
- [X] T012 Document `ForStation` vs `AvailableForStation` semantics in `src/Autonocraft.Crafting/Recipes/CraftRecipeRegistry.cs`

**Checkpoint**: Crafting and recipe book queries no longer hide recipes by journal unlock; formatter produces shared read model.

---

## Phase 3: User Story 1 - Start From Nothing (Priority: P1) MVP

**Goal**: New survival worlds spawn with empty hotbar and inventory; bare-hand gathering enables first craft without starter loot.

**Independent Test**: Start a new survival game, open inventory immediately — all slots empty. Punch a tree, pick up log, craft plank without pre-granted items. Load an existing save — inventory unchanged.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [X] T013 [P] [US1] Add `SurvivalStartTests.cs` with `RunEmptySurvivalStart` asserting all hotbar slots empty on new player in `tests/Autonocraft.Tests/Integration/SurvivalStartTests.cs`
- [X] T014 [P] [US1] Add `RunBareHandLogProgression` (log pickup → plank craft without pre-unlocked journal) in `tests/Autonocraft.Tests/Integration/SurvivalStartTests.cs`

### Implementation for User Story 1

- [X] T015 [US1] Replace starter hotbar blocks and tools with empty slots in `src/Autonocraft.Core/Player/Player.cs`
- [X] T016 [US1] Add `InitializeSurvivalLoadout()` that clears hotbar and main inventory in `src/Autonocraft.Core/Player/Player.cs`
- [X] T017 [US1] Call survival bootstrap on new-game path only (not save load) in `src/Autonocraft.Core/Game/GameSession.cs`
- [X] T018 [US1] Ensure loaded saves skip empty-start bootstrap in `src/Autonocraft/Game/GamePersistenceCoordinator.cs`
- [X] T019 [US1] Update welcome toast to gather-first copy without granting player items in `src/Autonocraft.Village/Founding/VillageFoundingService.cs`
- [X] T020 [US1] Register `SurvivalStartTests` in `tests/Autonocraft.Tests/IntegrationTestRunner.cs`

**Checkpoint**: User Story 1 complete when new survival sessions start empty and bare-hand log → plank progression works.

---

## Phase 4: User Story 3 - Always-Visible Recipe Book (Priority: P1)

**Goal**: Recipe book lists every station recipe by name with craftability styling; no `"???"` or empty lists for registered recipes.

**Independent Test**: Fresh world, empty inventory, open bench and press **B** — all bench recipes visible by name; craftable rows highlight when materials allow.

**Depends on**: Phase 2 (Foundational)

### Tests for User Story 3

- [X] T021 [P] [US3] Add `RunRecipeBookShowsAllBenchRecipes` counting visible vs `ForStation(StationBench)` in `tests/Autonocraft.Tests/Integration/CraftingTests.cs`
- [X] T022 [P] [US3] Add `RunRecipeBookNoHiddenNames` asserting no `"???"` display names in `tests/Autonocraft.Tests/Integration/CraftingTests.cs`

### Implementation for User Story 3

- [X] T023 [US3] Wire `RecipeBookFormatter` into inventory recipe book draw/update in `src/Autonocraft/UI/InventoryScreen.cs`
- [X] T024 [US3] Wire `RecipeBookFormatter` into crucible/bench recipe book draw/update in `src/Autonocraft/UI/CrucibleScreen.cs`
- [X] T025 [US3] Always render `RecipeBookEntry.displayName` and remove `"???"` branch in `src/Autonocraft/UI/RecipeBookPanel.cs`
- [X] T026 [US3] Apply craftable vs non-craftable row styling per `specs/003-premium-survival-start/contracts/recipe-book-ui-contract.md` in `src/Autonocraft/UI/RecipeBookPanel.cs`
- [X] T027 [US3] Show missing-material toast when clicking non-craftable recipe in `src/Autonocraft/Game/GameOverlayRouter.cs`
- [X] T028 [US3] Register new crafting tests in `tests/Autonocraft.Tests/IntegrationTestRunner.cs`

**Checkpoint**: User Story 3 complete when recipe book shows full station lists with correct craftability highlights.

---

## Phase 5: User Story 2 - Premium First-Survival Loop (Priority: P2)

**Goal**: Inventory-aware early guidance and one-shot milestone toasts guide gather → craft → food → settlement without granting items.

**Independent Test**: Play 15 minutes from empty start — HUD hints prioritize punching trees before Town Board; milestone toasts fire once for first log, plank, tool, and food.

**Depends on**: User Story 1 (empty start)

### Tests for User Story 2

- [X] T029 [P] [US2] Add `RunEarlyGuideEmptyInventoryHints` asserting gather-first headline when hotbar empty in `tests/Autonocraft.Tests/Integration/SurvivalStartTests.cs`
- [X] T030 [P] [US2] Add `RunSurvivalMilestoneSaveRoundTrip` for milestone flags in `tests/Autonocraft.Tests/Integration/SurvivalStartTests.cs`

### Implementation for User Story 2

- [X] T031 [US2] Add milestone boolean fields to `src/Autonocraft.Core/Player/PlayerStatistics.cs`
- [X] T032 [US2] Persist milestone fields in `src/Autonocraft.Domain/Persistence/WorldSaveDtos.cs` and `src/Autonocraft.Core/World/WorldSaveManager.cs`
- [X] T033 [US2] Add one-shot toast helper in `src/Autonocraft.Core/Game/EarlySurvivalMilestones.cs`
- [X] T034 [US2] Rewrite stages and `GetGuidanceHint` for empty-inventory progression in `src/Autonocraft.Core/Game/EarlyGameGuide.cs`
- [X] T035 [US2] Trigger gather milestone on first block/item pickup in `src/Autonocraft.Core/Player/Player.Inventory.cs`
- [X] T036 [US2] Trigger craft and tool milestones on successful craft in `src/Autonocraft.Crafting/CraftingSystem.cs`
- [X] T037 [US2] Trigger food milestone on first eat in `src/Autonocraft.Core/Player/Player.cs` or food consumption helper
- [X] T038 [US2] Add integration test confirming respawn does not restock starter loot in `tests/Autonocraft.Tests/Integration/SurvivalStartTests.cs`

**Checkpoint**: User Story 2 complete when guidance adapts to inventory state and milestones fire without spam.

---

## Phase 6: User Story 4 - Recipe Book as Progression Companion (Priority: P3)

**Goal**: Recipe rows show ingredient/output summaries; craftability updates live while book is open; grid and environment blockers explained.

**Independent Test**: Partial inventory (logs, no stone) — stone recipes show ingredient requirements; acquiring materials updates craftable state without reopening book.

**Depends on**: User Story 3 (recipe book UI wired)

### Tests for User Story 4

- [X] T039 [P] [US4] Add `RunRecipeBookCraftabilityRefresh` simulating inventory change while book open in `tests/Autonocraft.Tests/Integration/CraftingTests.cs`
- [X] T040 [P] [US4] Add `RunRecipeBookIngredientSummary` asserting non-empty ingredient text for bench recipes in `tests/Autonocraft.Tests/Integration/CraftingTests.cs`

### Implementation for User Story 4

- [X] T041 [US4] Populate `ingredientSummary`, `outputSummary`, and `missingHint` in `src/Autonocraft.Crafting/Recipes/RecipeBookFormatter.cs`
- [X] T042 [US4] Add secondary ingredient caption line per recipe row in `src/Autonocraft/UI/RecipeBookPanel.cs`
- [X] T043 [US4] Show expanded input/output detail on hovered or selected row in `src/Autonocraft/UI/RecipeBookPanel.cs`
- [X] T044 [US4] Classify `NeedsLargerGrid` and `NeedsEnvironment` craftability states in `src/Autonocraft.Crafting/Recipes/RecipeBookFormatter.cs`
- [X] T045 [US4] Rebuild recipe entries each frame while book is open in `src/Autonocraft/UI/InventoryScreen.cs`
- [X] T046 [US4] Rebuild recipe entries each frame while book is open in `src/Autonocraft/UI/CrucibleScreen.cs`

**Checkpoint**: User Story 4 complete when players can plan gathers from recipe book alone.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Agent parity, documentation, and full regression gate.

- [X] T047 [P] Expose optional `survivalMilestones` object in `src/Autonocraft.Core/Agent/Serialization/AgentStateSerializer.cs` per `specs/003-premium-survival-start/contracts/agent-state-contract.md`
- [X] T048 [P] Update integration test list in `AGENTS.md` for new survival and recipe book tests
- [X] T049 Run full headless integration suite: `dotnet run --project src/Autonocraft -- --test`
- [X] T050 Execute manual validation checklist in `specs/003-premium-survival-start/quickstart.md` sections 3–6

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **blocks US3 and US4**
- **User Story 1 (Phase 3)**: Depends on Setup only — can run **parallel to Phase 2**
- **User Story 3 (Phase 4)**: Depends on Phase 2
- **User Story 2 (Phase 5)**: Depends on User Story 1
- **User Story 4 (Phase 6)**: Depends on User Story 3
- **Polish (Phase 7)**: Depends on all desired user stories

### User Story Dependencies

| Story | Priority | Depends on | Independent test |
|-------|----------|------------|------------------|
| US1 — Start From Nothing | P1 | Phase 1 | Empty hotbar + bare-hand log → plank |
| US3 — Always-Visible Recipe Book | P1 | Phase 2 | All bench recipes visible by name on fresh world |
| US2 — Premium First-Survival Loop | P2 | US1 | Gather-first hints + one-shot milestones |
| US4 — Progression Companion | P3 | US3 | Ingredient summaries + live craftability refresh |

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Domain/crafting changes before UI wiring
- UI wiring before overlay/toast integration
- Story checkpoint before next priority

### Parallel Opportunities

- **Phase 1**: T002, T003, T004, T005 in parallel
- **Phase 2 + Phase 3**: Different owners can run US1 (Phase 3) alongside Foundational (Phase 2)
- **US1 tests**: T013, T014 in parallel
- **US3 tests**: T021, T022 in parallel
- **US2 tests**: T029, T030 in parallel
- **US4 tests**: T039, T040 in parallel
- **Polish**: T047, T048 in parallel

---

## Parallel Example: User Story 1

```bash
# Tests first (parallel):
T013: SurvivalStartTests.cs — RunEmptySurvivalStart
T014: SurvivalStartTests.cs — RunBareHandLogProgression

# Then sequential implementation T015–T020
```

## Parallel Example: User Story 3

```bash
# After Phase 2 completes, launch tests together:
T021: RunRecipeBookShowsAllBenchRecipes
T022: RunRecipeBookNoHiddenNames

# UI wiring can split:
T023: InventoryScreen.cs
T024: CrucibleScreen.cs
```

---

## Implementation Strategy

### MVP First (P1 stories: US1 + US3)

1. Complete Phase 1: Setup
2. Complete Phase 3: User Story 1 (empty start)
3. Complete Phase 2: Foundational (crafting decouple)
4. Complete Phase 4: User Story 3 (always-visible recipe book)
5. **STOP and VALIDATE**: Run T049 integration suite + quickstart sections 3–4

### Incremental Delivery

1. Setup + US1 → playable empty-start survival (MVP slice A)
2. Foundational + US3 → full recipe book visibility (MVP slice B)
3. US2 → polished early guidance and milestones
4. US4 → ingredient previews and live craftability
5. Polish → agent fields, docs, full regression

### Parallel Team Strategy

| Developer | Focus |
|-----------|-------|
| A | Phase 3 (US1) — player bootstrap + survival tests |
| B | Phase 2 + Phase 4 (US3) — crafting decouple + recipe book UI |
| C | Phase 5 (US2) after US1 merges — early guide + milestones |

---

## Notes

- Village starter storage (Town Heart shared items) unchanged in v1 per spec assumptions
- Creative mode MUST NOT apply empty-start rules
- `RequiresUnlock` retained on recipes for journal/achievements only — not for visibility or matching
- Commit after each task or logical group; stop at any checkpoint to validate story independently
