# Autonocraft Agent Guidelines

This document describes how AI agents and automation tools should build, test, and interact with the Autonocraft codebase.

> **Critical rule:** Any agent modifying **player physics, movement, gravity, collision, world generation, chunking, blocks, inventory, tools, skills, animals, combat, crafting, fluids, structures, saves, villagers, villages, UI, or rendering** must run the integration test suite before concluding the task:
>
> ```bash
> dotnet run --project src/Autonocraft -- --test
> ```
>
> Or, for unit tests only:
>
> ```bash
> dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Unit"
> ```
>
> CI also runs live HTTP E2E tests (`e2e.yml`) and quality gates (`quality.yml`) on every PR.
>
> All tests must pass (exit code `0`). Failures print a stack trace and exit with code `1`.

---

## 1. Technology Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10 (`net10.0`) |
| Graphics | MonoGame 3.8 DesktopGL (OpenGL) |
| Content | Procedural atlas (`atlas_layout.json`, `scripts/build_atlas.py`) |
| Agent API | `HttpListener` on port 5001 by default (`--agent-port`; macOS AirPlay uses 5000) |

The game no longer uses Vulkan or GLFW. Tests run headlessly without opening a window (`game.Run()` is never called).

---

## 2. CLI Entry Points

```bash
# Normal windowed play (main menu)
dotnet run --project src/Autonocraft

# Skip menu — immediate world load (useful for agent sessions)
dotnet run --project src/Autonocraft -- --skip-menu

# Custom agent API port (default 5001)
dotnet run --project src/Autonocraft -- --skip-menu --agent-port 5001

# Headless integration tests (no graphics window)
dotnet run --project src/Autonocraft -- --test

# Faster build skipping MGCB content rebuild
dotnet build src/Autonocraft -p:SkipMonoGameContent=true
```

On macOS, `run.sh` and `start.command` set `DYLD_LIBRARY_PATH=/opt/homebrew/lib` before launching.

---

## 2b. Continuous Integration

GitHub Actions validates every push and PR on **Ubuntu, Windows, and macOS**:

| Workflow | Jobs |
|----------|------|
| `.github/workflows/ci.yml` | `build` → `unit-tests` (xUnit unit filter) + `integration-tests` (`dotnet run -- --test`) |
| `.github/workflows/e2e.yml` | Live HTTP API (`test_live_api.py`) + all JSON scenarios via `scripts/ci_e2e.sh` / `scripts/ci_e2e.ps1` |
| `.github/workflows/quality.yml` | `dotnet format --verify-no-changes`, `build_atlas.py --check`, coverlet coverage |
| `.github/workflows/codeql.yml` | C# CodeQL analysis |
| `.github/workflows/release.yml` | Tag-triggered multi-RID `dotnet publish` + GitHub Release assets |

Nightly schedule (06:00 UTC) re-runs CI and E2E on `main`. Local parity:

```bash
dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Unit"
dotnet run --project src/Autonocraft -c Release -- --test
bash scripts/ci_e2e.sh   # macOS/Linux full E2E
```

---

## 3. Integration Test Suite

**Source:** `tests/Autonocraft.Tests/` (split by domain) with fallback runner in `src/Autonocraft/Core/GameIntegrationTests.cs`

Tests instantiate `AutonocraftGame(runTests: true)` without calling `Run()`, so no MonoGame window is created. Graphics device may be `null` for chunk updates in test mode.

### Covered areas (48 tests)

#### Settings & world generation

| Test | What it verifies |
|------|------------------|
| Game Settings Round-Trip | `settings.json` save/load |
| Chunk LOD Bands | Distance-based mesh detail levels |
| Chunk LOD Mesh Counts | Full / surface / shell vertex counts |
| Chunk Streaming Stability | Chunk load/unload around moving player |
| World Generation Basics | Biomes, sea level, caves, ores, rivers |
| Structure Generation | Procedural structures placed in world |
| Biome Tree Species | Willow/Palm trees in correct biomes |

#### Physics & player

| Test | What it verifies |
|------|------------------|
| Gravity and Collision | Ground contact, terminal velocity |
| Jumping | Jump impulse and landing |
| Fall Damage | Damage from high falls |
| Passable Blocks | Movement through non-solid blocks |
| Swim Through Water | Swimming physics |
| Drowning | Oxygen depletion underwater |
| Fall Damage In Water | Reduced fall damage in water |

#### Fluids

| Test | What it verifies |
|------|------------------|
| Fluid Spread | Water propagates to adjacent cells |
| Bucket Place and Pickup | Empty/water bucket placement |
| Fluid Save Round-Trip | Fluid levels persist in saves |
| No Walk On Water | Player cannot stand on water surface |
| Water Fills Excavated Gap | Water fills removed blocks |

