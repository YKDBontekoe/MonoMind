# Implementation Plan: Premium Survival Start & Recipe Book

**Branch**: `003-premium-survival-start` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-premium-survival-start/spec.md`

## Summary

Deliver a Minecraft-like survival opening and an always-visible recipe book by (1) removing
starter hotbar loot for new survival worlds, (2) rewriting early-game guidance and one-shot
milestones for gather → craft → food → settlement progression, and (3) decoupling recipe
visibility and grid matching from `DiscoveryJournal` unlocks while enriching `RecipeBookPanel`
with ingredient summaries and live craftability states. Work extends existing
`Player`, `EarlyGameGuide`, `RecipeBookResolver`, `GridCrafting`, `CraftingSystem`, and
`RecipeBookPanel` — no new projects or save format version required unless milestone flags
need DTO fields (backward compatible defaults).

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: MonoGame 3.8 DesktopGL; existing Autonocraft crafting, items,
player, UI, and village early-guide projects

**Storage**: Extend `PlayerStatisticsSaveData` with optional milestone booleans (default
false on load). Player hotbar/inventory and journal unlock lists continue existing
`world.json` paths. No save version bump unless milestone fields require it — use safe
defaults for missing fields.

**Testing**: `dotnet run --project src/Autonocraft -- --test`; add
`tests/Autonocraft.Tests/Integration/SurvivalStartTests.cs`; extend
`CraftingTests.cs` for recipe book visibility and craftability refresh

**Target Platform**: Desktop game on macOS, Windows, and Linux through MonoGame DesktopGL

**Project Type**: Desktop voxel sandbox game with survival, crafting, and settlement onboarding

**Performance Goals**: Recipe book builds a bounded list from `CraftRecipeRegistry.ForStation`
(≤100 recipes per station). Entry formatting is O(recipes × inputs) per frame while open —
acceptable at current registry size. Target: no measurable frame-time regression when recipe
book is toggled at 1280×720.

**Constraints**: Preserve **B** recipe book toggle, **I** inventory, save compatibility for
existing worlds, creative mode behavior, and agent hotbar JSON shape. Village starter storage
unchanged in v1 per spec assumptions.

**Scale/Scope**: Player bootstrap, crafting visibility rules, one UI panel enrichment, early
guide copy/stages, and integration tests — not village economy or break-time retuning.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Code Quality and Architecture**: PASS. Empty start in `Autonocraft.Core/Player`.
  Recipe read model in `Autonocraft.Crafting` (`RecipeBookEntry`, formatter). UI in
  `src/Autonocraft/UI/RecipeBookPanel.cs`. Guidance in
  `Autonocraft.Core/Game/EarlyGameGuide.cs`. Remove unlock filter from
  `RecipeBookResolver` and `GridCrafting` / `AvailableForStation` usage for matching.
  No new projects.
- **Testing Evidence**: PASS. Mandatory integration suite plus new tests listed in
  [quickstart.md](quickstart.md). Touches inventory, crafting, UI, saves, early guide —
  constitution II applies.
- **User Experience Consistency**: PASS. **B** / **I** preserved. Agent `guidanceHint` and
  hotbar fields unchanged structurally; optional milestone object additive only. Existing
  saves keep inventory.
- **Performance Budget**: PASS. Station recipe lists are small static registries; no world
  scans. Recipe book refresh uses inventory adapter already constructed in UI update.

### Post-Design Re-check (Phase 1)

All gates remain PASS. Contracts limit changes to UI behavior docs and optional agent fields.
Journal unlock decoupling is a behavior simplification, not a new subsystem.

## Project Structure

### Documentation (this feature)

```text
specs/003-premium-survival-start/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── recipe-book-ui-contract.md
│   └── agent-state-contract.md
└── tasks.md             # Phase 2 (/speckit-tasks) — not created by /speckit-plan
```

### Source Code (repository root)

```text
src/
├── Autonocraft.Core/
│   ├── Player/
│   │   ├── Player.cs                    # Remove starter hotbar; add survival bootstrap
│   │   └── PlayerStatistics.cs          # Milestone flags
│   ├── Game/
│   │   ├── EarlyGameGuide.cs            # Empty-inventory stage rewrite
│   │   └── EarlySurvivalMilestones.cs   # NEW — one-shot toasts
│   └── Game/GameSession.cs              # Call survival bootstrap on new game
├── Autonocraft.Crafting/
│   ├── Recipes/
│   │   ├── RecipeBookResolver.cs        # All station recipes; craftability helpers
│   │   └── RecipeBookFormatter.cs       # NEW — RecipeBookEntry builder
│   ├── Grid/GridCrafting.cs             # Match against all station recipes
│   ├── Recipes/CraftRecipeRegistry.cs   # Document AvailableForStation vs ForStation
│   └── CraftingSystem.cs                # Remove UnlockDefaultToolRecipes on fresh init
├── Autonocraft/
│   ├── UI/
│   │   ├── RecipeBookPanel.cs           # Names, ingredients, craftability UI
│   │   ├── InventoryScreen.cs           # Wire formatter entries
│   │   └── CrucibleScreen.cs            # Wire formatter entries
│   └── Game/GamePersistenceCoordinator.cs
├── Autonocraft.Village/
│   └── Founding/VillageFoundingService.cs  # Welcome toast copy only (no item grants)
tests/
└── Autonocraft.Tests/Integration/
    ├── SurvivalStartTests.cs            # NEW
    └── CraftingTests.cs                 # Recipe book visibility tests
