# Quickstart: Villager Onboarding Validation

## Prerequisites

- .NET 10 SDK installed
- A checkout of the repository with the villager onboarding changes

## 1. Run the Required Test Suite

```bash
dotnet run --project src/Autonocraft -- --test
```

Expected result:

```text
ALL TESTS PASSED SUCCESSFULLY! (EXIT CODE: 0)
```

This is mandatory because the feature touches villagers, villages, UI, and settlement state recovery.

## 2. Verify the Starter Flow Manually

```bash
dotnet run --project src/Autonocraft -- --skip-menu
```

Validation steps:

1. Open the villager or settlement management flow.
2. Confirm the current starter state is visible immediately.
3. Confirm the next action is clear and the player is not blocked by an empty or broken screen.
4. Try a valid recruit or summon action.
5. Confirm the roster and counts update immediately after success.

## 3. Verify Blocked-State Messaging

Set up a village state where recruitment should fail, then retry the action.

Expected result:

- The UI shows the blocker reason in plain language.
- The UI shows what the player should do next.
- The flow remains open and usable after the failure.

## 4. Verify Layout Readability

Use the existing village layout test path and ensure it still passes:

```bash
dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~VillageScreen"
```

Expected result:

- No overlapping controls
- No clipped primary actions
- Status and roster information remain readable at supported sizes

## 5. Check State Recovery

1. Open the villager flow.
2. Change the settlement state.
3. Close and reopen the flow.

Expected result:

- The UI reflects the latest state
- Counts are accurate
- No stale or duplicated villager information appears

## References

- Plan: [plan.md](plan.md)
- Model: [data-model.md](data-model.md)
- UI contract: [contracts/villager-onboarding-ui-contract.md](contracts/villager-onboarding-ui-contract.md)
- Agent contract: [contracts/agent-state-contract.md](contracts/agent-state-contract.md)
