# Autonocraft Agent HTTP API Reference

Base URL: `http://localhost:5001/` by default (`--agent-port` on game launch; avoid 5000 on macOS).

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Readiness probe. 200 + `ready: true` when playable; 503 while loading |
| GET | `/state` | Full player/world snapshot (JSON) |
| GET | `/screenshot?path=<file>` | PNG capture; optional server-side path |
| POST | `/action?cmd=<name>&...` | Queue game-loop action |
| POST | `/village/chat` | JSON body: `{"message": "...", "target": "mayor" \| "<villager_id>"}` |
| POST | `/village/chat/confirm` | Confirm pending village action from steward |

## GET /state fields (common)

| Field | Type | Notes |
|-------|------|-------|
| `gameState` | string | Must be `Playing` |
| `position` | `{x,y,z}` | Player world coords |
| `velocity` | `{x,y,z}` | Current velocity |
| `yaw`, `pitch` | float | Camera orientation |
| `flyingMode` | bool | Physics bypass when true |
| `isGrounded` | bool | On solid ground |
| `health`, `maxHealth` | int | Combat health |
| `oxygen` | float | Underwater breath |
| `selectedSlot` | int | Hotbar 0–8 |
| `hotbar` | array | `kind`: `empty`, `block`, `tool` |
| `skills` | object | `mining`, `woodcutting`, `combat` levels/xp |
| `targetBlock` | object? | `x,y,z`, `type`, `breakProgress`, `isMining` |
| `nearbyStation` | string? | `Bench`, `Forge`, `Crucible`, or null |
| `unlockedRecipes` | string[] | Crafting discoveries |
| `animals` | array | Nearby entities |
| `village`, `villagers` | object/array | When settlement exists |
| `playWithAi`, `aiProvider`, `llmAvailable` | — | AI chat availability |

## POST /action commands

| cmd | Parameters | Effect |
|-----|------------|--------|
| `key_down` | `key` | Press key (`w`,`a`,`s`,`d`,`space`,`shift`, or `Key` enum name) |
| `key_up` | `key` | Release key |
| `release_keys` | — | Clear all simulated keys |
| `click` | `button=left\|right` | Mouse click |
| `set_look` | `yaw`, `pitch` | Absolute camera (pitch clamped ±89°) |
| `look` | `dx`, `dy` | Relative rotation |
| `teleport` | `x`, `y`, `z` | Teleport; zero velocity |
| `set_flying` | `flying=true\|false` | Toggle flying |
| `select_slot` | `slot=0-8` | Hotbar selection |
| `set_time` | `value=0-1` | Time of day |
| `set_time_scale` | `value` | Day cycle speed (0 = pause) |
| `open_crucible` | — | Open UI for targeted station |
| `dev` | `cmd_line=<text>` | Dev console command |
| `recruit_villager` | — | Recruit at primary village |
| `assign_job` | `villager_id`, `job`, optional `target_x/y/z` | Job: Gather, Build, Haul, Idle |
| `shutdown` | — | Exit game |

## Dev console commands (via `dev`)

| Command | Example |
|---------|---------|
| Teleport | `tp 16 65 16` |
| Fly | `fly on` / `fly off` |
| Give blocks | `give Stone 64` |
| Give tool | `give tool pickaxe stone` |
| Give bucket | `give bucket water` |
| Health | `heal` / `health 20` |
| Spawn animals | `spawn Sheep 3` |
| Unlock recipe | `unlock recipe:plank` |
| Time | `time noon`, `time scale 0` |
| Hotbar | `slot 2`, `inv` |

Full list: `AGENTS.md` §5 or in-game `help`.

## CLI wrappers

```bash
# Repo standard
python3 tests/interact.py [--host HOST] [--port PORT] <command> [args]

# Skill client (same API, richer Python API)
python3 .cursor/skills/autonocraft-game-test/scripts/game_client.py <command> [args]
```