#### Inventory, tools & skills

| Test | What it verifies |
|------|------------------|
| Inventory | Hotbar add/remove/count |
| Tool Mining Speed | Tool tier affects break time |
| Tool Durability | Tools lose durability on use |
| Skill Progression | XP and level-ups for mining |

#### Blocks & interaction

| Test | What it verifies |
|------|------------------|
| Mining and Placing | Block break/place via `BlockInteractionSystem` |
| Click Priority | Animal vs block click targeting |

#### Saves

| Test | What it verifies |
|------|------------------|
| World Save Round-Trip | Serialize/deserialize `world.json` v6 (includes villages/villagers) |
| Player Statistics Round-Trip | Per-world stats counters persist in `world.json` |

#### Animals & combat

| Test | What it verifies |
|------|------------------|
| Animal Gravity | Animals fall and land on terrain |
| Animal Wander Collision | Wander AI respects solid blocks |
| Animal Spawn Cap | Population limits around spawn |
| Player Take Damage | Health reduction and bounds |
| Entity Raycast | Animal hit detection |
| Melee Kill Animal | Combat system kills animals |

#### Crafting

| Test | What it verifies |
|------|------------------|
| Sigil Bench Activation | Shift+right-click activates sigil → station |
| Crucible Plank Recipe | Crucible transmutation produces planks |
| New Craft Recipes | Station recipe registry |

#### Village & villagers

| Test | What it verifies |
|------|------------------|
| Inventory Stacking | Shared `Inventory` / `IItemContainer` |
| Block Action Service | Headless break/place helpers |
| Claim World Structure | Claim procedural `PlainsCottage` as outpost village |
| Farm Food Production | Farm plots increase `FoodStock` over time |
| Starter Settlement On New World | Town Heart placed, 2 villagers, seeded storage |
| Village Found And Recruit | Found village, storage rations, recruit peasant |
| Village Save Round-Trip V6 | Buildings, sites, caps, villager jobs persist (save v6) |
| Village Save Round-Trip V7 | Extended villager haul/tool state, output chests, village radius (save v7) |
| Village Guidance Hints | Next-best-action HUD hint selection |
| Village Events Notifier | Recruit/build/tier toast events |
| Village AI Tools | Mock LLM tool `get_village_summary` |

#### Survival & early game

| Test | What it verifies |
|------|------------------|
| Hunger Drain | Hunger decreases over simulated time in survival |
| Eat Food | Consuming cooked meat restores hunger |
| Animal Loot | Killing pig adds raw meat to inventory |
| Village Rations | Withdrawing from food stock reduces `FoodStock` and restores player hunger |
| Night Spawn | Wolf spawns at night, none at noon (headless sim) |
| Death Penalty | Death drops hotbar items; respawn restores partial hunger |
| Hunger Save Round-Trip | Hunger persists in `world.json` |
| Starter Settlement (extended) | Welcome cooked meat in storage, both settlers working, farm plot queued |

### Expected output

```
ALL TESTS PASSED SUCCESSFULLY! (EXIT CODE: 0)
```

---

## 4. Agent HTTP Server

**Source:** `src/Autonocraft/Core/AgentHttpServer.cs`

The server starts when the game enters **Playing** state (after world load completes). It is **not** available from the main menu. Use `--skip-menu` to reach gameplay faster.

Base URL: **http://localhost:5001/** (override with `--agent-port`; avoid 5000 on macOS — AirPlay uses it)

Poll **`GET /health`** before calling other endpoints. Returns `{"ready": true, "gameState": "Playing"}` when the agent API is available (503 while loading).

Hotbar slots use **0–8** everywhere (`selectedSlot`, hotbar `slot`, and `select_slot`).

### `GET /health`

Readiness probe. Returns HTTP 200 when `gameState` is `Playing`, otherwise 503.

### `GET /state`

Returns JSON with player state, skills, hotbar (blocks/tools), nearby animals, targeted station, and unlocked recipes.

