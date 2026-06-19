# Data Model: Improved Villager Flow

## Settlement Dashboard (read model)

Aggregated presentation state for the Town Board Overview tab. Not persisted;
built each refresh from `Village` + `VillagerManager`.

**Fields**

- `statusLine`: One-line settlement health summary (tier, population, food)
- `nextAction`: Prioritized plain-language recommendation
- `nextActionKind`: Enum-like tag for deep-link (`AssignJobs`, `AddressFood`,
  `QueueHousing`, `Recruit`, `SummonSettlers`, `None`)
- `idleWorkerCount`: Live citizens on `JobType.Idle`
- `foodRiskLevel`: `Ok`, `Low`, `Critical` derived from food stock vs population
- `pendingBuildCount`: Incomplete building sites
- `activeWorkSummary`: Short list of in-progress jobs (build sites, farm, haul)
- `recruitPreview`: Requirements and eligibility text before recruit commit

**Relationships**

- Built from one `Settlement` and its live `Citizen` rows.
- Feeds `OverviewPanel` and HUD `guidanceHint` when settlement is active.

**Validation Rules**

- `nextAction` must match the highest-priority issue from guidance rules (food
  critical beats idle workers beats construction backlog).
- Counts use `VillageSettlementHealth` live citizen enumeration, not stale
  `village.Population` alone.

## Citizen Row (read model)

Per-villager summary for People list and agent `/state`.

**Fields**

- `id`, `name`, `role`
- `activity`: Human-readable current task from `VillagerActivityText`
- `progress`: Optional progress snippet (percent, site name, coordinates)
- `happiness`, `needsAttention`
- `wellBeingFlags`: Optional `Starving`, `LowMorale`, `Idle`

**Relationships**

- Maps 1:1 to a live `Citizen` in the settlement registry.

**Validation Rules**

- `activity` must update when `CurrentJob`, `AiPhase`, `JobTarget`, or assigned
  site changes.
- `needsAttention` true when idle, low happiness, or settlement food crisis
  affects villager.

## Settlement Guidance

Prioritized recommendation engine output.

**Fields**

- `headline`: Short HUD-safe hint (fits one line)
- `detail`: Longer Town Board explanation
- `priority`: Ordered rank for crisis stacking
- `suggestedTab`: Optional UI navigation target (`Overview`, `People`, `Build`)

**Relationships**

- Consumed by `EarlyGameGuide`, `VillageViewModel`, and `AgentStateSerializer`.

**Validation Rules**

- Single headline per active settlement at a time.
- Founding mode (no `Settlement` yet) uses founding-specific copy from
  `FoundingPanel` with consistent vocabulary (“settlers”, “Town Heart”).

## Job Assignment

Link between citizen and work type. Existing simulation entity; UX layer adds
result metadata.

**Fields**

- `jobType`: Idle, Lumber, Mine, Farm, Build, Haul, etc.
- `targetPosition`: Optional world target
- `buildingSiteId`, `buildingId`: Optional assignment anchors
- `result` (transient): `JobAssignmentResult` on player-initiated assign

**State transitions**

- Player selects job → dispatcher validates → assign or return blocked result →
  UI toast + inline message → villager `AiPhase` updates on success.

**Validation Rules**

- Blocked results must include `reasonCode` and `playerMessage` (FR-005).
- Silent `false` without user feedback is not acceptable for player-initiated
  assigns from Town Board.

## Job Assignment Result (transient)

**Fields**

- `success`: Whether assignment applied
- `reasonCode`: Machine-friendly code (`NoQuarry`, `NoFarmPlot`, `NoLumberTarget`,
  `WrongVillage`, `NoPendingSite`, etc.)
- `playerMessage`: Plain-language explanation
- `remediation`: Suggested next step (“Queue farm plot on Build tab”)

**Validation Rules**

- `playerMessage` non-empty when `success` is false.
- Reason codes stable for tests and optional agent error payloads.

## Recruitment Offer

Conditions to add a citizen. Existing logic; exposed proactively in UI.

**Fields**

- `eligible`: Whether recruit can proceed now
- `housingAvailable`: `population < populationCap`
- `materialCost`: Oak plank cost (existing `RecruitFoodCost`)
- `materialsAvailable`: Storage check result
- `blockedReason`, `remediation`: When not eligible

**Validation Rules**

- Preview shown before recruit button commit (FR-007).
- Creative mode bypass documented in UI copy when applicable.

## Settlement Notification

Short-lived player message for milestones and crises.

**Fields**

- `message`: Toast text
- `category`: `recruit`, `build`, `goal`, `tier`, `food`, `assign`
- `dedupeKey`: Optional key to prevent spam (existing `VillageEvents` throttles)

**Relationships**

- Emitted by `VillageEvents`; must not contradict active `Settlement Guidance`
  headline.

**Validation Rules**

- Food-critical toasts throttle per existing `CheckFoodCritical` timing.
- Assign-failure messages prefer inline Town Board detail over duplicate toasts
  when UI is open.

## In-World Villager Target (transient)

Crosshair/proximity context for FR-009.

**Fields**

- `villagerId`, `distance`
- `promptLabel`: e.g., “Manage citizen”
- `canOpenDetail`: Whether player is in range

**Validation Rules**

- Opening detail selects villager on People tab and preserves Town Board state.
