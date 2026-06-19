using System;
using System.Numerics;
using System.Diagnostics;
using Autonocraft.Core;
using Autonocraft.Tests.Integration;

namespace Autonocraft.Tests;

public static class IntegrationTestRunner
{
    private static void RunTimed(string label, Action test)
    {
        var stopwatch = Stopwatch.StartNew();
        test();
        stopwatch.Stop();
        Console.WriteLine($"[Timing] {label}: {stopwatch.Elapsed.TotalSeconds:F2}s");
    }

    public static bool Run()
    {
        Console.WriteLine("\n==================================================================");
        Console.WriteLine("RUNNING AUTONOCRAFT AUTOMATED INTEGRATION TESTS");
        Console.WriteLine("==================================================================");

        using var host = new TestHost();

        try
        {
            RunTimed(nameof(WorldGenTests.RunGameSettingsRoundTrip), WorldGenTests.RunGameSettingsRoundTrip);
            RunTimed(nameof(VillageTests.RunVillageScreenInputLayout), VillageTests.RunVillageScreenInputLayout);
            RunTimed(nameof(VillageTests.RunBlueprintPlacementHelper), VillageTests.RunBlueprintPlacementHelper);
            RunTimed(nameof(VillageTests.RunCanPlaceBlueprint), VillageTests.RunCanPlaceBlueprint);
            RunTimed(nameof(VillageTests.RunQueuedBuildingSiteSurvivesSync), VillageTests.RunQueuedBuildingSiteSurvivesSync);
            RunTimed(nameof(VillageTests.RunStarterSettlementBeforeChunksLoaded), VillageTests.RunStarterSettlementBeforeChunksLoaded);
            RunTimed(nameof(VillageTests.RunLiveStyleJobAssignmentHasWorld), VillageTests.RunLiveStyleJobAssignmentHasWorld);
            RunTimed(nameof(VillageTests.RunFullVillageLifecycleJobs), VillageTests.RunFullVillageLifecycleJobs);
            RunTimed(nameof(VillageTests.RunAgentHttpVillageBridgeE2E), VillageTests.RunAgentHttpVillageBridgeE2E);
            RunTimed(nameof(WorldGenTests.RunChunkLodBands), WorldGenTests.RunChunkLodBands);
            RunTimed(nameof(WorldGenTests.RunChunkLodMeshCounts), WorldGenTests.RunChunkLodMeshCounts);
            RunTimed(nameof(WorldGenTests.RunOceanShellMeshSurfaces), WorldGenTests.RunOceanShellMeshSurfaces);
            RunTimed(nameof(ChunkStreamingTests.RunInitialLoadWaitsForInFlightGeneration), ChunkStreamingTests.RunInitialLoadWaitsForInFlightGeneration);
            RunTimed(nameof(ChunkStreamingTests.RunFaultedChunkGenerationDoesNotCrash), ChunkStreamingTests.RunFaultedChunkGenerationDoesNotCrash);
            RunTimed(nameof(ChunkStreamingTests.RunChunkUnloadDiscardsStaleInFlight), ChunkStreamingTests.RunChunkUnloadDiscardsStaleInFlight);
            RunTimed(nameof(ChunkStreamingTests.RunEnsureChunksLoadedDoesNotUnloadPlayerRadius), ChunkStreamingTests.RunEnsureChunksLoadedDoesNotUnloadPlayerRadius);
            RunTimed(nameof(WorldGenTests.RunChunkStreamingStability), WorldGenTests.RunChunkStreamingStability);
            RunTimed(nameof(WorldGenTests.RunWorldGenerationBasics), WorldGenTests.RunWorldGenerationBasics);
            RunTimed(nameof(WorldGenTests.RunOceanNoSurfaceIce), WorldGenTests.RunOceanNoSurfaceIce);
            RunTimed(nameof(WorldGenTests.RunNewSurfaceBiomes), WorldGenTests.RunNewSurfaceBiomes);
            RunTimed(nameof(WorldGenTests.RunCaveBiomes), WorldGenTests.RunCaveBiomes);
            RunTimed(nameof(TerrainSlabTests.RunTerrainSlabUnitRules), TerrainSlabTests.RunTerrainSlabUnitRules);
            RunTimed(nameof(TerrainSlabTests.RunTerrainSlabPlacementRules), TerrainSlabTests.RunTerrainSlabPlacementRules);
            RunTimed(nameof(TerrainSlabTests.RunTerrainSlabUpperStepRegression), TerrainSlabTests.RunTerrainSlabUpperStepRegression);
            RunTimed(nameof(TerrainSlabTests.RunGeneratedWorldHasNoMountainSlabs), TerrainSlabTests.RunGeneratedWorldHasNoMountainSlabs);
            RunTimed(nameof(WorldGenTests.RunStructureGeneration), WorldGenTests.RunStructureGeneration);
            RunTimed(nameof(WorldGenTests.RunStructureGallery), WorldGenTests.RunStructureGallery);
            RunTimed(nameof(WorldGenTests.RunStructureChestLoot), WorldGenTests.RunStructureChestLoot);
            RunTimed(nameof(WorldGenTests.RunImprovedBuildingCatalogQuality), WorldGenTests.RunImprovedBuildingCatalogQuality);
            RunTimed(nameof(WorldGenTests.RunImprovedBuildingInteriorQuality), WorldGenTests.RunImprovedBuildingInteriorQuality);
            RunTimed(nameof(WorldGenTests.RunImprovedBuildingGalleryReachability), WorldGenTests.RunImprovedBuildingGalleryReachability);
            RunTimed(nameof(WorldGenTests.RunImprovedBuildingFootprintSafety), WorldGenTests.RunImprovedBuildingFootprintSafety);

            RunTimed(nameof(VillageTests.RunInventoryStacking), VillageTests.RunInventoryStacking);
            RunTimed(nameof(VillageTests.RunBlockActionService), VillageTests.RunBlockActionService);
            RunTimed(nameof(VillageTests.RunClaimWorldStructure), VillageTests.RunClaimWorldStructure);
            RunTimed(nameof(VillageTests.RunImprovedClaimableStructureAccess), VillageTests.RunImprovedClaimableStructureAccess);
            RunTimed(nameof(VillageTests.RunFarmFoodProduction), VillageTests.RunFarmFoodProduction);
            RunTimed(nameof(VillageTests.RunBuildingJobWiring), VillageTests.RunBuildingJobWiring);
            RunTimed(nameof(VillageTests.RunVillagerToolMining), VillageTests.RunVillagerToolMining);
            RunTimed(nameof(VillageTests.RunVillageAiToolsMock), VillageTests.RunVillageAiToolsMock);
            RunTimed(nameof(VillageTests.RunVillageNumericGoals), VillageTests.RunVillageNumericGoals);
            RunTimed(nameof(VillageTests.RunPlayerWorkQueue), VillageTests.RunPlayerWorkQueue);
            RunTimed(nameof(VillageTests.RunRepairMissingCitizens), VillageTests.RunRepairMissingCitizens);
            RunTimed(nameof(VillageTests.RunVillagerLumberChopping), VillageTests.RunVillagerLumberChopping);
            RunTimed(nameof(VillageTests.RunAdoptOrphanedCitizens), VillageTests.RunAdoptOrphanedCitizens);
            RunTimed(nameof(VillageTests.RunRelinkStrandedCitizens), VillageTests.RunRelinkStrandedCitizens);
            RunTimed(nameof(VillageTests.RunVillageRegistryDesyncLiveChop), VillageTests.RunVillageRegistryDesyncLiveChop);
            RunTimed(nameof(VillageTests.RunVillageGuidanceHints), VillageTests.RunVillageGuidanceHints);
            RunTimed(nameof(VillageTests.RunSettlementGuidancePriority), VillageTests.RunSettlementGuidancePriority);
            RunTimed(nameof(VillageTests.RunSettlementDashboardFields), VillageTests.RunSettlementDashboardFields);
            RunTimed(nameof(VillageTests.RunJobAssignmentBlockedReasons), VillageTests.RunJobAssignmentBlockedReasons);
            RunTimed(nameof(VillageTests.RunVillagerActivityTextContext), VillageTests.RunVillagerActivityTextContext);
            RunTimed(nameof(VillageTests.RunRecruitPreviewBlockedReason), VillageTests.RunRecruitPreviewBlockedReason);
            RunTimed(nameof(VillageTests.RunSettlementWellBeingWarnings), VillageTests.RunSettlementWellBeingWarnings);
            RunTimed(nameof(VillageTests.RunPeopleTabCitizenDifferentiation), VillageTests.RunPeopleTabCitizenDifferentiation);
            RunTimed(nameof(VillageTests.RunAgentStateGuidanceParity), VillageTests.RunAgentStateGuidanceParity);
            RunTimed(nameof(VillageTests.RunVillageEventsNotifier), VillageTests.RunVillageEventsNotifier);
            RunTimed(nameof(VillageTests.RunStarvationConsequences), VillageTests.RunStarvationConsequences);
            RunTimed(nameof(VillageTests.RunVillageRename), VillageTests.RunVillageRename);

            using (var game = new AutonocraftGame(runTests: true))
            {
                var player = game.Player;
                var world = game.Grid;

                world.UpdateChunksAround(null, player.Position, 2);

                RunTimed(nameof(PhysicsTests.RunGravityAndCollision), () => PhysicsTests.RunGravityAndCollision(player, world));
                RunTimed(nameof(PhysicsTests.RunJumping), () => PhysicsTests.RunJumping(player, world));
                RunTimed(nameof(PhysicsTests.RunSlabStairWalking), () => PhysicsTests.RunSlabStairWalking(player, world));
                RunTimed(nameof(SurvivalStartTests.RunEmptySurvivalStart), SurvivalStartTests.RunEmptySurvivalStart);
                RunTimed(nameof(CraftingTests.RunRecipeBookShowsAllBenchRecipes), CraftingTests.RunRecipeBookShowsAllBenchRecipes);
                RunTimed(nameof(InventoryTests.RunInventory), () => InventoryTests.RunInventory(player));
                RunTimed(nameof(InventoryTests.RunDropItem), () => InventoryTests.RunDropItem(player, game.Session));
                RunTimed(nameof(InteractionTests.RunMiningAndPlacing), () => InteractionTests.RunMiningAndPlacing(game, player, world));
                RunTimed(nameof(InventoryTests.RunToolMiningSpeed), () => InventoryTests.RunToolMiningSpeed(player));
                RunTimed(nameof(InventoryTests.RunToolDurability), () => InventoryTests.RunToolDurability(player));
                RunTimed(nameof(InventoryTests.RunSkillProgression), () => InventoryTests.RunSkillProgression(player));
                RunTimed(nameof(SaveTests.RunWorldSaveRoundTrip), () => SaveTests.RunWorldSaveRoundTrip(game, player, world));
                RunTimed(nameof(SaveTests.RunImprovedBuildingSaveRoundTrip), () => SaveTests.RunImprovedBuildingSaveRoundTrip(game, player, world));
                RunTimed(nameof(SaveTests.RunPlayerStatisticsRoundTrip), () => SaveTests.RunPlayerStatisticsRoundTrip(game, player, world));
                RunTimed(nameof(SaveTests.RunCorruptSaveSelectedSlotClamped), SaveTests.RunCorruptSaveSelectedSlotClamped);
                RunTimed(nameof(SaveTests.RunSyncSaveFailureDoesNotThrow), SaveTests.RunSyncSaveFailureDoesNotThrow);
                RunTimed(nameof(SaveTests.RunLoadFailureForMissingSlot), SaveTests.RunLoadFailureForMissingSlot);
                RunTimed(nameof(AnimalCombatTests.RunAnimalGravity), () => AnimalCombatTests.RunAnimalGravity(world));
                RunTimed(nameof(AnimalCombatTests.RunAnimalWanderCollision), () => AnimalCombatTests.RunAnimalWanderCollision(world));
                RunTimed(nameof(AnimalCombatTests.RunAnimalSpawnCap), () => AnimalCombatTests.RunAnimalSpawnCap(world));
                RunTimed(nameof(AnimalCombatTests.RunPlayerTakeDamage), () => AnimalCombatTests.RunPlayerTakeDamage(player));
                RunTimed(nameof(AnimalCombatTests.RunEntityRaycast), () => AnimalCombatTests.RunEntityRaycast(world));
                RunTimed(nameof(AnimalCombatTests.RunMeleeKillAnimal), () => AnimalCombatTests.RunMeleeKillAnimal(game, player, world));
                RunTimed(nameof(SurvivalTests.RunHungerDrain), () => SurvivalTests.RunHungerDrain(player));
                RunTimed(nameof(SurvivalTests.RunEatFood), () => SurvivalTests.RunEatFood(player));
                RunTimed(nameof(SurvivalTests.RunAnimalLoot), () => SurvivalTests.RunAnimalLoot(player, game.Animals, world));
                RunTimed(nameof(SurvivalTests.RunNightSpawn), () => SurvivalTests.RunNightSpawn(world, player, game.Animals));
                RunTimed(nameof(SurvivalTests.RunDeathPenalty), () => SurvivalTests.RunDeathPenalty(player));
                RunTimed(nameof(SurvivalTests.RunHungerSaveRoundTrip), () => SurvivalTests.RunHungerSaveRoundTrip(game, player, world));
                RunTimed(nameof(PhysicsTests.RunFallDamage), () => PhysicsTests.RunFallDamage(game, player, world));
                RunTimed(nameof(InteractionTests.RunClickPriority), () => InteractionTests.RunClickPriority(game, player, world));
                RunTimed(nameof(CraftingTests.RunSigilBenchActivation), () => CraftingTests.RunSigilBenchActivation(game, world));
                RunTimed(nameof(CraftingTests.RunCruciblePlankRecipe), () => CraftingTests.RunCruciblePlankRecipe(game, player, world));
                RunTimed(nameof(PhysicsTests.RunPassableBlocks), () => PhysicsTests.RunPassableBlocks(player, world));
                RunTimed(nameof(FluidTests.RunSwimThroughWater), () => FluidTests.RunSwimThroughWater(player, world));
                RunTimed(nameof(FluidTests.RunDrowning), () => FluidTests.RunDrowning(game, player, world));
                RunTimed(nameof(FluidTests.RunFallDamageInWater), () => FluidTests.RunFallDamageInWater(game, player, world));
                RunTimed(nameof(FluidTests.RunFluidSpread), () => FluidTests.RunFluidSpread(game, world));
                RunTimed(nameof(FluidTests.RunBucketPlaceAndPickup), () => FluidTests.RunBucketPlaceAndPickup(game, player, world));
                RunTimed(nameof(FluidTests.RunFluidSaveRoundTrip), () => FluidTests.RunFluidSaveRoundTrip(game, world));
                RunTimed(nameof(FluidTests.RunNoWalkOnWater), () => FluidTests.RunNoWalkOnWater(player, world));
                RunTimed(nameof(FluidTests.RunWaterFillsExcavatedGap), () => FluidTests.RunWaterFillsExcavatedGap(world));
                RunTimed(nameof(CraftingTests.RunNewCraftRecipes), () => CraftingTests.RunNewCraftRecipes(game, player, world));
                RunTimed(nameof(CraftingTests.RunPlayerCraftGrid), () => CraftingTests.RunPlayerCraftGrid(game, player));
                RunTimed(nameof(CraftingTests.RunShapedToolBenchCraft), () => CraftingTests.RunShapedToolBenchCraft(game, player, world));
                RunTimed(nameof(CraftingTests.RunSticksCrafting), () => CraftingTests.RunSticksCrafting(game, player));
                RunTimed(nameof(CraftingTests.RunRecipeUnlockOnDiscovery), () => CraftingTests.RunRecipeUnlockOnDiscovery(game, player));
                RunTimed(nameof(CraftingTests.RunRecipeBookToolResolve), () => CraftingTests.RunRecipeBookToolResolve(player));
                RunTimed(nameof(CraftingTests.RunRecipeBookNoHiddenNames), () => CraftingTests.RunRecipeBookNoHiddenNames(player));
                RunTimed(nameof(CraftingTests.RunRecipeBookCraftabilityRefresh), () => CraftingTests.RunRecipeBookCraftabilityRefresh(player));
                RunTimed(nameof(CraftingTests.RunRecipeBookIngredientSummary), () => CraftingTests.RunRecipeBookIngredientSummary(player));
                RunTimed(nameof(SurvivalStartTests.RunBareHandLogProgression), () => SurvivalStartTests.RunBareHandLogProgression(game, player, world));
                RunTimed(nameof(SurvivalStartTests.RunEarlyGuideEmptyInventoryHints), SurvivalStartTests.RunEarlyGuideEmptyInventoryHints);
                RunTimed(nameof(SurvivalStartTests.RunSurvivalMilestoneSaveRoundTrip), () => SurvivalStartTests.RunSurvivalMilestoneSaveRoundTrip(game, player));
                RunTimed(nameof(SurvivalStartTests.RunRespawnNoStarterLoot), () => SurvivalStartTests.RunRespawnNoStarterLoot(player));
                RunTimed(nameof(CraftingTests.RunStorageInventory), () => CraftingTests.RunStorageInventory(player));
                RunTimed(nameof(VillageTests.RunStarterSettlementOnNewWorld), () => VillageTests.RunStarterSettlementOnNewWorld(game));
                RunTimed(nameof(SurvivalTests.RunVillageRations), () => SurvivalTests.RunVillageRations(player, game.Session.Villages.GetPrimaryVillage()!));
                RunTimed(nameof(VillageTests.RunVillageFoundAndRecruit), () => VillageTests.RunVillageFoundAndRecruit(game));
                RunTimed(nameof(VillageTests.RunVillageSaveRoundTripV6), () => VillageTests.RunVillageSaveRoundTripV6(game));
                RunTimed(nameof(VillageTests.RunVillageSaveRoundTripV7), () => VillageTests.RunVillageSaveRoundTripV7(game));
            }

            RunTimed(nameof(WorldGenTests.RunBiomeTreeSpecies), WorldGenTests.RunBiomeTreeSpecies);
            RunTimed(nameof(WorldGenTests.RunFloraPlacement), WorldGenTests.RunFloraPlacement);
            RunTimed(nameof(WorldGenTests.RunDesertPalmDensity), WorldGenTests.RunDesertPalmDensity);
            RunTimed(nameof(WorldGenTests.RunTreeShapeDiversity), WorldGenTests.RunTreeShapeDiversity);
            RunTimed(nameof(WorldGenTests.RunTreeBlockBudget), WorldGenTests.RunTreeBlockBudget);
            RunTimed(nameof(WorldGenTests.RunBiomeFloraPresence), WorldGenTests.RunBiomeFloraPresence);
            RunTimed(nameof(WorldGenTests.RunFloraMeshBuilderTileMapping), WorldGenTests.RunFloraMeshBuilderTileMapping);

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