```json
{
  "gameState": "Playing",
  "worldSeed": 12345,
  "oxygen": 15.0,
  "position": { "x": 16.5, "y": 65.0, "z": 16.5 },
  "velocity": { "x": 0, "y": 0, "z": 0 },
  "yaw": 0,
  "pitch": 0,
  "creativeMode": false,
  "isGrounded": true,
  "health": 20,
  "maxHealth": 20,
  "hunger": 20,
  "maxHunger": 20,
  "earlyGuideStage": 0,
  "guidanceHint": "Press V — take rations or open Town Board",
  "timeOfDay": 0.3,
  "timeScale": 0.01,
  "timePaused": false,
  "selectedSlot": 0,
  "hotbar": [
    { "slot": 0, "kind": "block", "type": "Dirt", "count": 64 },
    { "slot": 1, "kind": "tool", "toolId": "WoodPickaxe", "name": "Wood Pickaxe", "durability": 59, "maxDurability": 60 },
    { "slot": 2, "kind": "empty" }
  ],
  "skills": {
    "mining": { "level": 1, "xp": 0 },
    "woodcutting": { "level": 1, "xp": 0 },
    "combat": { "level": 1, "xp": 0 }
  },
  "animals": [{ "id": 1, "type": "Sheep", "health": 8, "maxHealth": 8, "x": 20, "y": 64, "z": 18 }],
  "targetBlock": { "x": 10, "y": 64, "z": 12, "type": "Stone", "breakProgress": 0.35, "isMining": true },
  "nearbyStation": "Crucible",
  "unlockedRecipes": ["sigil:bench", "recipe:plank"]
}
```

Hotbar `kind` values: `"empty"`, `"block"`, `"tool"`, `"food"`, `"fluid_container"`. `nearbyStation` is `"Bench"`, `"Forge"`, `"Crucible"`, or `null`.

`village` and `villagers[]` appear when a settlement exists. `playWithAi`, `aiProvider`, and `llmAvailable` reflect main-menu AI settings (`Mock`, `OpenRouter`, `LlamaCpp`, or off).

### `POST /village/chat`

Natural-language village steward / villager dialogue. Requires **Play with AI** enabled in settings. Supports OpenRouter (cloud), llama.cpp local server, or Mock (offline tests).

Body JSON: `{ "message": "...", "target": "mayor" | "<villager_id>" }`

### Village actions (via `POST /action`)

| Command | Parameters | Description |
|---------|------------|-------------|
| `recruit_villager` | — | Recruit peasant at primary village |
| `assign_job` | `villager_id`, `job`, optional `target_x/y/z` | Assign Gather/Build/Haul/Idle |

### `GET /screenshot`

Captures the MonoGame back buffer as **PNG**.

| Query param | Description |
|-------------|-------------|
| `path` (optional) | Server-side save path (default: `screenshot.png` in output directory) |

Response: `image/png` bytes.

### `POST /action?cmd=<command>&...`

Queues thread-safe actions on the game loop. Returns `{"success": true|false, "message": "..."}`.

| Command | Parameters | Description |
|---------|------------|-------------|
| `key_down` | `key` | Press a key (`w`, `a`, `s`, `d`, `space`, `shift`, or `Key` enum name) |
| `key_up` | `key` | Release a key |
| `release_keys` | — | Clear all simulated keys |
| `click` | `button=left\|right` | Simulate mouse click |
| `set_look` | `yaw`, `pitch` | Set camera orientation (pitch clamped ±89°) |
| `look` | `dx`, `dy` | Relative look rotation |
| `teleport` | `x`, `y`, `z` | Teleport player, zero velocity |
| `set_creative` | `creative=true\|false` | Toggle creative / survival mode (alias: `set_flying` with `flying=`) |
| `select_slot` | `slot=0-8` | Select hotbar slot |
| `set_time` | `value=0-1` | Set time of day (0=midnight, 0.5=noon) |
| `set_time_scale` | `value` | Day-cycle speed (0 pauses) |
| `open_crucible` | — | Open station UI for targeted crafting station (fails if none targeted) |
| `dev` | `cmd_line=<text>` | Run a dev-console command (see §5) |
| `shutdown` | — | Exit the game |

---

## 5. Developer Console

