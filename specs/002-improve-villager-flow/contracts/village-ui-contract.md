# Contract: Town Board (Settlement Management UI)

## Purpose

Define player-facing behavior for the improved settlement management experience
opened with **V** (Town Board) and founding flow, without prescribing layout
pixels or implementation classes.

## Entry Points

| Trigger | Behavior |
|---------|----------|
| `V` key near active settlement | Opens Town Board on **Overview** tab with dashboard visible |
| `V` key during founding | Opens founding panel with same vocabulary as established settlement |
| In-world villager affordance | Opens Town Board on **People** tab with target citizen selected |
| `R` key (when recruit eligible) | Recruits if requirements met; otherwise shows blocked reason |

## Overview Tab (Dashboard)

When a settlement has live citizens, the player MUST see without switching tabs:

- Population vs housing cap
- Food stock with risk indicator when low or critical
- Idle worker count (if any > 0)
- Count of active construction or queued work
- One prioritized **Next action** line with plain language
- Optional call-to-action control that navigates to the relevant tab

When no citizens are present, a recovery banner MUST show summon/claim steps
consistent with `VillageGuidance.GetQuickStartSteps`.

## People Tab

| Element | Requirement |
|---------|-------------|
| Citizen list | Name, role, job, activity summary, attention flag |
| Detail pane | Activity + progress in plain language; morale; skills; trait |
| Job buttons | Assign on click with immediate success/failure feedback |
| Failure feedback | Inline message with reason and remediation (same screen) |
| Talk button | Opens village chat for selected citizen when AI enabled; hidden or disabled with explanation when AI disabled |

## Build Tab

Unchanged catalog behavior; blocked queue actions SHOULD surface material or
placement errors in footer/status area when player attempts invalid queue.

## Footer Actions

| Action | Preconditions shown before commit |
|--------|-------------------------------------|
| Recruit | Housing space, plank cost, storage availability |
| Summon settlers | Player near Town Heart or distance hint |
| Claim structure | Nearby claimable structure indicator |

## Guidance Synchronization

The **Next action** on Overview, HUD `guidanceHint` (when settlement active),
and early-game guide strings MUST derive from the same prioritized guidance
rules. They may differ in length but MUST NOT contradict.

## Layout

- Minimum supported resolution: **1280×720**
- Primary controls and citizen list rows MUST NOT clip at this resolution
- Tab bar labels remain: Overview, Build, People, Goals (Goals hidden until
  early guide stage ≥ 3, existing behavior)

## Non-Goals (this contract)

- Changing job simulation rules or recruit economy
- Replacing `C` steward chat screen layout
- New save file fields
