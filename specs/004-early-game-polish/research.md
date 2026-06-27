# Research: Early Game Polish

## Decision: Reuse the Existing Starter-Settlement Spine

**Rationale**: The codebase already creates `Founder's Hamlet` during new-world
startup via `GameStateMachine.EnterPlaying()` and
`VillageFoundingService.InitializeStarterSettlement()`. That gives the feature a
natural landmark, nearby villagers, a town heart, and starter storage without a
new tutorial subsystem.

**Alternatives considered**:

- **Build a separate tutorial island or intro scenario** - Rejected because it
  would duplicate world-start systems, complicate saves, and move the game away
  from its existing village-first opening.
- **Use a purely text-based tutorial overlay** - Rejected because the spec
  explicitly wants the opening area to feel more interesting, not just more
  instructive.

## Decision: Drive Progress Through `EarlyGuideStage`

**Rationale**: `PlayerStatistics.EarlyGuideStage` already persists through saves
and `EarlyGameGuide.Update()` already handles stage-based reminders, toasts, and
completion. Extending the existing stage machine keeps the feature aligned with
current save behavior and agent-visible guidance fields.

**Alternatives considered**:

- **Add a new tutorial-progress save field** - Rejected because it would create
  redundant state with `EarlyGuideStage` and increase migration risk.
- **Infer progress only from inventory/state each frame** - Rejected because it
  would make repeat behavior less stable across saves and restarts.

## Decision: Make the Start Area Interesting Through Local, Curated Content

**Rationale**: The feature’s “more interesting start” goal can be met by
curating the immediate spawn region around the starter settlement: clearer
landmarking, nearby resources, and an obvious first destination. That keeps the
change bounded to the spawn region and avoids a broad world-generation rewrite.

**Alternatives considered**:

- **Rework the entire world generator** - Rejected as out of scope and high
  risk for a feature focused on first impressions.
- **Spawn only a text prompt with no world change** - Rejected because it would
  improve guidance but not the actual discovery experience.

## Decision: Keep the Opening Guidance Non-Blocking and Dismissible

**Rationale**: The current game already uses toasts and HUD hints for early
guidance. A polished start should communicate a first goal without preventing
movement, building, or exploration once the player begins interacting.

**Alternatives considered**:

- **Force a mandatory tutorial sequence** - Rejected because it conflicts with
  the spec’s desire for a polished but not intrusive start.

## Decision: No New External Contracts or Save Format

**Rationale**: The feature is entirely internal to gameplay startup and existing
UI/HUD flows. Agent `/state` fields already expose `earlyGuideStage` and
`guidanceHint`, and those remain sufficient for verification.

**Alternatives considered**:

- **Add new `/state` fields for opening objectives** - Rejected; unnecessary for
  this scope and would create API churn without a clear user benefit.
- **Introduce a new save version** - Rejected because the existing player
  statistics field already stores the necessary progression.

## Decision: Verify With Integration Tests Plus Visual Walkthroughs

**Rationale**: Because the feature spans gameplay startup, starter settlement
placement, and presentation polish, the headless integration suite is required.
Manual walkthroughs and screenshots are still needed for the “polished” part of
the spec, but the deterministic evidence comes from integration coverage.

**Alternatives considered**:

- **Manual review only** - Rejected; too weak for a gameplay/system change.
- **Unit tests only** - Rejected; not enough to cover startup flow and world
  state interactions.