Toggle in-game with **F3** or **`` ` ``**. Also callable via HTTP: `POST /action?cmd=dev&cmd_line=<command>`.

**Source:** `src/Autonocraft/Core/DevCommands.cs`

| Command | Description |
|---------|-------------|
| `help` / `?` | List commands |
| `time` | Show time of day, scale, pause state |
| `time set <0-1>` | Set cycle position |
| `time dawn\|noon\|dusk\|midnight` | Preset times |
| `time scale <n>` | Cycle speed (0 = pause) |
| `time pause\|resume` | Pause/resume day cycle |
| `tp <x> <y> <z>` / `teleport` | Teleport |
| `pos` | Show position |
| `creative [on\|off]` | Toggle creative mode (alias: `fly`) |
| `give <block> [n]` | Add blocks to hotbar |
| `give tool <pickaxe\|axe\|shovel\|sword> [wood\|stone\|iron\|gold]` | Add tool |
| `give bucket [water]` | Add empty or water bucket |
| `health [n]` / `heal` | Set health |
| `damage [n]` / `hurt` | Apply damage |
| `speed <n>` | Set move speed |
| `slot <0-8>` | Select hotbar slot |
| `inv` / `hotbar` | List all hotbar slots |
| `seed` | Show world seed |
| `chunks` | Show loaded chunk count and player chunk coords |
| `spawn <type> [n]` | Spawn animals (`Sheep`, `Pig`, `Chicken`) |
| `animals` | Show animal counts |
| `recipes` | List unlocked crafting discoveries |
| `unlock <id>` | Debug-unlock sigil/recipe (e.g. `sigil:bench`, `recipe:plank`) |
| `perf` | Show performance counters |
| `clear` | Clear console output |

---

## 6. Interaction CLI (`tests/interact.py`)

Python helper for the HTTP API. Requires the game to be in **Playing** state.

```bash
# Wait until agent API is ready
python3 tests/interact.py wait

# Query state
python3 tests/interact.py state

# Screenshot (saved as PNG)
python3 tests/interact.py screenshot my_view.png

# Movement
python3 tests/interact.py action key_down key=w
python3 tests/interact.py action key_up key=w

# Mining / placing
python3 tests/interact.py action click button=left
python3 tests/interact.py action click button=right

# Camera and teleport
python3 tests/interact.py action look dx=10 dy=0
python3 tests/interact.py action teleport x=16 y=65 z=16

# Flying and inventory
python3 tests/interact.py action set_creative creative=false
python3 tests/interact.py action select_slot slot=2

# Crafting
python3 tests/interact.py action open_crucible
python3 tests/interact.py action dev cmd_line="unlock recipe:plank"

# Dev console via HTTP
python3 tests/interact.py action dev cmd_line="give Stone 64"

