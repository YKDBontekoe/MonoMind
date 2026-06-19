# Contract: Protected Domain Coverage Mapping

## Purpose

Map each regression-sensitive domain (AGENTS.md / constitution Principle II) to
at least one executable automated check. Satisfies FR-005 and SC-002.

## Domain Matrix

| Protected Domain | Primary Check Type | Automated Mapping |
|------------------|-------------------|-------------------|
| Player physics, movement, gravity, collision | Integration | `--test`: `RunGravityAndCollision`, `RunJumping`, `RunSlabStairWalking`, `RunPassableBlocks` (PhysicsTests) |
| World generation, chunking, biomes | Integration | `--test`: `RunWorldGenerationBasics`, `RunChunkLodBands`, `RunChunkStreamingStability`, `RunNewSurfaceBiomes`, `RunBiomeTreeSpecies` |
| Blocks, mining, placing | Integration + Unit | `--test`: `RunMiningAndPlacing`; unit: `MiningCalculatorTests` |
| Inventory, tools, skills | Integration | `--test`: `RunInventory`, `RunToolMiningSpeed`, `RunToolDurability`, `RunSkillProgression` |
| Animals, combat | Integration + Unit | `--test`: `RunAnimalGravity`, `RunAnimalWanderCollision`, `RunMeleeKillAnimal`, `RunPlayerTakeDamage`; unit: `AnimalPanicTests` |
| Crafting | Integration + Unit | `--test`: `RunSigilBenchActivation`, `RunCruciblePlankRecipe`, `RunPlayer2x2CraftGrid`, `RunShapedToolBenchCraft`, `RunNewCraftRecipes`; unit: `CraftRecipeRegistryTests`, `GridCraftingTests` |
| Fluids | Integration | `--test`: `RunFluidSpread`, `RunBucketPlaceAndPickup`, `RunFluidSaveRoundTrip`, `RunNoWalkOnWater`, `RunWaterFillsExcavatedGap` |
| Structures | Integration | `--test`: `RunStructureGeneration`, `RunStructureGallery`, `RunImprovedBuildingCatalogQuality` |
| Saves, persistence | Integration | `--test`: `RunWorldSaveRoundTrip`, `RunPlayerStatisticsRoundTrip`, `RunImprovedBuildingSaveRoundTrip`, `RunHungerSaveRoundTrip` |
| Villagers, villages | Integration + Agent E2E | `--test`: `RunStarterSettlementBeforeChunksLoaded`, `RunVillageFoundAndRecruit`, `RunVillageSaveRoundTripV6/V7`, `RunAgentHttpVillageBridgeE2E`, village partials in `VillageTests.*`; E2E: `test_live_api.py`, `run_scenario.py --all` via `scripts/ci_e2e.sh` |
| UI, HUD, rendering layout | Integration | `--test`: `RunVillageScreenInputLayout`, `RunPeopleTabCitizenDifferentiation`; manual: screenshot scenarios in game-test skill |
| Agent HTTP API | Agent E2E | CI job `Agent E2E (Linux)` → `scripts/ci_e2e.sh` → `test_live_api.py`, `run_scenario.py` |
| Atlas / procedural content | Quality gate | `Autonocraft.AtlasBuild --check` (also CI `Fast gates`) |
| Assembly boundaries | Unit | `AssemblyDependencyRulesTests` |
| Settings | Integration | `--test`: `RunGameSettingsRoundTrip` |
| Survival / hunger | Integration | `--test`: `RunHungerDrain`, `RunEatFood`, `RunVillageRations`, `RunDeathPenalty` |

## Manual / Scheduled (documented exclusions)

| Workflow | Owner | Trigger | Rationale |
|----------|-------|---------|-----------|
| `tests/verify_terrain_slabs.py` | Rendering maintainers | Manual before terrain PRs | Requires visible window |
| `tests/capture_structure_gallery.py` | Art/structures | Manual gallery refresh | Visual asset capture |
| `tests/interact.py` | Contributors | Ad-hoc debugging | CLI helper, not a suite |
| `tests/live_villager_e2e.py` | Village maintainers | Manual deep villager smoke | **Deprecated** — see `docs/ci/manual-verification.md` |

## Maintenance Rules

1. Adding a new protected domain in AGENTS.md REQUIRES updating this matrix in the same PR.
2. Removing a test MUST update the matrix or add replacement coverage.
3. `IntegrationTestRunner` / `--test` inventory is authoritative for integration rows; link `Run*` method names when adding rows.
