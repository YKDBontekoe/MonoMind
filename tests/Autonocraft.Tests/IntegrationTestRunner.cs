using System;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Tests.Integration;

namespace Autonocraft.Tests;

public static class IntegrationTestRunner
{
    public static bool Run()
    {
        Console.WriteLine("\n==================================================================");
        Console.WriteLine("RUNNING AUTONOCRAFT AUTOMATED INTEGRATION TESTS");
        Console.WriteLine("==================================================================");

        using var host = new TestHost();

        try
        {
            WorldGenTests.RunGameSettingsRoundTrip();
            VillageTests.RunVillageScreenInputLayout();
            VillageTests.RunBlueprintPlacementHelper();
            VillageTests.RunCanPlaceBlueprint();
            VillageTests.RunQueuedBuildingSiteSurvivesSync();
            VillageTests.RunStarterSettlementBeforeChunksLoaded();
            VillageTests.RunLiveStyleJobAssignmentHasWorld();
            VillageTests.RunFullVillageLifecycleJobs();
            VillageTests.RunAgentHttpVillageBridgeE2E();
            WorldGenTests.RunChunkLodBands();
            WorldGenTests.RunChunkLodMeshCounts();
            WorldGenTests.RunOceanShellMeshSurfaces();
            ChunkStreamingTests.RunInitialLoadWaitsForInFlightGeneration();
            ChunkStreamingTests.RunFaultedChunkGenerationDoesNotCrash();
            ChunkStreamingTests.RunChunkUnloadDiscardsStaleInFlight();
            ChunkStreamingTests.RunEnsureChunksLoadedDoesNotUnloadPlayerRadius();
            WorldGenTests.RunChunkStreamingStability();
            WorldGenTests.RunWorldGenerationBasics();
            TerrainSlabTests.RunTerrainSlabUnitRules();
            TerrainSlabTests.RunTerrainSlabPlacementRules();
            TerrainSlabTests.RunTerrainSlabUpperStepRegression();
            TerrainSlabTests.RunGeneratedWorldHasNoMountainSlabs();
            WorldGenTests.RunStructureGeneration();

            VillageTests.RunInventoryStacking();
            VillageTests.RunBlockActionService();
            VillageTests.RunClaimWorldStructure();
            VillageTests.RunFarmFoodProduction();
            VillageTests.RunBuildingJobWiring();
            VillageTests.RunVillagerToolMining();
            VillageTests.RunVillageAiToolsMock();
            VillageTests.RunVillageNumericGoals();
            VillageTests.RunPlayerWorkQueue();
            VillageTests.RunRepairMissingCitizens();
            VillageTests.RunVillagerLumberChopping();
            VillageTests.RunAdoptOrphanedCitizens();
            VillageTests.RunVillageRegistryDesyncLiveChop();
            VillageTests.RunVillageGuidanceHints();
            VillageTests.RunVillageEventsNotifier();
            VillageTests.RunStarvationConsequences();
            VillageTests.RunVillageRename();

            using (var game = new AutonocraftGame(runTests: true))
            {
                var player = game.Player;
                var world = game.Grid;

                world.UpdateChunksAround(null, player.Position, 2);

                PhysicsTests.RunGravityAndCollision(player, world);
                PhysicsTests.RunJumping(player, world);
                PhysicsTests.RunSlabStairWalking(player, world);
                InventoryTests.RunInventory(player);
                InventoryTests.RunDropItem(player, game.Session);
                InteractionTests.RunMiningAndPlacing(game, player, world);
                InventoryTests.RunToolMiningSpeed(player);
                InventoryTests.RunToolDurability(player);
                InventoryTests.RunSkillProgression(player);
                SaveTests.RunWorldSaveRoundTrip(game, player, world);
                SaveTests.RunPlayerStatisticsRoundTrip(game, player, world);
                SaveTests.RunCorruptSaveSelectedSlotClamped();
                SaveTests.RunSyncSaveFailureDoesNotThrow();
                SaveTests.RunLoadFailureForMissingSlot();
                AnimalCombatTests.RunAnimalGravity(world);
                AnimalCombatTests.RunAnimalWanderCollision(world);
                AnimalCombatTests.RunAnimalSpawnCap(world);
                AnimalCombatTests.RunPlayerTakeDamage(player);
                AnimalCombatTests.RunEntityRaycast(world);
                AnimalCombatTests.RunMeleeKillAnimal(game, player, world);
                SurvivalTests.RunHungerDrain(player);
                SurvivalTests.RunEatFood(player);
                SurvivalTests.RunAnimalLoot(player, game.Animals, world);
                SurvivalTests.RunNightSpawn(world, player, game.Animals);
                SurvivalTests.RunDeathPenalty(player);
                SurvivalTests.RunHungerSaveRoundTrip(game, player, world);
                PhysicsTests.RunFallDamage(game, player, world);
                InteractionTests.RunClickPriority(game, player, world);
                CraftingTests.RunSigilBenchActivation(game, world);
                CraftingTests.RunCruciblePlankRecipe(game, player, world);
                PhysicsTests.RunPassableBlocks(player, world);
                FluidTests.RunSwimThroughWater(player, world);
                FluidTests.RunDrowning(game, player, world);
                FluidTests.RunFallDamageInWater(game, player, world);
                FluidTests.RunFluidSpread(game, world);
                FluidTests.RunBucketPlaceAndPickup(game, player, world);
                FluidTests.RunFluidSaveRoundTrip(game, world);
                FluidTests.RunNoWalkOnWater(player, world);
                FluidTests.RunWaterFillsExcavatedGap(world);
                CraftingTests.RunNewCraftRecipes(game, player, world);
                CraftingTests.RunPlayerCraftGrid(game, player);
                CraftingTests.RunShapedToolBenchCraft(game, player, world);
                CraftingTests.RunSticksCrafting(game, player);
                CraftingTests.RunRecipeUnlockOnDiscovery(game, player);
                CraftingTests.RunRecipeBookToolResolve(player);
                CraftingTests.RunStorageInventory(player);
                VillageTests.RunStarterSettlementOnNewWorld(game);
                SurvivalTests.RunVillageRations(player, game.Session.Villages.GetPrimaryVillage()!);
                VillageTests.RunVillageFoundAndRecruit(game);
                VillageTests.RunVillageSaveRoundTripV6(game);
                VillageTests.RunVillageSaveRoundTripV7(game);
            }

            WorldGenTests.RunBiomeTreeSpecies();
            WorldGenTests.RunFloraPlacement();
            WorldGenTests.RunDesertPalmDensity();
            WorldGenTests.RunTreeShapeDiversity();
            WorldGenTests.RunTreeBlockBudget();
            WorldGenTests.RunBiomeFloraPresence();
            WorldGenTests.RunFloraMeshBuilderTileMapping();

            Console.WriteLine("\n==================================================================");
            Console.WriteLine("ALL TESTS PASSED SUCCESSFULLY! (EXIT CODE: 0)");
            Console.WriteLine("==================================================================\n");
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nTEST FAILURE: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            Console.WriteLine("\n==================================================================");
            Console.WriteLine("TEST SUITE FAILED! (EXIT CODE: 1)");
            Console.WriteLine("==================================================================\n");
            return false;
        }
    }
}
