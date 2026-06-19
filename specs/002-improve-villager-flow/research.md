# Research: Improved Villager Flow

## Decision: Extend Town Board Panels Rather Than Replace Them

**Rationale**: `VillageScreen` already uses a panel architecture (`OverviewPanel`,
`BuildPanel`, `PeoplePanel`, `GoalsPanel`) with `VillageViewModel` supplying
read-model data. Players already learn `V` for Town Board. Improving Overview and
People first delivers P1/P2 value without retraining controls.

**Alternatives considered**: New full-screen settlement map; radial job wheel;
separate villager management app. These increase scope and fracture the mental
model established by `EarlyGameGuide` and HUD hints.

## Decision: Centralize Guidance in Village Domain Layer

**Rationale**: `VillageGuidance`, `EarlyGameGuide`, HUD `guidanceHint`, and
`VillageViewModel.NextAction` currently overlap with slightly different strings.
A single prioritized advisor in `Autonocraft.Village` (extending or wrapping
`VillageGuidance`) ensures FR-010 synchronization and testable priority rules
(food crisis > idle workers > construction backlog > recruit opportunity).

**Alternatives considered**: Duplicate hint strings in each UI surface. Rejected
because contradictions are a stated edge case and hard to regression-test.

## Decision: Return Structured Assignment/Recruit Results from Domain

**Rationale**: `JobDispatcher.TryAssignJob` and `VillageManager.TryRecruit` today
return `bool` with toast side effects scattered in `VillageManager`. For FR-005
and SC-003, introduce `JobAssignmentResult` / `RecruitResult` records with
`Success`, `ReasonCode`, and `PlayerMessage` so Town Board panels show inline
errors without guessing from silent `false`.

**Alternatives considered**: UI-layer heuristics (“if Mine and no quarry, show
message”). Rejected because it duplicates dispatcher preconditions and drifts
from simulation truth.

## Decision: Enrich VillagerActivityText With World Context

**Rationale**: `VillagerActivityText` already powers `VillageViewModel` rows and
`VillagerNameplateRenderer` but uses generic labels (“Chopping wood”, “Building”).
FR-006 requires coordinates, building names, and construction progress. Extend
`Describe` / `DescribeProgress` to accept optional `Village` + `VoxelWorld`
context for site names, marked resource positions, and haul targets — without
moving rendering code into the village project.

**Alternatives considered**: UI-only string formatting from raw villager fields.
Rejected because nameplates and agent state should share the same copy.

## Decision: Surface Status on Overview Without Mandatory Tab Switching

**Rationale**: Spec P1 requires population, food, idle count, active work, and
next action visible on open. `OverviewPanel` already shows stat cards and
`ViewModel.NextAction` above the fold; extend with idle-worker badge, food-risk
banner, active-work summary line, and pinned “do this next” call-to-action that
can deep-link to People or Build tab selection.

**Alternatives considered**: Merge all tabs into one scroll. Rejected as too dense
for 1280×720; tab model stays, but Overview becomes the dashboard.

## Decision: In-World Affordances Complement Town Board

**Rationale**: FR-009 asks for contextual interaction near villagers and Town
Heart. `VillagerNameplateRenderer` already draws activity above entities. Add HUD
context prompt when crosshair targets a villager (e.g., “E — Manage” or reuse
existing click flow) and open People tab with villager pre-selected. Keeps `V`
as primary entry.

**Alternatives considered**: Require Town Board for all management. Insufficient
for spec acceptance scenario P2-4.

## Decision: Agent API Additive Fields Only

**Rationale**: Automation uses `GET /state` with `guidanceHint`, `village`, and
`villagers[]`. Add optional `nextAction`, per-villager `activity` / `progress`,
and `blockedReason` on failed `assign_job` responses. Backwards compatible per
constitution III.

**Alternatives considered**: New `/village/dashboard` endpoint. Deferred; not
required for parity with improved player UI.

## Decision: Preserve Save Format and Simulation Rules

**Rationale**: Spec assumptions state UX-only scope. Recruitment costs, job
types, housing cap, and haul/build pipelines remain unchanged. Validation relies
on existing village save round-trip tests plus new UI/guidance tests.

**Alternatives considered**: New villager needs or morale systems. Out of scope
for this feature.

## Decision: Test Layout at 1280×720 and Guidance Priority

**Rationale**: `RunVillageScreenInputLayout` already exists. Extend with assertions
for new Overview elements, People detail activity lines, and recruit preview text.
Add `RunSettlementGuidancePriority` and `RunJobAssignmentBlockedReasons` for SC-003.

**Alternatives considered**: Manual playtest only. Insufficient for CI and
constitution II.