```

**Structure Decision**: Single-repo MonoGame layout; crafting domain owns recipe book read
model; UI renders it; player/core owns survival bootstrap and guidance.

## Complexity Tracking

No constitution violations requiring justification.

## Implementation Phases (for /speckit-tasks)

### Phase A — Empty Survival Start (P1)

1. Replace starter hotbar assignment in `Player` constructor with empty slots.
2. Add `Player.InitializeSurvivalLoadout()` called from new-game path in `GameSession` /
   `GamePersistenceCoordinator` (not on save load).
3. Update `VillageFoundingService` welcome toast to gather-first copy (no item grants to player).
4. Add `RunEmptySurvivalStart` integration test.

### Phase B — Open Recipe Book (P1)

1. Change `RecipeBookResolver.GetVisibleRecipes` to use `CraftRecipeRegistry.ForStation`
   without journal filter.
2. Change `GridCrafting.FindMatch` and crucible transmute candidate lists to use unfiltered
   station recipes (ignore `RequiresUnlock` for matching).
3. Remove or gate `CraftingSystem.UnlockDefaultToolRecipes()` for fresh journals.
4. Add `RecipeBookFormatter` producing `RecipeBookEntry` list with ingredient summaries.
5. Update `RecipeBookPanel.Draw` — always show names; add ingredient caption; remove `"???"`
   branch.
6. Add `RunRecipeBookShowsAllBenchRecipes` and craftability refresh test.

### Phase C — Premium Early Loop (P2)

1. Rewrite `EarlyGameGuide` stages and `GetGuidanceHint` for inventory-aware progression.
2. Add `EarlySurvivalMilestones` + `PlayerStatistics` flags; hook block pickup, craft, eat.
3. Ensure death/respawn does not restock starter loot (verify `DeathConsequences` unchanged).
4. Manual validation per [quickstart.md](quickstart.md) sections 3–5.

### Phase D — Agent & Docs (P3)

1. Optional: expose `survivalMilestones` in `AgentStateSerializer`.
2. Update `AGENTS.md` test list if new integration tests added.

## Artifacts Generated

| Artifact | Path |
|----------|------|
| Research | [research.md](research.md) |
| Data model | [data-model.md](data-model.md) |
| Quickstart | [quickstart.md](quickstart.md) |
| Recipe book contract | [contracts/recipe-book-ui-contract.md](contracts/recipe-book-ui-contract.md) |
| Agent contract | [contracts/agent-state-contract.md](contracts/agent-state-contract.md) |

## Next Step

Run **`/speckit-tasks`** to break phases into implementable tasks with test coverage.
