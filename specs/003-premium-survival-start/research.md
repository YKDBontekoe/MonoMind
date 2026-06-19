# Research: Premium Survival Start & Recipe Book

## Decision: Empty Player Inventory via Explicit Survival Bootstrap

**Rationale**: `Player` constructor currently hard-codes starter hotbar items (grass, logs, dirt, wood pickaxe, wood axe). Survival empty-start belongs in player initialization, not scattered across `GameSession` or save load. Add `Player.InitializeForSurvival()` (or constructor parameter `SurvivalLoadout.Empty`) called from new-game paths only; loaded saves continue using persisted hotbar from `WorldSaveManager`.

**Alternatives considered**: Clear hotbar in `GamePersistenceCoordinator` after spawn. Rejected because tests and `--skip-menu` paths that construct `Player` directly would diverge; constructor/bootstrap helper keeps one source of truth.

## Decision: Bare-Hand Progression Already Supported — Verify, Do Not Rebuild

**Rationale**: `MiningCalculator` allows breaking wood and earth at base break time with empty hands; stone is slow but possible. `BlockInteractionSystem` already drops block loot. No new mining system required — add integration test proving log break → pickup → plank craft on fresh player.

**Alternatives considered**: Minecraft-style punch-only wood (no stone without pick). Current game already penalizes stone with `WrongToolHardBlockPenalty`; acceptable for v1 without retuning break tables.

## Decision: Decouple Recipe Visibility and Craft Matching From Discovery Journal

**Rationale**: Today three paths filter by unlock:
- `RecipeBookResolver.GetVisibleRecipes` hides locked recipes and shows `"???"`.
- `CraftRecipeRegistry.AvailableForStation` filters `RequiresUnlock` for grid matching.
- `CraftingSystem.UnlockDefaultToolRecipes()` pre-unlocks wood recipes on init.

For Minecraft-like behavior, `RequiresUnlock` becomes metadata for journal/achievements only. Recipe book uses `CraftRecipeRegistry.ForStation(stationType)` (all recipes). `GridCrafting.FindMatch` and crucible transmute use the same unfiltered station list. Journal continues recording unlocks on first craft/acquire for stats and optional toasts.

**Alternatives considered**: Keep crafting gated but show recipes — rejected because players would see stone pickaxe ingredients but fail to craft until arbitrary unlock chain; contradicts spec acceptance scenarios.

## Decision: Centralize Recipe Book Read Model in Crafting Domain

**Rationale**: Add `RecipeBookEntry` builder in `Autonocraft.Crafting` (e.g. `RecipeBookFormatter` or methods on `CraftRecipe`) producing display name, ingredient summary string, craftability enum (`Craftable`, `MissingMaterials`, `NeedsBench`, `NeedsEnvironment`), and missing-requirement hint. UI (`RecipeBookPanel`) consumes read model only — no duplicate unlock logic in UI layer.

**Alternatives considered**: UI-only string formatting from raw `CraftRecipe`. Rejected because agent HTTP and tests should share the same craftability rules.

## Decision: Extend RecipeBookPanel With Ingredient Preview Row

**Rationale**: Spec P3 requires ingredient summaries on hover/selection. Extend each recipe row with a secondary caption line (e.g. `"2× Oak Log → 2× Oak Plank"` or shaped pattern legend) using existing `CraftInput` / `ShapedPattern` data. Selected/hovered row shows expanded detail; list remains scrollable at 1280×720.

**Alternatives considered**: Separate detail pane. Acceptable follow-up; secondary caption per row satisfies SC-004 with minimal layout change.

## Decision: Rewrite EarlyGameGuide Stages for Empty-Inventory Flow

**Rationale**: Current stage 0 pushes rations at Town Heart; stage 2 assumes crafting sticks without gathering. Replace with inventory-aware stages:
0 — punch trees / gather wood  
1 — craft planks and sticks (inventory screen + recipe book)  
2 — craft first tool or secure food  
3 — introduce Town Board / settlement after basic self-sufficiency  
4+ — existing settlement growth beats  

Use `player.Hotbar`/`Inventory` emptiness and item-type checks to pick HUD hints via `GetGuidanceHint`.

**Alternatives considered**: New tutorial overlay system. Out of scope; extend existing `EarlyGameGuide` + toasts.

## Decision: Early Survival Milestones via PlayerStatistics Flags

**Rationale**: FR-013 requires milestone feedback without spam. Add persisted bitmask or individual bool flags on `PlayerStatistics` (`HasGatheredLog`, `HasCraftedPlank`, `HasCraftedTool`, `HasEatenFood`) with one-time toast triggers in `EarlySurvivalMilestones` helper hooked from block break, craft success, and eat events.

**Alternatives considered**: Reuse `EarlyGuideStage` only. Insufficient granularity for multiple one-shot toasts within a stage.

## Decision: Village Starter Storage Unchanged in v1

**Rationale**: Spec Assumptions defer village bootstrap loot (Town Heart storage with planks, tools, cooked meat) unless playtesting fails. Implementation focuses on empty **player** inventory. Document in plan for optional follow-up task if empty-start playtests feel undermined by shared storage.

**Alternatives considered**: Strip village storage in same PR. Rejected to limit save/simulation scope; can add after manual validation.

## Decision: Remove UnlockDefaultToolRecipes for Fresh Journals

**Rationale**: Pre-unlocking wood recipes on `CraftingSystem` init defeats empty-start discovery toasts and confuses tests counting visible recipe totals. Fresh worlds start with empty journal; unlocks fire on first craft/acquire as today.

**Alternatives considered**: Keep pre-unlock for recipe book only. Unnecessary once visibility is decoupled from journal.

## Decision: Agent API Additive Fields Only

**Rationale**: `GET /state` may expose `recipeBookVisibleCount`, `earlyGuideStage`, and per-hotbar emptiness for automation. No breaking changes to existing inventory arrays. Optional `guidanceHint` string updates automatically via `EarlyGameGuide`.

**Alternatives considered**: New `/crafting/recipes` endpoint. Deferred; not required for player parity.

## Decision: Testing Strategy

**Rationale**: Constitution requires `dotnet run --project src/Autonocraft -- --test` for this domain. Add `SurvivalStartTests.cs` and extend `CraftingTests.cs`:
- `RunEmptySurvivalStart` — new player hotbar all empty
- `RunBareHandLogProgression` — break log, craft plank without pre-unlocked journal entries
- `RunRecipeBookShowsAllBenchRecipes` — count matches `ForStation(StationBench).Count`, no `"???"` names
- `RunRecipeBookCraftabilityRefresh` — simulate inventory change, verify craftable flag flips

Manual quickstart covers 15-minute fresh session and recipe book at 1280×720.
