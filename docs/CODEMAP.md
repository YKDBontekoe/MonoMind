# Autonocraft Code Map

Agent-oriented navigation index. Pair with [AGENTS.md](../AGENTS.md) for build/test commands and [ARCHITECTURE.md](ARCHITECTURE.md) for subsystem design.

**Repo name:** MonoMind. **Game/project name:** Autonocraft.

**Folder budget:** aim for ≤15 `.cs` files per directory; split when exceeded.

---

## Assembly map

| Project | Path | Depends on |
|---------|------|------------|
| `Autonocraft.Domain` | `src/Autonocraft.Domain/` | — (leaf: enums, constants, save DTOs, contracts) |
| `Autonocraft.Diagnostics` | `src/Autonocraft.Diagnostics/` | Domain |
| `Autonocraft.Items` | `src/Autonocraft.Items/` | Domain |
| `Autonocraft.World` | `src/Autonocraft.World/` | Domain, Diagnostics, Items |
| `Autonocraft.Crafting` | `src/Autonocraft.Crafting/` | Domain, Items, World |
| `Autonocraft.Ai` | `src/Autonocraft.Ai/` | Domain |
| `Autonocraft.Village` | `src/Autonocraft.Village/` | Domain, Items, World, Entities, Crafting |
| `Autonocraft.Engine` | `src/Autonocraft.Engine/` | Domain, Diagnostics, Items, World, Entities, Village |
| `Autonocraft.Core` | `src/Autonocraft.Core/` | Domain, Diagnostics, Items, World, Entities, Village, Crafting, Ai, Engine |
| `Autonocraft` (exe) | `src/Autonocraft/` | Core, Engine |
| `Autonocraft.Tests` | `tests/Autonocraft.Tests/` | Exe + libs |

**Still in the executable:** `AutonocraftGame`, `ScreenManager`, `UI/`, content assets (`atlas.png`, fonts).

Dependency rules are enforced by `tests/Autonocraft.Tests/Unit/AssemblyDependencyRulesTests.cs`.

---

## `Autonocraft.Domain/`

| Subfolder | Contents |
|-----------|----------|
| `Core/` | `GameDefaults`, `DayNightCycle`, `PlayerConstants` |
| `World/` | `BlockType`, `WorldConstants`, `IBlockReader`, `IBlockWriter` |
| `Items/` | `ItemId`, `ItemKind`, `ToolType`, `IPlayerInventory` |
| `Crafting/` | `MaterialTag`, `ICraftingFx`, `ICraftingHudHint` |
| `Rendering/` | `IBlockInteractionOverlay`, `IPlayerMotionView`, `CrosshairState`, `PlacePopEffect` |
| `Entities/` | `IPlayerAmbientView` |
| `Village/` | `VillagerRole`, `JobType`, `BuildingKind`, … |
| `Persistence/` | `WorldSaveData`, `SaveSnapshot`, all save DTOs |

## `Autonocraft.Diagnostics/`

| File | Description |
|------|-------------|
| `PerfCounters.cs` | Frame/update/draw profiling counters |
| `WorldDebugTrace.cs` | Optional chunk/mesh debug hook (wired from Core) |

## `Autonocraft.Items/`

| Subfolder | Contents |
|-----------|----------|
| `Inventory/` | `Inventory`, `ItemStack`, `IItemContainer`, slot interaction |
| `Tools/` | `ToolRegistry`, `MiningCalculator`, `BlockHarvestCategory` |
| `Food/` | `FoodRegistry` |
| `Rendering/` | `IPlayerHudView`, `IItemEntityRenderView` |
| (root) | `MaterialRegistry`, `PlayerSkills`, villager skill types |

## `Autonocraft.World/`

| Subfolder | Contents |
|-----------|----------|
| `Chunks/` | `Chunk` (partial), `ChunkStorage`, `ChunkMeshBuilder`, `ChunkGpuResources`, `ChunkLod`, mesh schedulers, `Vertex`, `FloraVertex`, `FloraMeshBuilder` |
| `Streaming/` | `ChunkStreamCoordinator`, `ChunkWorkQueue` (partial facets of `VoxelWorld`) |
| `Generation/` | `WorldGenerator`, biomes, terrain, caves, ores, noise (`INoiseProvider`) |
| `Fluids/` | `FluidSystem`, `WaterQuery`, `LavaQuery` |
| `Atlas/` | `BlockAtlas`, `AtlasLayout`, texture blend |
| `Structures/` | procedural structure placement |
| (root) | `VoxelWorld` (core: block get/set, modifications, public API) |

**Large-type decomposition (partial classes, same assembly):**

