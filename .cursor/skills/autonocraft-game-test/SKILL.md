---
name: autonocraft-game-test
description: >-
  Navigate and test the Autonocraft voxel game via HTTP scripts: launch the game,
  wait for readiness, take screenshots, move the player, mine/place blocks, query
  state, run dev commands, execute JSON scenarios, and run the live API test suite.
  Use when testing gameplay visually, verifying rendering or UI changes, automating
  movement or interaction, capturing screenshots, or scripting end-to-end play sessions.
---

# Autonocraft Game Testing

Script-driven testing through the **Agent HTTP API**. Default port is **5001** (macOS AirPlay occupies 5000).

## Quick start

```bash
# Terminal 1 — launch game
./.cursor/skills/autonocraft-game-test/scripts/start_game.sh

# Terminal 2 — run everything
./.cursor/skills/autonocraft-game-test/scripts/run_all_tests.sh
```

`run_all_tests.sh` builds, starts the game, runs **22 live API tests**, then **5 JSON scenarios**.

## Toolkit

| Script | Purpose |
|--------|---------|
| `scripts/game_client.py` | Full Python client + CLI (`wait`, `state`, `screenshot`, `dev`, `move`, `mine`, …) |
| `scripts/run_scenario.py` | Declarative JSON scenario runner (`--all` for every example) |
| `scripts/test_live_api.py` | 22 automated live integration tests |
| `scripts/run_all_tests.sh` | One-command build + test orchestration |
| `scripts/start_game.sh` | Launch with `--skip-menu --agent-port 5001` |
| `tests/interact.py` | Lightweight CLI (same API, repo-standard) |

## Python client highlights

```python
from game_client import GameClient, GameSession, Vec3

with GameSession(GameClient()) as g:
    g.wait_ready()
    g.fly_to(Vec3(16, 72, 16))
    g.look_at_ground(0)           # pitch -60 (look down)
    g.select_tool("pickaxe")      # auto-find hotbar slot
    g.give_block("Stone", 64)
    g.move(forward=2.0)
    g.click("left")               # instant mine via agent click
    g.screenshot("out.png")       # validates PNG signature + size
    g.assert_state(has_village=True, health_min=1)
    g.village_chat("Status report?")
    g.reset_input()
```

**Helpers:** `snapshot()`, `wait_for()`, `wait_position_change()`, `mine_block()`, `place_block()`, `find_hotbar_slot()`, `recruit_villager()`, `assign_job()`, `open_crucible()`, `set_time()`, `set_time_scale()`.

## Scenario JSON steps

Each step is one key. Supported keys:

| Step | Example |
|------|---------|
| `wait_ready` | `{"wait_ready": 120}` |
| `sleep` | `{"sleep": 0.5}` |
| `screenshot` | `{"screenshot": "out.png"}` |
| `state` | `{"state": "dump.json"}` |
| `dev` | `{"dev": "give Stone 64"}` |
| `give_block` | `{"give_block": {"type": "Dirt", "count": 48}}` |
| `give_tool` | `{"give_tool": {"tool": "pickaxe", "tier": "stone"}}` |
| `select_slot` | `{"select_slot": 0}` |
| `select_tool` | `{"select_tool": "pickaxe"}` |
| `teleport` | `{"teleport": {"x": 16, "y": 70, "z": 16}}` |
| `look` | `{"look": {"yaw": 0, "pitch": -60}}` or `{"look": {"dx": 30, "dy": 0}}` |
| `set_flying` | `{"set_flying": true}` |
| `move` | `{"move": {"forward": 2.0}}` or `{"move": {"keys": ["w","a"], "seconds": 1.5}}` |
| `click` | `{"click": "left"}` or `{"click": {"button": "left", "times": 3}}` |
| `mine` | `{"mine": {"clicks": 20, "interval": 0.15}}` |
| `place` | `{"place": 1}` |
| `spawn_animal` | `{"spawn_animal": {"type": "Sheep", "count": 2}}` |
| `set_time` | `{"set_time": 0.5}` |
| `set_time_scale` | `{"set_time_scale": 0.01}` |
| `assert` | `{"assert": {"has_village": true, "hotbar_block": "Stone"}}` |
| `wait_for` | `{"wait_for": {"position_change": 0.3, "timeout": 8}}` |
| `repeat` | `{"repeat": {"count": 3, "steps": [{"click": "left"}]}}` |
| `village_chat` | `{"village_chat": "How is food stock?"}` |
| `recruit_villager` | `{"recruit_villager": true}` |
| `assign_job` | `{"assign_job": {"villager_id": 1, "job": "Gather"}}` |
| `open_crucible` | `{"open_crucible": true}` |
| `action` | `{"action": {"cmd": "dev", "cmd_line": "fly on"}}` |
| `release_keys` | `{"release_keys": true}` |
| `shutdown` | `{"shutdown": true}` |

Examples in `examples/*.json`. Run one or all:

```bash
python3 .cursor/skills/autonocraft-game-test/scripts/run_scenario.py examples/explore.json
python3 .cursor/skills/autonocraft-game-test/scripts/run_scenario.py --all
```

## Live API test suite

```bash
python3 .cursor/skills/autonocraft-game-test/scripts/test_live_api.py
python3 .cursor/skills/autonocraft-game-test/scripts/test_live_api.py --skip-ai
```

Covers: health, state schema, screenshots, teleport, look, movement, dev give, mining click, village, animals, time, invalid key rejection. Results written to `test_output/live_api/test_results.json`.

## Workflow checklist

```
- [ ] Start game: ./run.sh --skip-menu --agent-port 5001
- [ ] Wait: python3 tests/interact.py wait
- [ ] Baseline screenshot + state
- [ ] Run actions / scenario
- [ ] Assert state fields or compare screenshots
- [ ] release_keys before ending
```

## When to use headless tests instead

Use `dotnet run --project src/Autonocraft -- --test` for physics, saves, crafting logic. Use this skill for **visual verification** and **HTTP-driven play**.

## Additional resources

- [reference.md](reference.md) — full HTTP API
- [examples.md](examples.md) — patterns and custom scripts
- `AGENTS.md` §4–6 — repo agent guide
