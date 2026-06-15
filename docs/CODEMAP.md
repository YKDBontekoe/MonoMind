# Autonocraft Code Map

Agent-oriented navigation index. Pair with [AGENTS.md](../AGENTS.md) for build/test commands and [ARCHITECTURE.md](ARCHITECTURE.md) for subsystem design.

**Repo name:** MonoMind. **Game/project name:** Autonocraft.

---

## Folder Index

### `Core/` — game loop, player, systems

| File | Description |
|------|-------------|
| `AutonocraftGame.cs` | MonoGame shell: state machine, input routing, UI overlays, lifecycle |
| `GameSession.cs` | Owns player/world/systems; core gameplay simulation tick |
| `GameRenderContext.cs` | Read-only snapshot for rendering (decouples Engine from game) |
| `GameConstants.cs` | Spawn coords, autosave interval |
| `SaveSnapshot.cs` | DTO for save serialization |
| `Player.cs` | Physics, inventory, hunger, skills, swimming, stats tracking |
| `PlayerStatistics.cs` | Lifetime counters (distance, kills, play time, etc.) |
| `SurvivalConstants.cs` | Hunger, death, wolf spawn tuning |
| `EarlyGameGuide.cs` | New-world onboarding step machine + offline steward replies |
| `DeathConsequences.cs` | Hotbar loss and tool wear on death |
| `AnimalLoot.cs` | Animal kill drop table (raw meat) |
| `BlockInteractionSystem.cs` | Raycast, mining, placing, eating, sigils, station interaction |
| `CombatSystem.cs` | Melee combat, hunger tick, fall effects, respawn |
| `AgentHttpServer.cs` | HTTP API on port 5000 |
| `DevCommands.cs` | F3/`~` dev console commands |
| `GameState.cs` | MainMenu / NewWorldSetup / WorldLoading / Playing |
| `GameSettings.cs` | Render distance settings |
| `GameSettingsManager.cs` | settings.json persistence |
| `GameIntegrationTests.cs` | Thin wrapper delegating to test project |
| `Input.cs` | Key enum helpers |

### `World/` — voxel world and generation

| File | Description |
|------|-------------|
| `VoxelWorld.cs` | Chunk dict, async gen/mesh, modifications |
| `Chunk.cs` | Mesh building, GPU buffers, LOD meshes |
| `ChunkLod.cs` | Distance bands for Full/Surface/Shell |
| `WorldGenerator.cs` | Per-chunk terrain pipeline orchestrator |
| `BiomeMap.cs` | Temperature/moisture → biome |
| `TerrainShaper.cs` | Biome-driven height and block layers |
| `TerrainPostProcessor.cs` | River carving |
| `CaveCarver.cs` | 3D noise tunnels |
| `OrePlacer.cs` | Coal/iron/gold placement |
| `Decorator.cs` | Trees, grass, flowers per biome |
| `BlockType.cs` | 43 block types + extension methods |
| `FluidSystem.cs` | Water flow simulation |
| `WaterQuery.cs` | Swimming/drowning/underwater helpers |
| `WorldSaveManager.cs` | Save/load world.json |
| `WorldSaveData.cs` | Save format v4 schema |
| `BlockAtlas.cs` | BlockType → UV mapping |
| `AtlasLayout.cs` | atlas_layout.json loader |
| `WorldGenParams.cs` | Generation presets per world type |
| `WorldConstants.cs` | Sea level, default seed |
| `MeshBuildContext.cs` | Per-vertex blend during mesh build |
| `BlockTextureBlend.cs` | Biome transition weights |

### `World/Structures/` — procedural buildings

| File | Description |
|------|-------------|
| `StructureRegistry.cs` | 12 structure type definitions |
| `StructurePlacer.cs` | Places structures during world gen |
| `StructureTemplate.cs` | Multi-block layout templates |
| `StructureDefinition.cs` | Biome/tier placement rules |
| `StructureBlock.cs` | Single block in a template |
| `StructureTier.cs` | Structure size tiers |
| `StructurePlacementMode.cs` | Placement strategy enum |

### `Engine/` — rendering and effects