# Exit
python3 tests/interact.py action shutdown
```

### Agent skill: scripted play tests

For multi-step flows, JSON scenarios, and a reusable Python client, see `.cursor/skills/autonocraft-game-test/` (`game_client.py`, `run_scenario.py`, `examples/*.json`).

---

## 7. Codebase Map (where to edit what)

| Concern | Primary files |
|---------|---------------|
| Game loop & state machine | `Core/AutonocraftGame.cs`, `Core/GameSession.cs`, `Core/GameState.cs` |
| Player physics & inventory | `Core/Player.cs`, `Core/PlayerStatistics.cs` |
| Block mine/place | `Core/BlockInteractionSystem.cs` |
| Combat | `Core/CombatSystem.cs` |
| Survival / hunger | `Core/SurvivalConstants.cs`, `Core/FoodConsumption` in `AnimalLoot.cs`, `Items/FoodRegistry.cs` |
| Early-game guide | `Core/EarlyGameGuide.cs` |
| Night threats | `Entities/NightThreatSpawner.cs` |
| Death penalty | `Core/DeathConsequences.cs` |
| HTTP agent API | `Core/AgentHttpServer.cs` |
| Integration tests | `tests/Autonocraft.Tests/`, `Core/GameIntegrationTests.cs` |
| World & chunks | `World/VoxelWorld.cs`, `World/Chunk.cs`, `World/ChunkLod.cs` |
| Terrain generation | `World/WorldGenerator.cs`, `World/BiomeMap.cs`, `World/TerrainShaper.cs`, `World/CaveCarver.cs`, `World/OrePlacer.cs`, `World/Decorator.cs`, `World/TerrainPostProcessor.cs` |
| Structures | `World/Structures/StructurePlacer.cs`, `World/Structures/StructureRegistry.cs` |
| Fluids | `World/FluidSystem.cs`, `World/WaterQuery.cs` |
| Block types | `World/BlockType.cs` |
| Saves | `World/WorldSaveManager.cs`, `World/WorldSaveData.cs`, `Core/SaveSnapshot.cs` |
| Texture atlas | `World/BlockAtlas.cs`, `World/AtlasLayout.cs`, `atlas_layout.json`, `scripts/build_atlas.py` |
| Rendering | `Engine/Renderer.cs`, `Engine/WorldRenderer.cs`, `Engine/HudRenderer.cs`, `Engine/SceneLighting.cs`, `Engine/BlockTerrainEffect.cs` |
| Audio | `Engine/Audio/AudioManager.cs`, `Engine/Audio/ProceduralSfx.cs`, `Engine/Audio/ProceduralAmbient.cs`, `Engine/Audio/ProceduralMusic.cs` |
| Tools & skills | `Items/ToolRegistry.cs`, `Items/MiningCalculator.cs`, `Items/PlayerSkills.cs` |
| Crafting sigils | `Crafting/SigilRegistry.cs`, `Crafting/SigilPattern.cs` |
| Station recipes | `Crafting/CraftRecipeRegistry.cs`, `Crafting/CraftingSystem.cs` |
| Crafting UI | `UI/CrucibleScreen.cs`, `UI/JournalScreen.cs` |
| Animals | `Entities/AnimalManager.cs`, `Entities/Animal.cs`, `Entities/EntityCollision.cs` |
| UI screens | `UI/*.cs` |
| Player stats dashboard | `Core/PlayerStatistics.cs`, `UI/PlayerDashboardScreen.cs`, `UI/SaveSlotScreen.cs` |

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/CODEMAP.md](docs/CODEMAP.md) for deeper navigation.

### Task → file quick reference

| Task | Start here | Test to run |
|------|------------|-------------|
| Add block type | `BlockType.cs`, `build_atlas.py`, `BlockInteractionSystem.cs` | `TestMiningAndPlacing` |
| Add craft recipe | `CraftRecipeRegistry.cs`, `CraftingSystem.cs` | `TestNewCraftRecipes` |
| Change mining speed | `MiningCalculator.cs`, `ToolRegistry.cs` | `TestToolMiningSpeed` |
| Fix swimming | `Player.cs`, `FluidSystem.cs` | `TestSwimThroughWater`, `TestDrowning` |
| Survival / hunger | `SurvivalConstants.cs`, `FoodRegistry.cs`, `Player.cs` | `SurvivalTests`, hunger save round-trip |
| Night threats | `NightThreatSpawner.cs`, `AnimalType.cs` | `RunNightSpawn` |
| Add structure | `StructureRegistry.cs`, `StructurePlacer.cs` | `TestStructureGeneration` |
| Rendering change | `Engine/WorldRenderer.cs`, `Engine/SceneLighting.cs`, `Engine/BlockTerrainEffect.cs` | `--test` + screenshot |

---

## 8. Common Agent Workflows

### Verify a physics change

```bash
dotnet run --project src/Autonocraft -- --test
```

### Visually inspect after a rendering change

```bash
# Terminal 1
dotnet run --project src/Autonocraft -- --skip-menu

# Terminal 2 (after world loads)
python3 tests/interact.py wait
python3 tests/interact.py screenshot render_check.png
python3 tests/interact.py state
```

### Regenerate block textures

```bash
python3 scripts/build_atlas.py
dotnet build src/Autonocraft
```

### Test save/load

Run `--test` (includes `TestWorldSaveRoundTrip`), or manually: create a world in-game, modify blocks, exit, reload the save slot.

### Test crafting via HTTP

```bash
python3 tests/interact.py action dev cmd_line="give OakLog 64"
python3 tests/interact.py action dev cmd_line="unlock recipe:plank"
python3 tests/interact.py action open_crucible
```

---

## 9. Saves Location

Tests override saves to a temp directory. In normal play:

- **macOS:** `~/Library/Application Support/Autonocraft/saves/<slot-id>/world.json`
- **Windows:** `%LOCALAPPDATA%/Autonocraft/saves/<slot-id>/world.json`
- **Linux:** `~/.local/share/Autonocraft/saves/<slot-id>/world.json`

Settings: `.../Autonocraft/settings.json`

| Field | Description |
|-------|-------------|
| `renderDistance` | Chunk radius 2–12 (default 8) |
| `playWithAi` | Enable village steward chat (`C` key, HTTP `/village/chat`) |
| `aiProvider` | `Disabled`, `Mock`, `OpenRouter`, or `LlamaCpp` |
| `openRouterModel` | OpenRouter model id (default `openai/gpt-4o-mini`) |
| `openRouterApiKey` | Optional; falls back to env/file |
| `llamaCppBaseUrl` | Local llama-server URL (default `http://127.0.0.1:8080`) |
| `llamaCppModel` | Optional model name sent to llama-server |
| `masterVolume` | Master audio level 0–1 (default 1) |
| `sfxVolume` | Sound effects level 0–1 (default 1) |
| `ambientVolume` | Ambient loops level 0–1 (default 0.6) |
| `musicVolume` | Background music level 0–1 (default 0.5) |
| `muteAudio` | Mute all audio (default false) |

Configure from the main menu **SETTINGS** button. For local models:

```bash
llama-server -m /path/to/model.gguf --port 8080
```

See `openrouter_key.example.txt` for OpenRouter key file layout.