| Logical component | File | Responsibility |
|-------------------|------|------------------|
| `VoxelWorld` core | `VoxelWorld.cs` | Block get/set, modifications dict, chunk registration, save/export |
| `ChunkStreamCoordinator` | `Streaming/ChunkStreamCoordinator.cs` | `UpdateChunksAround`, load/unload, `BeginInitialLoad` / `AdvanceInitialLoad` |
| `ChunkWorkQueue` | `Streaming/ChunkWorkQueue.cs` | `ProcessPendingWork`, mesh candidate selection, `TerrainGenScheduler` / `MeshBuildScheduler` wiring |
| `ChunkStorage` | `Chunks/ChunkStorage.cs` | Block array, column height cache |
| `ChunkMeshBuilder` | `Chunks/ChunkMeshBuilder.cs` | CPU mesh Full/Surface/Shell + flora |
| `ChunkGpuResources` | `Chunks/ChunkGpuResources.cs` | Vertex/index buffers, upload, invalidation |

## `Autonocraft.Entities/`

| Subfolder | Contents |
|-----------|----------|
| `Animals/` | `Animal`, `AnimalType` |
| `Collision/` | `EntityCollision`, `EntityRaycast` |
| `Navigation/` | `VoxelPathfinder` |

Animal AI managers live in `src/Autonocraft.Entities/Animals/`; `NightThreatSpawner` is in Core.

---

## `Autonocraft.Core/` — game loop, player, systems

| File | Description |
|------|-------------|
| `GameSession.cs` | Owns player/world/systems; core gameplay simulation tick |
| `GameConstants.cs` | Spawn coords, autosave interval |
| `Player.cs` | Physics, inventory, hunger, skills; implements render read-model interfaces |
| `PlayerStatistics.cs` | Lifetime counters (distance, kills, play time, early guide stage) |
| `EarlyGameGuide.cs` | Unified early-game survival + village tutorial |
| `DeathConsequences.cs` | Death hotbar drop and respawn hunger restore |
| `AnimalLoot.cs` | Animal kill loot + `FoodConsumption` helpers |
| `BlockInteractionSystem.cs` | Raycast, mining, placing; implements `IBlockInteractionOverlay` |
| `CombatSystem.cs` | Melee combat, fall effects, respawn |
| `AgentHttpServer.cs` | HTTP listener + route dispatch (port 5001 default) |
| `Agent/Handlers/` | `StateHandler`, `ActionHandler`, `VillageChatHandler`, `ScreenshotHandler` |
| `Agent/Serialization/AgentStateSerializer.cs` | `/state` and village-debug DTO assembly |
| `DevCommands.cs` | F3/`~` dev console commands |
| `GameSettingsManager.cs` | settings.json persistence |
| `World/WorldSaveManager.cs` | Save/load world.json |
| `World/Persistence/SaveJsonContext.cs` | STJ source-generated save serializers |
| `ItemEntity.cs` | Dropped item entities; implements `IItemEntityRenderView` |

## `Autonocraft.Engine/` — rendering and effects

| File | Description |
|------|-------------|
| `GameRenderContext.cs` | Read-only snapshot for rendering (Domain/Items interfaces only) |
| `Renderer.cs` | Thin draw orchestrator |
| `WorldRenderer.cs` | Sky, terrain, water, flora, animals, overlay |
| `HudRenderer.cs` | HUD: crosshair, hotbar, health, skills |
| `Camera.cs` | View/projection matrices |
| `BlockTerrainEffect.cs` | Terrain `BasicEffect` wrapper (fog + sun/moon lights) |
| `BlockOverlayRenderer.cs` | Block highlight outline |
| `SceneLighting.cs` | Time-of-day sun/moon/ambient |
| `Textures/` | Procedural tiles: `TerrainTiles`, `FloraTiles`, `EntityTiles`, `StationTiles` |
| `Animation/` | `InteractionAnimator`, `UiTransition`, `Tween` |
| `Audio/` | `AudioManager`, procedural SFX/ambient/music |

---

## Executable (`src/Autonocraft/`) — folder index

### Game shell (references Core + Engine libs)

| File | Description |
|------|-------------|
| `AutonocraftGame.cs` | MonoGame shell: lifecycle, simulation tick, agent bridge (`partial`) |
| `Game/GameStateMachine.cs` | MainMenu → NewWorldSetup → WorldLoading → Playing transitions |
| `Game/GameInputRouter.cs` | Keyboard/mouse, agent simulated keys, hotbar, sprint, combat input |
| `Game/GameOverlayRouter.cs` | Pause, death, inventory, village UI, crucible, journal stack |
| `Game/GamePersistenceCoordinator.cs` | Autosave, new world, load save |
| `Game/AutonocraftGame.Draw.cs` | `Draw` / `DrawFrame` partial |
| `GameServiceProvider.cs` | M.E.DI composition root |
| `ScreenManager.cs` | Screen stack, fades |
| `GameIntegrationTests.cs` | Thin wrapper delegating to test project |

