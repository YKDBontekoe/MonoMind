# Autonocraft Architecture

Technical reference for the Autonocraft voxel engine and game systems. For agent workflows see [AGENTS.md](../AGENTS.md). For file-level navigation see [CODEMAP.md](CODEMAP.md).

## Overview

Multi-assembly layout. Gameplay libraries live under `src/Autonocraft.*`; the executable (`src/Autonocraft/`) holds `Program.cs`, `AutonocraftGame`, UI screens, and content assets. Rendering lives in **`Autonocraft.Engine`** (`src/Autonocraft.Engine/`).

```
Autonocraft.sln
├── Autonocraft.Domain          — shared types, save DTOs, narrow interfaces, render read-models
├── Autonocraft.Diagnostics     — PerfCounters, WorldDebugTrace
├── Autonocraft.Items           — inventory, tools, food, ICraftingPlayer, HUD render views
├── Autonocraft.Crafting        — recipes, grids, station crafting
├── Autonocraft.Ai              — LLM clients (OpenRouter, llama.cpp, Mock)
├── Autonocraft.World           — chunks, generation, fluids, atlas, VoxelWorld
├── Autonocraft.Entities        — animals, collision, pathfinding
├── Autonocraft.Village         — villages, villagers, jobs, economy
├── Autonocraft.Engine          — MonoGame rendering, audio, GameRenderContext
├── Autonocraft.Core            — game systems, agent HTTP, saves
├── Autonocraft (exe)           — AutonocraftGame shell, UI, Program.cs
└── Autonocraft.Tests
```

```
Program.cs → GameServiceProvider (M.E.DI) → AutonocraftGame (partial, exe)
    ├── GameStateMachine      — menu / loading / playing transitions
    ├── GameInputRouter       — keyboard, mouse, agent keys, sprint
    ├── GameOverlayRouter     — pause, death, inventory, village, crucible, journal
    ├── GamePersistenceCoordinator — save / load / autosave
    └── AutonocraftGame.Draw  — render pass
    └── GameSession (Core)    — player, world, animals, gameplay systems
    └── VoxelWorld (World)    — chunk streaming, terrain, fluids
    └── AgentHttpServer (Core) — localhost agent API
        └── Agent/Handlers/* + Agent/Serialization/AgentStateSerializer
```

Namespaces unchanged: `Autonocraft.Core`, `Autonocraft.Engine`, `Autonocraft.World`, etc.

### Dependency rules (enforced by unit test)

```
Domain ← Diagnostics, Items, Ai
Domain, Items ← Crafting
Domain, World ← Entities
Domain, Diagnostics, Items, World, Entities ← Village
Domain, Diagnostics, Items, World, Entities, Village ← Engine
Domain, Diagnostics, Items, World, Entities, Village, Crafting, Ai, Engine ← Core
Core, Engine ← Exe (Autonocraft)
```

`World` uses `BlockRaycast` and `BlockPlacement` instead of `Core` types. `GameSettings`, `GameState`, and `SurvivalConstants` live in `Autonocraft.Domain.Core`. Engine renderers consume **read-model interfaces** (`IBlockInteractionOverlay`, `IPlayerHudView`, `IPlayerMotionView`, `ICraftingHudHint`, etc.) implemented by Core types — no `Autonocraft.Core` reference from Engine.

See `docs/CORE_CROSS_REFERENCES.md` for the pre-split Core cross-reference inventory.

---

## Game State Machine

| State | Description |
|-------|-------------|
| `MainMenu` | Save slot selection |
| `NewWorldSetup` | Seed, world type — **FOUND SETTLEMENT** flow |
| `WorldLoading` | Async chunk terrain + mesh generation with progress UI |
| `Playing` | Active gameplay; starter hamlet spawned on new worlds; agent HTTP server starts here |

**New world start:** `VillageManager.InitializeStarterSettlement()` places a completed Town Heart near spawn, seeds storage, spawns two villagers (lumberjack + peasant), and opens the village UI once with onboarding toasts. Player hotbar is tuned for settlement play (axe + lighter block stacks).

