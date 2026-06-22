using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autonocraft.Core;
using Autonocraft.Tests.Integration;

namespace Autonocraft.Tests;

public static class IntegrationTestRunner
{
    private const string ShardIndexEnv = "AUTONOCRAFT_TEST_SHARD_INDEX";
    private const string ShardCountEnv = "AUTONOCRAFT_TEST_SHARD_COUNT";
    private const string IntegrationNamespace = "Autonocraft.Tests.Integration";

    private sealed record IntegrationCase(string ClassName, string MethodName, Action Execute)
    {
        public string DisplayName => $"{ClassName}.{MethodName}";
    }

    private sealed record IntegrationClass(string ClassName, IReadOnlyList<IntegrationCase> Cases)
    {
        public int Weight => Cases.Count;
    }

    private static void RunTimed(string label, Action test)
    {
        var stopwatch = Stopwatch.StartNew();
        test();
        stopwatch.Stop();
        Console.WriteLine($"[Timing] {label}: {stopwatch.Elapsed.TotalSeconds:F2}s");
    }

    private static (int ShardIndex, int ShardCount) GetShardConfig()
    {
        var shardCount = 1;
        var shardIndex = 0;

        if (int.TryParse(Environment.GetEnvironmentVariable(ShardCountEnv), out var parsedShardCount) && parsedShardCount > 0)
        {
            shardCount = parsedShardCount;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable(ShardIndexEnv), out var parsedShardIndex))
        {
            shardIndex = parsedShardIndex;
        }

        if (shardIndex < 0 || shardIndex >= shardCount)
        {
            throw new InvalidOperationException(
                $"Invalid integration test shard {shardIndex}/{shardCount}. Expected 0 <= shard_index < shard_count.");
        }