### `Items/` — tools, skills, mining

| File | Description |
|------|-------------|
| `ToolRegistry.cs` | Tool definitions and creation |
| `FoodRegistry.cs` | Food item hunger restore values |
| `ToolDefinition.cs` | Single tool stats |
| `ToolType.cs` | Pickaxe, Axe, Shovel, Sword |
| `ToolTier.cs` | Wood, Stone, Iron, Gold |
| `MiningCalculator.cs` | Break time from tool + skill |
| `ItemStack.cs` | Hotbar slot (block, tool, bucket) |
| `ItemId.cs` | Item identifier enum |
| `ItemKind.cs` | Empty, Block, Tool, FluidContainer |
| `PlayerSkills.cs` | Mining/woodcutting/combat XP |
| `PlayerSkill.cs` | Single skill level + XP |
| `SkillProgress.cs` | XP curve helpers |
| `BlockHarvestCategory.cs` | Block → harvest type mapping |

### `Crafting/` — sigils, recipes, crucible

| File | Description |
|------|-------------|
| `CraftingSystem.cs` | Sigil activation, crucible, journal |
| `CraftRecipeRegistry.cs` | Station transmutation recipes |
| `CraftRecipe.cs` | Single recipe definition |
| `SigilRegistry.cs` | 3D sigil patterns |
| `SigilPattern.cs` | Relative block positions in sigil |
| `MaterialTag.cs` | Wood/stone material tags for sigils |
| `CraftEnvironment.cs` | Time/heat/block environment checks |
| `DiscoveryJournal.cs` | Unlocked recipe persistence |

### `Entities/` — animals

| File | Description |
|------|-------------|
| `AnimalManager.cs` | Spawn, update, population cap |
| `Animal.cs` | Single animal state and AI |
| `AnimalType.cs` | Sheep, Pig, Chicken, Wolf stats |
| `NightThreatSpawner.cs` | Night wolf spawns near player |
| `EntityCollision.cs` | Animal-world collision |
| `EntityRaycast.cs` | Raycast against animals |

### `Autonocraft.Village/` — settlements, jobs, economy

| Subfolder | Contents |
|-----------|----------|
| `Economy/` | `VillageEconomy`, `VillageStorage`, haul/farm/workshop, `GatherWorkQueue`, `BuildingEffects` |
| `Founding/` | `VillageFoundingService`, spawn/claim helpers, blueprint placement previews |
| `Persistence/` | `VillagePersistence` (save v7 export/load) |
| `AI/` | `VillageGoalParser`, `VillageGuidance` (HUD hints) |
| `Jobs/` | Per-job AI (`IVillagerJob`, `JobRegistry`, `*Job.cs`) |
| (root) | `Village`, `VillageManager`, `VillageSimulation`, `JobDispatcher`, `Villager*`, buildings |

### `Village/` (exe UI)

| File | Description |
|------|-------------|
| `UI/VillageScreen.cs` | Town board (V key) |
| `UI/Village/VillageViewModel.cs` | UI snapshot with plain-language activity |

### `UI/` — screens and overlays

| File | Description |
|------|-------------|
| `SaveSlotScreen.cs` | Main menu save slots, lifetime stats strip |
| `PlayerDashboardScreen.cs` | Home-screen player stats overlay (STATS button) |
| `NewWorldSetupScreen.cs` | Seed and world type picker |
| `LoadingScreen.cs` | Chunk load progress |
| `PauseMenuScreen.cs` | Pause overlay |
| `DeathScreen.cs` | Death and respawn |
| `CrucibleScreen.cs` | Crucible crafting UI |
| `JournalScreen.cs` | Discovery journal (J key) |
| `DevConsole.cs` | F3/`~` console overlay |
| `MainMenuScreen.cs` | Legacy menu (unused) |

---

## Task Playbooks

### Add a block type

1. Add enum value in `World/BlockType.cs` (+ extension methods: `IsSolid`, `IsTransparent`, etc.)
2. Add texture tile in `ProceduralAtlasBuilder.Tiles.cs` (or `ProceduralTextureSynth`) and run `dotnet run --project src/Autonocraft.AtlasBuild`
3. Update `atlas_layout.json` if needed
4. Add harvest category in `Items/BlockHarvestCategory.cs` if mineable
5. Update `BlockInteractionSystem.cs` if special behavior (station, passable, fluid)
6. Run `--test` → `TestMiningAndPlacing`

### Add a craft recipe

1. Define recipe in `Crafting/CraftRecipeRegistry.cs`
2. Add environment checks in `Crafting/CraftEnvironment.cs` if needed
3. Wire output block in `CraftingSystem.TryTransmute()`
4. Run `--test` → `TestNewCraftRecipes`, `TestCruciblePlankRecipe`