**Discoverable outposts:** World-gen structures (`PlainsCottage`, `VillageOutpost`, `ForestShelter`) can be claimed via **V** UI, Shift+right-click on structure blocks, or auto-claim when opening **V** near an unclaimed site.

`--skip-menu` sets initial state to `WorldLoading`, bypassing menus.

---

## World and Chunks

### Dimensions

| Constant | Value |
|----------|-------|
| Chunk width / depth | 16 × 16 blocks |
| Chunk height | 192 blocks (Y: 0–191) |
| Sea level | 62 |
| Default seed | 1337 |

### VoxelWorld (`World/VoxelWorld.cs`)

- Stores chunks in a `(chunkX, chunkZ)` dictionary.
- Tracks player modifications separately from generated terrain.
- Uses `ReaderWriterLockSlim` for thread-safe access.
- **Async pipeline:** terrain generation and mesh building run on background tasks with per-frame budgets (`DefaultTerrainChunksPerFrame = 4`, `DefaultMeshChunksPerFrame = 4`).
- Fires `ChunksLoaded` / `ChunksUnloaded` events for the loading screen and renderer.
- Owns `FluidSystem` for water flow simulation.

### Chunk LOD (`World/ChunkLod.cs`, `World/Chunk.cs`)

Three mesh detail levels based on distance from the player:

| Detail | Use |
|--------|-----|
| `Full` | Near chunks — all exposed faces |
| `Surface` | Mid range — top surface emphasis |
| `Shell` | Far chunks — outer shell only |

Each level has its own vertex/index buffers on the GPU.

---

## World Generation Pipeline

`WorldGenerator.GenerateChunkTerrain()` runs per chunk in this order:

1. **BiomeMap** — temperature + moisture noise → biome type (Ocean, Beach, Plains, Forest, Desert, Mountains, SnowyPeaks, Swamp)
2. **TerrainShaper** — biome profile drives base height, surface/subsurface/filler blocks
3. **TerrainPostProcessor** — river carving across chunk boundaries (when `EnableRivers` is true)
4. **CaveCarver** — 3D Perlin noise tunnels
5. **OrePlacer** — coal, iron, gold at depth bands
6. **Decorator** — orchestrates grove-based tree placement (`TreePlacer`), biome-weighted ground flora (`FloraPlacer`), cave glowshroom clusters, mountain ropes, boulders, and animal features
7. **StructurePlacer** — procedural structures (ForestShelter, PlainsCottage, etc.) from `StructureRegistry`

### Vegetation (`World/Generation/`)

- **TreePlacer** — noise-cluster grove sampling (2–5 trees per grove, 3–7 block radius); species from `TreeSpeciesRegistry` (8 log/leaf pairs: Oak, Birch, Pine, Willow, Palm, Cherry, Mahogany, Maple)
- **TreeShapeGenerator** — hybrid template scaffold + `BranchPlacer` L-system branch pass; per-species `MaxBlocks` cap (64 default, 96 for Oak/Mahogany); stumps (5%) and rare fallen logs in Forest
- **FloraPlacer** — single weighted roll per column from `FloraPlacementRules`; `FloraDensity` + `FloraDensityScale` knobs; understory pass under leaf canopy
- **Flora blocks** — TallGrass, flowers, Fern, mushrooms, Shrub, Heather, Juniper (billboards), Moss (cube on stone), kelp/seagrass clusters in water

`BiomeProfile` exposes `TreeDensity`, `FloraDensity`, `AllowUnderstory`, `AllowFlowers`, and `AllowCactus` per biome; `BiomeMap.BlendProfiles` interpolates density at borders (e.g. desert palms via `TreeDensity = 0.03`).

### World types (`World/WorldType.cs`)

`Default`, `Mountains`, `Islands`, `Flat` — each maps to `WorldGenParams` presets (amplitude, cave density, river toggle, etc.).

### Block types (`World/BlockType.cs`)

