# Contract: Agent State Extensions (Survival Start & Crafting)

## Purpose

Keep automation aligned with empty survival start and open recipe book behavior. All changes are **additive** and backwards compatible.

## Existing Workflow (unchanged)

1. Start game (`--skip-menu` or normal load).
2. `GET /health` until `gameState` is `Playing`.
3. `GET /state` for player and crafting context.

## `GET /state` — Player Inventory

Existing `hotbar[]` and inventory fields MUST remain. For a **new** survival session after this feature:

- All hotbar slots SHOULD report `empty: true` or equivalent zero-count stacks immediately after spawn.

Loaded saves MUST reflect persisted inventory unchanged.

## `GET /state` — Additive Fields (optional)

| Field | Type | Description |
|-------|------|-------------|
| `earlyGuideStage` | int | Already present — stage meanings updated for empty-start flow |
| `survivalMilestones` | object | Optional `{ gathered, craftedPlank, craftedTool, securedFood }` booleans |

## `GET /state` — Guidance

| Field | Type | Description |
|-------|------|-------------|
| `guidanceHint` | string | MUST reflect empty-inventory hints when hotbar is empty (e.g. punch trees before Town Board) |

Existing semantics preserved; string content changes only.

## Crafting Actions

No new HTTP commands required. Existing inventory/craft flows via gameplay input.

Agent tests MAY verify:

- Fresh `--skip-menu` session has empty hotbar in `/state`
- After dev/give or mining via action commands, crafting remains functional

## Save Compatibility

- Journal unlock arrays in save files unchanged
- New milestone flags optional in `PlayerStatisticsSaveData` with safe defaults on load

## Validation Expectations

- `guidanceHint` for empty hotbar MUST NOT reference pre-equipped tools
- Loaded world inventory MUST match save file regardless of empty-start rules
