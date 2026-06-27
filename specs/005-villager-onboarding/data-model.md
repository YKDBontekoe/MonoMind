# Data Model: Villager Onboarding

## Starter Flow State

Represents the current state of the opening villager journey for a settlement. This is a read model assembled from existing settlement and villager data.

**Fields**

- `hasSettlement`: Whether a village or town heart exists
- `villagerCount`: Current live population in the settlement
- `starterStep`: Current onboarding step or stage
- `nextAction`: The single best next player action
- `nextActionKind`: High-level action category (`SummonSettlers`, `Recruit`, `QueueHousing`, `AssignJobs`, `AddressFood`, `None`)
- `isBlocked`: Whether the player cannot proceed yet
- `blockedReason`: Plain-language explanation when blocked
- `remediation`: Suggested next step or prerequisite

**Validation Rules**

- `nextAction` must always match the current highest-priority settlement issue.
- `blockedReason` and `remediation` must be present when `isBlocked` is true.
- The model must refresh after any successful or failed recruit or starter action.

## Recruit Action Result

Structured result returned to the UI when the player attempts to obtain a first villager or settler.

**Fields**

- `success`: Whether the action completed
- `reasonCode`: Stable code for the blocker or outcome
- `playerMessage`: Short explanation shown to the player
- `remediation`: Suggested follow-up step
- `spawnedVillagerName`: Name or label of the added villager when successful

**Validation Rules**

- Failed results must never be silent.
- Successful results must update the visible roster and counts immediately.

## Onboarding UI Summary

The visible summary shown on the settlement management screen.

**Fields**

- `header`: Current settlement or onboarding title
- `statusCards`: Compact counts and alerts
- `primaryAction`: The prominent button or call-to-action
- `supportingText`: Short explanation of the current state
- `secondaryActions`: Additional contextual actions

**Validation Rules**

- The primary action must remain visible at supported window sizes.
- Status cards and text must not overlap or become unreadable at 1280×720.

## Villager Roster Entry

Individual row in the early settlement villager list.

**Fields**

- `id`: Villager identifier
- `name`: Display name
- `role`: Current settlement role
- `activity`: Human-readable current task
- `needsAttention`: Whether the villager requires intervention

**Validation Rules**

- The roster must not show stale activity after a state change.
- Entries must remain distinguishable when there is only one villager.

## Settlement Recovery Hint

The guidance shown when the starter flow is incomplete, blocked, or partial.

**Fields**

- `headline`: Short instruction
- `detail`: Longer explanation
- `targetArea`: Suggested place to act next

**Validation Rules**

- Recovery hints must direct the player toward an actionable next step.
- Hints must be consistent with the current settlement state and recruit result.
