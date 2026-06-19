# Quickstart: Improved Villager Flow Validation

## Prerequisites

- .NET 10 SDK installed
- Python 3 for `tests/interact.py` (optional manual pass)
- Working tree contains the villager flow improvements

## 1. Run Required Integration Tests

```bash
dotnet run --project src/Autonocraft -- --test
```

Expected result:

```text
ALL TESTS PASSED SUCCESSFULLY! (EXIT CODE: 0)
```

Mandatory because this feature touches villagers, villages, UI layout tests,
guidance, saves, and agent state serialization.

Focused village tests (also run inside `--test`):

- `RunVillageScreenInputLayout`
- `RunVillageGuidanceHints`
- New: settlement guidance priority and blocked assign/recruit reason tests

## 2. Headless Guidance Sanity Check

```bash
dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Village"
```

Expected: all village-related tests pass.

## 3. Manual Town Board Walkthrough (New Player Path)

Terminal 1:

```bash
dotnet run --project src/Autonocraft -- --skip-menu
```

In-game:

1. Wait for starter settlement to load.
2. Press **V** to open Town Board.
3. **Verify Overview** shows population, food, next action, and idle guidance
   without switching tabs.
4. Open **People**, select a settler, assign **Lumber** or **Build**.
5. **Verify** inline confirmation or blocked reason on the same screen.
6. Close Town Board; confirm HUD hint aligns with Overview next action.

## 4. Manual Recruit and Blocked-State Check

With starter settlement and storage state:

1. Open Town Board footer **Recruit** area.
2. **Verify** cost and housing requirements visible before clicking.
3. If at housing cap, attempt recruit and confirm remediation text mentions
   queuing housing on Build tab.

## 5. Agent API Parity (Optional)

Terminal 1: `dotnet run --project src/Autonocraft -- --skip-menu`

Terminal 2:

```bash
python3 tests/interact.py wait
python3 tests/interact.py state
```

Expected:

- `guidanceHint` present for early settlement
- When implemented: `village.nextAction` and `villagers[].activity` align with
  Town Board copy (see [agent-state-contract.md](contracts/agent-state-contract.md))

Test blocked assign via HTTP:

```bash
python3 tests/interact.py action assign_job villager_id=1 job=Mine
```

When no quarry exists, `message` should explain the blocker.

## 6. In-World Affordance Check (Optional)

1. Approach a villager in the world.
2. Confirm HUD or nameplate shows activity text matching People tab.
3. Use villager interaction affordance (when implemented) to open People detail.

## 7. Save Round-Trip

Run integration test `TestVillageSaveRoundTripV7` (included in `--test`) or:

1. Assign jobs and recruit if possible.
2. Save and reload world slot.
3. Confirm assignments and food stock persist.

## Success Checklist

| Criterion | How to verify |
|-----------|---------------|
| SC-001 Next action clarity | Manual: Overview shows actionable next step in < 30s |
| SC-002 Fast job assign | Manual: assign job in < 15s from opening Town Board |
| SC-003 Blocked reasons | Integration tests + manual recruit/assign failures |
| SC-005 1280×720 layout | `RunVillageScreenInputLayout` |
| SC-006 No regressions | Full `--test` green |

## References

- UI behavior: [village-ui-contract.md](contracts/village-ui-contract.md)
- Agent fields: [agent-state-contract.md](contracts/agent-state-contract.md)
- Entities: [data-model.md](data-model.md)