43 block types (Air through Ice) including terrain, ores, vegetation, water, tree species (Oak, Birch, Pine, Willow, Palm), crafting stations (`StationBench`, `StationForge`, `StationCrucible`), and crafted blocks (`OakPlank`, `Glass`, `IronBlock`, `Cobblestone`, etc.).

---

## Fluid System

`World/FluidSystem.cs` simulates water flow between blocks. `WaterQuery.cs` provides helpers for swimming, drowning, underwater camera tint, and splash detection. Player physics in `Player.cs` handles swim-up/down, surface buoyancy, and oxygen depletion.

---

## Structures

`World/Structures/` places multi-block templates during world generation. `StructureRegistry` defines 12 structure types with tier and biome placement rules. `StructurePlacer` integrates with `WorldGenerator` after decoration.

---

## Rendering

### Stack

- **MonoGame DesktopGL** with stock `BasicEffect` for sky, terrain, HUD, and entity billboards.
- **BlockTerrainEffect** — thin wrapper around `BasicEffect` with fog, dual directional lights (sun/moon), and vertex-color terrain shading.
- **SceneLighting** — global time-of-day sun/moon direction, ambient, fog, and sky colors (no per-voxel block/sky light propagation).
- **SkyDomeRenderer** — cached hemisphere mesh with CPU sky colors (`SkyColor.cs`); colors refresh only when time-of-day or lighting inputs change.
- **WorldRenderer** — sky, terrain LOD (opaque / water / alpha-cutout passes), flora, animals, block overlay.
- **HudRenderer** — crosshair, hotbar, health, skills, compass, damage flash, held-item animation.
- **Renderer** — thin orchestrator calling `Draw(GameRenderContext)`.

### Time of day

- `DayNightCycle` in `Autonocraft.Domain` centralizes `TimePhase` boundaries for crafting, villages, and HUD labels.
- Chunk meshes bake ambient occlusion into vertex colors at build time; caves use the same global directional light as the surface.

### Texture Atlas

- Layout defined in `atlas_layout.json` (8×8 grid, 128px tiles).
- Packed PNG at `src/Autonocraft/atlas.png`, built by `Autonocraft.AtlasBuild` or runtime `ProceduralAtlasBuilder`.
- `BlockAtlas` maps `BlockType` + face direction → UV coordinates.
- `BlockTextureBlend` provides per-vertex blend weights for smooth biome transitions on terrain faces.
- `MeshBuildContext` carries blend data during chunk mesh construction.

---

## Player and Physics

`Core/Player.cs`:

- **Creative mode** (default off at spawn): no gravity, free vertical movement with Space/Shift, unlimited resources.
- **Physics mode**: gravity, ground collision, jumping, fall damage, swimming, drowning.
- **Inventory**: 9-slot hotbar with `ItemStack` (blocks, tools with durability, fluid containers, food).
- **Hunger** (survival): drains over time; starvation damage at 0; low-hunger walk debuff; persisted in saves.
- **Skills**: mining, woodcutting, combat — XP and levels affect mining speed.
- **Spawn**: `FindSafeSpawnPosition()` searches surface near `(16, 16)`.

### Survival loop

| Component | Role |
|-----------|------|
| `SurvivalConstants.cs` | Hunger drain, starvation, ration cost, night-wolf caps |
| `FoodRegistry.cs` | Food items (`RawMeat`, `CookedMeat`, `Bread`) and hunger restore values |
| `FoodConsumption` | Right-click eat from hotbar; `Take Rations` from village food stock |
| `AnimalLoot.cs` | Raw meat drops on animal kill |
| `NightThreatSpawner.cs` | Spawns wolves at night outside shelter (cap 2; despawn at dawn) |
| `DeathConsequences.cs` | Drop half hotbar on death; 60% hunger on respawn |
| `EarlyGameGuide.cs` | Unified survival + village tutorial stages (`earlyGuideStage` in saves) |

Crafting: wood/stone swords at Bench; cooked meat at Forge; bread from wheat at Bench.

Camera (`Engine/Camera.cs`) syncs yaw/pitch/position from the player each frame.

---

## Tools and Skills