| File | Description |
|------|-------------|
| `Renderer.cs` | Thin draw orchestrator |
| `WorldRenderer.cs` | Sky, terrain, water, flora, animals, overlay |
| `HudRenderer.cs` | HUD: crosshair, hotbar, health, skills |
| `PixelFont.cs` | Bitmap glyph data |
| `Camera.cs` | View/projection matrices |
| `BlockTerrainEffect.cs` | Terrain `BasicEffect` wrapper (fog + sun/moon lights) |
| `BlockOverlayRenderer.cs` | Block highlight outline |
| `SkyEffect.cs` | Sky/cloud `BasicEffect` wrapper |
| `SkyBoxRenderer.cs` | Cached hemisphere skydome (`SkyDomeRenderer`) |
| `CloudLayerRenderer.cs` | Three scrolling cloud layers |
| `SkyColor.cs` | CPU sky gradient + procedural stars |
| `FloraRenderer.cs` | Tall grass/flower billboards |
| `ParticleSystem.cs` | Block break and water splash particles |
| `SceneLighting.cs` | Time-of-day sun/moon/ambient |
| `ProceduralAtlasBuilder.cs` | Runtime atlas fallback |
| `ProceduralTextureSynth.cs` | Procedural tile generation |
| `FloraMeshBuilder.cs` | Flora geometry |
| `Vertex.cs` | Chunk vertex format |
| `UiRenderer.cs` | Menu screen rendering |
| `Animation/InteractionAnimator.cs` | Mining recoil, tool swing |
| `Animation/UiTransition.cs` | Screen fade transitions |
| `Animation/Tween.cs` | Easing helpers |

### `Engine/Audio/` — procedural sound

| File | Description |
|------|-------------|
| `AudioManager.cs` | SFX pool, ambient/music loops, volume, state crossfade |
| `SfxKind.cs` | One-shot effect identifiers |
| `MusicState.cs` | Menu vs gameplay music |
| `WavEncoder.cs` | Float samples → WAV `MemoryStream` |
| `WaveSynth.cs` | Oscillators, noise, ADSR, filters |
| `ProceduralSfx.cs` | Mine, place, combat, UI, movement presets |
| `ProceduralAmbient.cs` | Water and wind loops |
| `ProceduralMusic.cs` | Menu pad and gameplay arpeggio loops |

### `Items/` — tools, skills, mining

| File | Description |
|------|-------------|
| `ToolRegistry.cs` | Tool definitions and creation |
| `ToolDefinition.cs` | Single tool stats |
| `ToolType.cs` | Pickaxe, Axe, Shovel, Sword |
| `ToolTier.cs` | Wood, Stone, Iron, Gold |
| `MiningCalculator.cs` | Break time from tool + skill |
| `ItemStack.cs` | Hotbar slot (block, tool, bucket, consumable) |
| `FoodRegistry.cs` | Consumable hunger restore values |
| `ItemKind.cs` | Empty, Block, Tool, FluidContainer, Consumable |
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
| `AnimalManager.cs` | Spawn, update, population cap, hostile tracking |
| `Animal.cs` | Single animal state, wander AI, hostile chase |
| `AnimalType.cs` | Sheep, Pig, Chicken, Wolf |
| `NightThreatSpawner.cs` | Night wolf spawns and safe-zone checks |
| `EntityCollision.cs` | Animal-world collision |
| `EntityRaycast.cs` | Raycast against animals |

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
2. Add texture tile in `scripts/build_atlas.py` and run `python3 scripts/build_atlas.py`
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

1. World geometry: `Engine/WorldRenderer.cs`, `Engine/BlockTerrainEffect.cs`
2. Sky/time-of-day: `Engine/SceneLighting.cs`, `Engine/SkyColor.cs`, `Engine/SkyBoxRenderer.cs` (`SkyDomeRenderer`), `Engine/CloudLayerRenderer.cs`
3. Shared day/night phases: `Autonocraft.Domain/Core/DayNightCycle.cs`
4. HUD: `Engine/HudRenderer.cs`
5. Run `--test` then visual check:
   ```bash
   dotnet run --project src/Autonocraft -- --skip-menu
   python3 tests/interact.py screenshot check.png
   ```

### Add or change a sound

1. Add a `SfxKind` value in `Engine/Audio/SfxKind.cs` (or extend `ProceduralAmbient` / `ProceduralMusic`)
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
| `AutonocraftGame.cs` (~1290 LOC) | `GameSession.cs` (simulation), `GameConstants.cs` | Refactored |
| `Renderer.cs` (~1385 LOC) | `WorldRenderer.cs`, `HudRenderer.cs`, `PixelFont.cs` | Refactored |
| `GameIntegrationTests.cs` (~1842 LOC) | `tests/Autonocraft.Tests/Integration/*.cs` | Refactored |

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
