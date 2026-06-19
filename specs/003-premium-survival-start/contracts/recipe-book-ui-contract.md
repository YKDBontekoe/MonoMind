# Contract: Recipe Book UI

## Purpose

Define player-visible recipe book behavior after decoupling from discovery unlocks. Applies to inventory screen (2×2 player craft) and crucible/bench screen (3×3 station craft). Toggle remains **B**.

## Entry Visibility

| Condition | Expected behavior |
|-----------|-------------------|
| Player opens recipe book at bench | All recipes where `StationType == StationBench` appear |
| Player opens recipe book at forge | All forge recipes appear |
| Fresh world, empty journal | Full list still shown — never empty with "No recipes yet" when registry has entries |
| Locked `RequiresUnlock` recipe | Shows real `DisplayName` and ingredient summary — never `"???"` |

## Row Presentation

Each recipe row MUST include:

| Element | Rule |
|---------|------|
| Name | Always `CraftRecipe.DisplayName` |
| Status icon | `●` craftable, `○` visible but not craftable |
| Ingredient line | Secondary caption from `RecipeBookEntry.ingredientSummary` |
| Highlight | Craftable rows use accent highlight; non-craftable use muted fill |

## Craftability States

| State | Visual | Click behavior |
|-------|--------|----------------|
| Craftable | Accent highlight, `●` | Auto-fill crafting grid / bench slots |
| Missing materials | Muted, `○` | Toast or hint: `"Need …"`; no grid fill |
| Needs larger grid | Muted, `○` | Hint: `"Requires bench (3×3)"` when on 2×2 inventory |
| Needs environment | Muted, `○` | Hint: `"Requires heat"` / `"Requires water"` as applicable |

## Live Updates

While recipe book is open, acquiring or spending inventory items MUST update craftability indicators without closing the panel.

## Layout

- Readable at 1280×720
- Scroll wheel navigates long lists
- Footer hint: `"B toggle · click to fill grid"` (unchanged unless UX review replaces)

## Out of Scope

- Recipe categories/tabs (future enhancement)
- Village workshop crafting UI (uses separate `VillageWorkshopCrafting` path)

## Validation

See [quickstart.md](../quickstart.md) manual steps 3–4 and integration test `RunRecipeBookShowsAllBenchRecipes`.
