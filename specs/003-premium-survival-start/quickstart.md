# Quickstart: Premium Survival Start & Recipe Book Validation

## Prerequisites

- .NET 10 SDK installed
- Working tree contains survival start and recipe book improvements

## 1. Run Required Integration Tests

```bash
dotnet run --project src/Autonocraft -- --test
```

Expected result:

```text
ALL TESTS PASSED SUCCESSFULLY! (EXIT CODE: 0)
```

Mandatory because this feature touches player inventory, crafting, early guidance, UI, and saves.

New/extended tests (also run inside `--test`):

- `RunEmptySurvivalStart`
- `RunBareHandLogProgression`
- `RunRecipeBookShowsAllBenchRecipes`
- `RunRecipeBookCraftabilityRefresh`

## 2. Focused Crafting and Survival Tests

```bash
dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Crafting|FullyQualifiedName~Survival"
```

Expected: all filtered tests pass.

## 3. Manual Empty-Start Walkthrough

Terminal 1:

```bash
dotnet run --project src/Autonocraft -- --skip-menu
```

In game:

1. Immediately press **I** to open inventory.
2. **Verify** all hotbar slots are empty.
3. Punch a tree (bare hands) until a log drops; pick it up.
4. **Verify** milestone toast or HUD hint acknowledges first gather (when implemented).
5. Open recipe book (**B**) on inventory screen.
6. **Verify** all bench recipes visible by name with ingredient hints — no `"???"` rows.

## 4. Manual First-Tool Progression

Continuing fresh session:

1. Place or find a **bench** (existing starter settlement has crafting infrastructure).
2. Open bench (crucible UI), open recipe book (**B**).
3. **Verify** wood tool recipes listed even before crafting planks.
4. Craft planks → sticks → wood pickaxe using recipe book click-to-fill.
5. **Verify** craftable rows highlight as materials are acquired without reopening book.

## 5. Manual Guidance Check

1. With empty inventory, read HUD guidance hint.
2. **Verify** hint prioritizes gathering wood over Town Board rations.
3. After first tool, **Verify** hint shifts toward food or settlement (stage progression).

## 6. Agent API Check (Optional)

Terminal 1: `dotnet run --project src/Autonocraft -- --skip-menu --agent-port 5001`

Terminal 2:

```bash
python3 tests/interact.py wait
python3 tests/interact.py state
```

Expected for new session:

- Empty hotbar in state payload
- `guidanceHint` appropriate for empty start (see [agent-state-contract.md](contracts/agent-state-contract.md))

## 7. Save Round-Trip

1. Gather items and craft a tool on a new world.
2. Save and reload slot.
3. **Verify** inventory, journal unlocks, and milestone flags persist.
4. Load a **pre-change** save (if available) and confirm inventory not wiped.

## Success Checklist

| Criterion | How to verify |
|-----------|---------------|
| SC-001 Empty start | Integration `RunEmptySurvivalStart` + manual step 3 |
| SC-003 All recipes visible | Integration `RunRecipeBookShowsAllBenchRecipes` + manual step 3 |
| SC-004 Ingredient clarity | Manual: identify missing items from recipe row in < 10s |
| SC-006 Layout 1280×720 | Manual recipe book scroll at default/windowed resolution |
| SC-007 No regressions | Full `--test` green |

## References

- Recipe book UI: [recipe-book-ui-contract.md](contracts/recipe-book-ui-contract.md)
- Agent fields: [agent-state-contract.md](contracts/agent-state-contract.md)
- Entities: [data-model.md](data-model.md)