        return (shardIndex, shardCount);
    }

    private static IReadOnlyList<IntegrationClass> DiscoverIntegrationClasses()
    {
        var assembly = typeof(IntegrationTestRunner).Assembly;
        var classes = new List<IntegrationClass>();

        foreach (var type in assembly.GetTypes()
                     .Where(type => type.IsClass
                         && type.IsAbstract
                         && type.IsSealed
                         && type.Namespace == IntegrationNamespace)
                     .OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            var cases = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method =>
                    method.ReturnType == typeof(void)
                    && method.GetParameters().Length == 0
                    && method.Name.StartsWith("Run", StringComparison.Ordinal))
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .Select(method => new IntegrationCase(
                    type.Name,
                    method.Name,
                    () => _ = method.Invoke(null, null)))
                .ToList();

            if (cases.Count > 0)
            {
                classes.Add(new IntegrationClass(type.Name, cases));
            }
        }

        if (classes.Count == 0)
        {
            throw new InvalidOperationException("No integration tests were discovered.");
        }

        return classes;
    }

    private static IReadOnlyList<IntegrationClass> SelectShard(IReadOnlyList<IntegrationClass> classes, int shardIndex, int shardCount)
    {
        if (shardCount > classes.Count)
        {
            throw new InvalidOperationException(
                $"Integration test shard count {shardCount} exceeds discovered class count {classes.Count}.");
        }

        var buckets = new List<IntegrationClass>[shardCount];
        var totals = new int[shardCount];

        for (var i = 0; i < shardCount; i++)
        {
            buckets[i] = [];
        }

        foreach (var integrationClass in classes
                     .OrderByDescending(integrationClass => integrationClass.Weight)
                     .ThenBy(integrationClass => integrationClass.ClassName, StringComparer.Ordinal))
        {
            var targetIndex = 0;
            for (var i = 1; i < shardCount; i++)
            {
                if (totals[i] < totals[targetIndex])
                {
                    targetIndex = i;
                }
            }

            buckets[targetIndex].Add(integrationClass);
            totals[targetIndex] += integrationClass.Weight;
        }

        return buckets[shardIndex]
            .OrderBy(integrationClass => integrationClass.ClassName, StringComparer.Ordinal)
            .ToList();
    }

    public static bool Run()
    {
        var (shardIndex, shardCount) = GetShardConfig();
        var classes = DiscoverIntegrationClasses();
        var selectedClasses = shardCount == 1 ? classes : SelectShard(classes, shardIndex, shardCount);
        var selectedCases = selectedClasses.SelectMany(@class => @class.Cases).ToList();

        if (selectedCases.Count == 0)
        {
            throw new InvalidOperationException(
                $"Integration shard {shardIndex + 1}/{shardCount} did not receive any tests.");
        }

        Console.WriteLine("\n==================================================================");
        Console.WriteLine("RUNNING AUTONOCRAFT AUTOMATED INTEGRATION TESTS");
        Console.WriteLine("==================================================================");
        Console.WriteLine($"Shard {shardIndex + 1}/{shardCount}: {selectedClasses.Count} classes, {selectedCases.Count} tests");

        using var host = new TestHost();

        try
        {
            foreach (var integrationClass in selectedClasses)
            {
                foreach (var testCase in integrationClass.Cases)
                {
                    RunTimed(testCase.DisplayName, testCase.Execute);
                }
            }

            using (var game = new AutonocraftGame(runTests: true))
            {
                var player = game.Player;
                var world = game.Grid;

                world.UpdateChunksAround(null, player.Position, 2);

                RunTimed(nameof(PhysicsTests.RunGravityAndCollision), () => PhysicsTests.RunGravityAndCollision(player, world));
                RunTimed(nameof(PhysicsTests.RunJumping), () => PhysicsTests.RunJumping(player, world));
                RunTimed(nameof(PhysicsTests.RunSlabStairWalking), () => PhysicsTests.RunSlabStairWalking(player, world));
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
                RunTimed(nameof(SurvivalTests.RunNewPlayerStartsWithoutStarterItems), SurvivalTests.RunNewPlayerStartsWithoutStarterItems);
                RunTimed(nameof(SurvivalTests.RunHungerDrain), () => SurvivalTests.RunHungerDrain(player));
                RunTimed(nameof(SurvivalTests.RunEatFood), () => SurvivalTests.RunEatFood(player));
                RunTimed(nameof(SurvivalTests.RunAnimalLoot), () => SurvivalTests.RunAnimalLoot(player, game.Animals, world));
                RunTimed(nameof(SurvivalTests.RunNightSpawn), () => SurvivalTests.RunNightSpawn(world, player, game.Animals));
                RunTimed(nameof(SurvivalTests.RunDeathPenalty), () => SurvivalTests.RunDeathPenalty(player));
                RunTimed(nameof(SurvivalTests.RunHungerSaveRoundTrip), () => SurvivalTests.RunHungerSaveRoundTrip(game, player, world));
                RunTimed(nameof(PhysicsTests.RunFallDamage), () => PhysicsTests.RunFallDamage(game, player, world));
                RunTimed(nameof(InteractionTests.RunClickPriority), () => InteractionTests.RunClickPriority(game, player, world));
                RunTimed(nameof(InteractionTests.RunLeafDecay), () => InteractionTests.RunLeafDecay(game, player, world));
                RunTimed(nameof(InteractionTests.RunSaplingGrowth), () => InteractionTests.RunSaplingGrowth(game, player, world));
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
                RunTimed(nameof(CraftingTests.RunStorageInventory), () => CraftingTests.RunStorageInventory(player));
                RunTimed(nameof(VillageTests.RunStarterSettlementOnNewWorld), () => VillageTests.RunStarterSettlementOnNewWorld(game));
                RunTimed(nameof(SurvivalTests.RunVillageRations), () => SurvivalTests.RunVillageRations(player, game.Session.Villages.GetPrimaryVillage()!));
                RunTimed(nameof(VillageTests.RunVillageFoundAndRecruit), () => VillageTests.RunVillageFoundAndRecruit(game));
                RunTimed(nameof(VillageTests.RunVillageSaveRoundTripV6), () => VillageTests.RunVillageSaveRoundTripV6(game));
                RunTimed(nameof(VillageTests.RunVillageSaveRoundTripV7), () => VillageTests.RunVillageSaveRoundTripV7(game));
                RunTimed(nameof(SnowSystemTests.RunSnowAccumulationAndMeltingTests), () => SnowSystemTests.RunSnowAccumulationAndMeltingTests(game.Session, player, world));
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
