# Contract: Agent State Extensions (Villager Flow)

## Purpose

Keep automation (`GET /state`, village actions) aligned with improved player-visible
settlement status. All changes are **additive** and backwards compatible.

## Existing Workflow (unchanged)

1. Start game (`--skip-menu` or normal load).
2. `GET /health` until `gameState` is `Playing`.
3. `GET /state` for player and settlement context.
4. `POST /action` for `recruit_villager`, `assign_job`, movement, etc.
5. Optional `POST /village/chat` when AI enabled.

## `GET /state` — Additive Fields

When an active settlement exists, `village` object MAY include:

| Field | Type | Description |
|-------|------|-------------|
| `nextAction` | string | Same prioritized recommendation as Town Board Overview |
| `idleWorkers` | int | Count of citizens on idle job |
| `foodRisk` | string | `ok`, `low`, or `critical` |

Each entry in `villagers[]` MAY include:

| Field | Type | Description |
|-------|------|-------------|
| `activity` | string | Human-readable task description |
| `progress` | string | Optional progress snippet |
| `needsAttention` | bool | Idle, low morale, or crisis flag |

Existing fields (`guidanceHint`, `village.name`, `population`, `foodStock`,
`job`, etc.) MUST remain present with unchanged semantics.

## `POST /action?cmd=assign_job`

Response `message` SHOULD include plain-language failure reason when assignment
fails (not only `success: false`).

Parameters unchanged: `villager_id`, `job`, optional `target_x/y/z`.

## `POST /action` recruit

Existing `recruit_villager` command unchanged. When recruit fails, `message`
SHOULD echo the same blocked reason shown in Town Board recruit preview.

## `POST /village/chat`

Unchanged. Villager targeting from Town Board does not alter HTTP contract.

## Validation Expectations

- `guidanceHint` and `village.nextAction` (when present) describe the same
  priority issue.
- `villagers[].activity` matches in-world nameplate text for the same citizen.
- Failed `assign_job` returns actionable `message` for blocked scenarios covered
  in [village-ui-contract.md](village-ui-contract.md).

## Non-Goals

- New HTTP endpoints
- Breaking renames of existing JSON fields
- Exposing internal `reasonCode` enums in HTTP responses (optional future)