`Items/ToolRegistry.cs` defines pickaxes, axes, shovels, and swords across four tiers (Wood, Stone, Iron, Gold). `MiningCalculator.cs` computes effective break time from tool tier, block hardness, and skill level. `PlayerSkills.cs` tracks XP progression.

---

## Block Interaction

`Core/BlockInteractionSystem.cs`:

- Raycasts up to 5 blocks from the camera.
- **Left click:** if an animal is in range, `CombatSystem` handles melee; otherwise mines the targeted block via `MiningCalculator.GetEffectiveBreakTime()`.
- **Right click:** places the held block, eats food from hotbar, or uses bucket.
- **Shift+right click:** activates a sigil pattern → crafting station block.
- **Right click station:** opens crucible UI for Bench/Forge/Crucible.

---

## Animals

`Entities/AnimalManager.cs` manages Sheep, Pig, Chicken, and night-spawned Wolf entities:

- Spawn around player on world entry (`PopulateAroundSpawn`).
- Gravity, ground collision, wander AI with obstacle avoidance.
- Retaliation damage when attacked; death drops nothing yet.
- Spawn cap enforced per type in a radius around spawn.
- **Not persisted in saves** — animals respawn on world load.

Rendering uses atlas texture tiles (`sheep_body`, `pig_head`, etc.) as colored billboards.

---

## Crafting

### Sigils (`Crafting/SigilRegistry.cs`)

3D block-pattern recipes activated via shift+right-click:

| Sigil | Output |
|-------|--------|
| Workbench | `StationBench` |
| Forge | `StationForge` |
| Crucible | `StationCrucible` |

### Station recipes (`Crafting/CraftRecipeRegistry.cs`)

Crucible transmutation recipes with environment requirements (time of day, nearby blocks, heat). Examples: planks from logs, glass from sand, iron blocks.

### Discovery

`DiscoveryJournal` tracks unlocked sigil and recipe IDs. Persisted in save data. UI: `JournalScreen` (J key), `CrucibleScreen` (right-click station).

---

## Saves

`World/WorldSaveManager.cs` stores per-slot data as `world.json` (version 6):

- World seed and spawn coordinates
- Player position, velocity, health, creative mode, hotbar (`ItemStack` with tool durability)
- Player skills (mining/woodcutting/combat levels and XP)
- Block and fluid modifications
- Unlocked crafting discovery IDs
- Time of day, scale, pause state
- Villages (buildings, sites, storage, food, caps) and villagers (jobs, inventory)
- Claimed world-structure anchors

**Not saved:** animal positions (respawn on load). v5 saves migrate forward with empty buildings/sites.

Auto-save every 300 seconds and on exit/pause. Saves live under the OS local application data folder (see AGENTS.md).

---

## Settings

`Core/GameSettings.cs` — render distance (2–12 chunks, default 8), AI provider options, and audio volumes. Persisted via `GameSettingsManager` to `settings.json`.

| Audio field | Default | Description |
|-------------|---------|-------------|
| `MasterVolume` | 1.0 | Global multiplier |
| `SfxVolume` | 1.0 | One-shot effects (mine, place, combat) |
| `AmbientVolume` | 0.6 | Water/wind loops |
| `MusicVolume` | 0.5 | Menu and gameplay themes |
| `MuteAudio` | false | Hard mute |

Configure from main menu **SETTINGS** (all sliders) or pause menu **SETTINGS** (mute toggle).

---

## Audio

Procedural audio lives in `Engine/Audio/`. No external `.wav` files or MGCB audio content.

| Component | Role |
|-----------|------|
| `WavEncoder` | Builds in-memory 16-bit mono WAV streams |
| `WaveSynth` | Sine, noise, envelopes, filters |
| `ProceduralSfx` | One-shot presets (`SfxKind`) |
| `ProceduralAmbient` | Looping water/wind |
| `ProceduralMusic` | Menu and gameplay themes (`MusicState`) |
| `AudioManager` | Playback pool, crossfade, ambient proximity, volume |

