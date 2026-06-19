# Data Model: Premium Survival Start & Recipe Book

## Survival Start Profile

Configuration for what a new survival player receives. Not a persisted entity; applied once at new-game spawn.

**Fields**

- `emptyInventory`: bool — true for survival default (hotbar + main inventory empty)
- `creativeBypass`: bool — when `Player.CreativeMode`, ignore empty start
- `preserveOnLoad`: bool — always true; saves override bootstrap

**Relationships**

- Applied by `GameSession` / `GamePersistenceCoordinator` on new world creation only.

**Validation Rules**

- When `emptyInventory` is true, all 9 hotbar slots and extended inventory slots MUST be `ItemStack.Empty` after spawn.
- Creative mode MUST NOT clear existing creative inventory conventions.

## Player Inventory (existing, behavior change)

Hotbar (9 slots) and main inventory holding blocks, tools, materials, food.

**State Transitions**

- New survival world → all slots empty
- Save load → restored from `PlayerSaveData`
- Death → `DeathConsequences.ApplyOnDeath` (unchanged); no starter restock on respawn

**Validation Rules**

- FR-004: no code path grants items on spawn, first inventory open, or welcome toast in survival.

## Recipe Book Entry (read model)

Presentation row for one recipe in the recipe book UI. Not persisted.

**Fields**

- `recipeId`: string
- `displayName`: string — always the recipe's real name
- `ingredientSummary`: string — human-readable inputs (e.g. `"1× Oak Log"`, `"P×2 + T×1"`)
- `outputSummary`: string — e.g. `"4× Stick"`, `"Wood Pickaxe"`
- `craftability`: enum — `Craftable`, `MissingMaterials`, `NeedsLargerGrid`, `NeedsEnvironment`
- `missingHint`: string? — e.g. `"Need 2× Stick"`, `"Requires bench (3×3)"`, `"Requires heat"`
- `sortKey`: string — display name for ordering

**Relationships**

- Built from `CraftRecipe` + `IItemContainer` + active `CraftGridSize` + optional `CraftEnvironment`.

**Validation Rules**

- `displayName` MUST NOT be `"???"` for registered recipes.
- `craftability == Craftable` iff `RecipeBookResolver.CanCraftWithInventory` (or env-aware variant) returns true.
- Recipes with `GridSize` larger than active grid appear with `NeedsLargerGrid`, still listed.

## Craft Recipe (existing domain entity)

Station-bound crafting definition. `RequiresUnlock` retained for journal semantics but ignored for visibility and grid matching after this feature.

**Fields** (relevant)

- `Id`, `DisplayName`, `StationType`, `Inputs`, `ShapedPattern`, `GridSize`
- `RequiresUnlock` — journal/achievement only post-change
- `RequiresHeat`, `RequiresWater`, environment fields — still gate craftability status

## Discovery Journal (existing, reduced role)

Tracks unlocked recipe and sigil IDs for saves, journal screen, and optional toasts.

**State Transitions**

- Unlock on first craft, item acquire, or sigil activation (unchanged)
- No longer filters recipe book or grid matching

**Validation Rules**

- Save/load round-trip for `UnlockedIds` unchanged (FR-014).

## Early Survival Guidance

Dynamic hints and one-shot milestone toasts for the first session.

**Fields**

- `stage`: int — `PlayerStatistics.EarlyGuideStage` (rewritten stage meanings)
- `headline`: string — HUD one-liner from `EarlyGameGuide.GetGuidanceHint`
- `inventoryAware`: bool — hints consider empty vs partial inventory

**Relationships**

- Consumed by HUD, optional Town Board context note, agent `guidanceHint`.

## Early Survival Milestones (new persisted flags)

One-shot player progress markers on `PlayerStatistics`.

**Fields**

- `hasGatheredResource`: bool
- `hasCraftedPlank`: bool
- `hasCraftedTool`: bool
- `hasSecuredFood`: bool

**State Transitions**

- Set true on first occurrence; triggers toast once via `EarlySurvivalMilestones`

**Validation Rules**

- Persisted in save data (extend `PlayerStatisticsSaveData` if not using existing counters).
- Must not re-fire toast after save/load.

## Recipe Book View State (UI session)

Ephemeral UI state in `RecipeBookPanel` / `CraftingSystem`.

**Fields**

- `isOpen`: bool — `CraftingSystem.RecipeBookOpen`
- `scrollOffset`: int
- `hoveredIndex`: int
- `selectedRecipeId`: string?

**Validation Rules**

- Craftability rows refresh each frame or on inventory mutation while open (FR-010).
