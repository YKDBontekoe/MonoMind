# Contract: Agent Inspection Workflow

## Purpose

Preserve the automation workflow used to inspect generated buildings without
requiring new public endpoints for this feature.

## Existing Workflow

1. Start the game in structure gallery mode.
2. Wait for the agent HTTP server to report ready.
3. Request the structure catalog.
4. Teleport to a structure anchor.
5. Capture screenshots and state for visual review.
6. Repeat for each updated building.
7. Shut down the game.

## Required Behavior

- `GET /structures` continues to return every registered structure with stable
  IDs, anchors, tier, index, and footprint metadata.
- `POST /action?cmd=teleport` continues to allow direct inspection near each
  structure anchor.
- `GET /screenshot` and `POST /action?cmd=screenshot` continue to capture
  rendered building views.
- `GET /state` continues to report gameplay state after teleporting into the
  gallery.
- Hotbar slots, controls, readiness, and action semantics remain unchanged.

## Validation Expectations

- Automation can visit every updated structure from the `/structures` catalog.
- Screenshots show nonblank rendered buildings from exterior and interior
  inspection points.
- Failed readiness or screenshot calls use existing failure semantics; no new
  error contract is introduced by this feature.