### Add a sigil pattern

1. Define pattern in `Crafting/SigilRegistry.cs`
2. Add journal ID in `Crafting/DiscoveryJournal.cs`
3. Run `--test` → `TestSigilBenchActivation`

### Change player physics

1. Edit `Core/Player.cs`
2. Run `--test` → `TestGravityAndCollision`, `TestJumping`, `TestFallDamage`, fluid tests

### Change mining speed

1. Edit `Items/MiningCalculator.cs` and/or `Items/ToolRegistry.cs`
2. Run `--test` → `TestToolMiningSpeed`, `TestSkillProgression`

### Change world generation

1. Edit relevant pipeline file (`BiomeMap`, `TerrainShaper`, `Decorator`, etc.)
2. Run `--test` → `TestWorldGenerationBasics`, `TestBiomeTreeSpecies`, `TestStructureGeneration`

### Change rendering

1. World geometry: `Autonocraft.Engine/WorldRenderer.cs`, `BlockTerrainEffect.cs`
2. Sky/time-of-day: `Autonocraft.Engine/SceneLighting.cs`, `SkyColor.cs`, `SkyBoxRenderer.cs` (`SkyDomeRenderer`), `CloudLayerRenderer.cs`
3. Shared day/night phases: `Autonocraft.Domain/Core/DayNightCycle.cs`
4. HUD: `Autonocraft.Engine/HudRenderer.cs`
5. Run `--test` then visual check:
   ```bash
   dotnet run --project src/Autonocraft -- --skip-menu
   python3 tests/interact.py screenshot check.png
   ```

### Add or change a sound

1. Add a `SfxKind` value in `Autonocraft.Engine/Audio/SfxKind.cs` (or extend `ProceduralAmbient` / `ProceduralMusic`)
2. Implement the preset in `ProceduralSfx.cs` (or ambient/music builder)
3. Cache it in `AudioManager.Initialize()` if adding a new category
4. Fire from the gameplay system via `PlaySfx` callback or `GameSession.BindAudio()`
5. Run `--test` (audio is disabled in test mode; verifies no regressions)

### Change save format

1. Bump version in `World/WorldSaveData.cs`
2. Update `World/WorldSaveManager.cs` and `Core/SaveSnapshot.cs`
3. Run `--test` → `TestWorldSaveRoundTrip`

---

## God-File Split Map

| Original | Extracted to | Status |
|----------|--------------|--------|
| `AutonocraftGame.cs` (~635 LOC) | `Game/Game*.cs` partials, `GameSession.cs` | Refactored |
| `Renderer.cs` (~1385 LOC) | `WorldRenderer.cs`, `HudRenderer.cs`, `PixelFont.cs` | Refactored |
| `GameIntegrationTests.cs` (~1842 LOC) | `tests/Autonocraft.Tests/Integration/*.cs` (incl. `SurvivalTests.cs`) | Refactored |

---

## Test Coverage Map

| Subsystem | Integration test(s) |
|-----------|---------------------|
| Settings | `RunGameSettingsRoundTrip` |
| Chunk LOD | `RunChunkLodBands`, `RunChunkLodMeshCounts`, `RunChunkStreamingStability` |
| World gen | `RunWorldGenerationBasics`, `RunBiomeTreeSpecies` |
| Structures | `RunStructureGeneration` |
| Physics | `RunGravityAndCollision`, `RunJumping`, `RunFallDamage`, `RunPassableBlocks` |
| Fluids | `RunSwimThroughWater`, `RunDrowning`, `RunFallDamageInWater`, `RunFluidSpread`, `RunBucketPlaceAndPickup`, `RunFluidSaveRoundTrip`, `RunNoWalkOnWater`, `RunWaterFillsExcavatedGap` |
| Inventory | `RunInventory` |
| Tools/skills | `RunToolMiningSpeed`, `RunToolDurability`, `RunSkillProgression` |
| Mining/placing | `RunMiningAndPlacing`, `RunClickPriority` |
| Saves | `RunWorldSaveRoundTrip`, `RunPlayerStatisticsRoundTrip` |
| Animals | `RunAnimalGravity`, `RunAnimalWanderCollision`, `RunAnimalSpawnCap` |
| Combat | `RunPlayerTakeDamage`, `RunEntityRaycast`, `RunMeleeKillAnimal` |
| Crafting | `RunSigilBenchActivation`, `RunCruciblePlankRecipe`, `RunNewCraftRecipes` |

Unit tests (fast, no game boot): `MiningCalculatorTests`, `CraftRecipeRegistryTests`, `ChunkLodTests`, `SigilRegistryTests`.