**Lifecycle:** `AutonocraftGame.LoadContent()` creates and initializes `AudioManager`, binds it to `GameSession`, and disposes on exit. In `--test` mode audio is disabled (no OpenAL).

**Event wiring:** `GameSession.WireNotifications()` connects `PlaySfx` callbacks on `BlockInteractionSystem`, `CombatSystem`, and crafting discovery. Footsteps/jump/land are handled in `GameSession.UpdateMovementAudio()`.

**Music states:** `MainMenu`/`NewWorldSetup` → menu theme; `WorldLoading`/`Playing` → gameplay theme (2 s crossfade). Pause and crafting UIs duck ambient/music to 30%.

---

## Villages and settlers

Villages are the default gameplay loop — not an optional side system.

| Component | Role |
|-----------|------|
| `VillageManager` | Facade over founding, job dispatch, simulation, persistence |
| `VillageFoundingService` | Starter settlement, found/claim structures |
| `JobDispatcher` / `HaulCoordinator` | Job assignment, haul logistics |
| `VillageSimulation` | Per-frame tick, sleep cycle, building finalization |
| `VillagePersistence` | Save v7 export/load |
| `Village/Jobs/*` | Per-job AI handlers (`IVillagerJob`, `JobRegistry`) |
| `Village/Economy/*` | Food stock, storage, haul/farm/workshop simulation |
| `Village/Founding/*` | Starter settlement, structure claim, blueprint placement |
| `Village/Persistence/*` | Save v7 export/load |
| `Village/AI/*` | Goal parsing, next-best-action HUD hints |
| `VillageEvents` / `VillageGuidance` | Player feedback toasts and next-best-action hints |
| `Village` | Storage, food/happiness sim, tier progression, building sites |
| `Villager` / `VillagerManager` | Entity state + registry; AI delegated to job handlers |
| `VillageScreen` + `VillageViewModel` | Town board UI with plain-language villager activity |
| `VillageAiOrchestrator` (on `GameSession`) | Shared in-game + HTTP steward chat |

**Jobs:** Idle, Lumber, Mine, Farm, Build, Haul, Craft, Hunt, Mason, Cook (+ Sleep at night).

**Economy:** `VillageEconomy` tracks supply/demand; farmers/hunters/cooks add `FoodStock`; daily consumption affects happiness.

**Persistence (v7):** Extended villager mid-task state (haul, equipped tool, AI phase), output chest buffers, village radius, onboarding flag.

---

## Content Pipeline

`Autonocraft.csproj` runs MGCB before build:

```
Content/Content.mgcb → Content/BlockEffect.xnb
```

Skip with `-p:SkipMonoGameContent=true` for faster C#-only iteration.

`dotnet-tools.json` pins `dotnet-mgcb` 3.8.2.1105. Run `dotnet tool restore` on first clone.

---

## Testing Architecture

`tests/Autonocraft.Tests/` contains domain-split integration and unit tests. `Program.cs --test` delegates to the test runner for backward compatibility.

- No `Run()` → no game window.
- `GraphicsDevice` may be null; chunk mesh building uses null-safe paths.
- Saves/settings redirected to temp directories, cleaned up in `finally`.
- Tests exercise simulation via `GameSession` APIs: physics ticks, block ops, save round-trip.

---

## External libraries (deferred)

| Library | Status | Notes |
|---------|--------|-------|
| **FastNoiseLite** | Skipped | No maintained NuGet for the official [Auburn/FastNoiseLite](https://github.com/Auburn/FastNoiseLite) C# port; upstream ships a single vendored `FastNoiseLite.cs`. Terrain keeps `PerlinNoiseProvider` behind `INoiseProvider` until a vetted package or vendored copy is added. |
| **Refit** | Skipped | `OpenRouterClient` uses hand-built `HttpClient` + `JsonSerializer`; Refit would add API surface and test doubles without simplifying the existing `IOpenRouterClient` / `MockOpenRouterClient` pattern. |

---

## Legacy / Unused

- `openrouter_key.txt` — placeholder for planned LLM integration; no runtime code consumes it yet.
