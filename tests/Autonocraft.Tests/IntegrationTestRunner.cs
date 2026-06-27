using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autonocraft.Core;
using Autonocraft.Tests.Integration;
using Autonocraft.World;

namespace Autonocraft.Tests;

public static class IntegrationTestRunner
{
    private const string ShardIndexEnv = "AUTONOCRAFT_TEST_SHARD_INDEX";
    private const string ShardCountEnv = "AUTONOCRAFT_TEST_SHARD_COUNT";
    private const string IntegrationNamespace = "Autonocraft.Tests.Integration";

    private sealed record IntegrationCase(string SuiteName, string MethodName, int Weight, bool RequiresGame, Action<IntegrationContext?> Execute)
    {
        public string DisplayName => $"{SuiteName}.{MethodName}";
    }

    private sealed record IntegrationSuite(string SuiteName, IReadOnlyList<IntegrationCase> Cases)
    {
        public int Weight => Cases.Sum(testCase => testCase.Weight);
    }

    private sealed class IntegrationContext : IDisposable
    {
        private AutonocraftGame? _game;
        private bool _chunksLoaded;

        public AutonocraftGame Game => _game ??= new AutonocraftGame(runTests: true);
        public Player Player => Game.Player;
        public VoxelWorld World => Game.Grid;

        public void EnsureWorldLoaded()
        {
            if (_chunksLoaded)
            {
                return;
            }

            World.UpdateChunksAround(null, Player.Position, 2);
            _chunksLoaded = true;
        }

        public void Reset()
        {
            if (_game == null)
            {
                return;
            }

            _game.Session.ReplaceWorld(WorldConstants.DefaultSeed, null);
            _game.Session.ResetPlayer();
            _game.Session.ResetCrafting();
            _chunksLoaded = false;
        }

        public void Dispose() => _game?.Dispose();
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

    private static IReadOnlyList<IntegrationSuite> DiscoverIntegrationSuites()
    {
        var assembly = typeof(IntegrationTestRunner).Assembly;
        var suites = new List<IntegrationSuite>();

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
                    Weight: 1,
                    RequiresGame: false,
                    _ => method.Invoke(null, null)))
                .ToList();

            if (cases.Count > 0)
            {
                suites.Add(new IntegrationSuite(type.Name, cases));
            }
        }

        suites.Add(BuildGameBackedSuite());

        if (suites.Count == 0)
        {
            throw new InvalidOperationException("No integration tests were discovered.");
        }

        return suites;
    }

    private static IntegrationSuite BuildGameBackedSuite()
    {
        var cases = new List<IntegrationCase>
        {
            GameCase(nameof(PhysicsTests), nameof(PhysicsTests.RunGravityAndCollision), context => PhysicsTests.RunGravityAndCollision(context.Player, context.World)),
            GameCase(nameof(PhysicsTests), nameof(PhysicsTests.RunJumping), context => PhysicsTests.RunJumping(context.Player, context.World)),
            GameCase(nameof(PhysicsTests), nameof(PhysicsTests.RunSlabStairWalking), context => PhysicsTests.RunSlabStairWalking(context.Player, context.World)),
            GameCase(nameof(PhysicsTests), nameof(PhysicsTests.RunFallDamage), context => PhysicsTests.RunFallDamage(context.Game, context.Player, context.World)),
            GameCase(nameof(PhysicsTests), nameof(PhysicsTests.RunPassableBlocks), context => PhysicsTests.RunPassableBlocks(context.Player, context.World)),

            GameCase(nameof(InventoryTests), nameof(InventoryTests.RunInventory), context => InventoryTests.RunInventory(context.Player)),
            GameCase(nameof(InventoryTests), nameof(InventoryTests.RunDropItem), context => InventoryTests.RunDropItem(context.Player, context.Game.Session)),
            GameCase(nameof(InventoryTests), nameof(InventoryTests.RunToolMiningSpeed), context => InventoryTests.RunToolMiningSpeed(context.Player)),
            GameCase(nameof(InventoryTests), nameof(InventoryTests.RunToolDurability), context => InventoryTests.RunToolDurability(context.Player)),
            GameCase(nameof(InventoryTests), nameof(InventoryTests.RunSkillProgression), context => InventoryTests.RunSkillProgression(context.Player)),

            GameCase(nameof(InteractionTests), nameof(InteractionTests.RunMiningAndPlacing), context => InteractionTests.RunMiningAndPlacing(context.Game, context.Player, context.World)),
            GameCase(nameof(InteractionTests), nameof(InteractionTests.RunClickPriority), context => InteractionTests.RunClickPriority(context.Game, context.Player, context.World)),
            GameCase(nameof(InteractionTests), nameof(InteractionTests.RunSwordMissDoesNotMineBlock), context => InteractionTests.RunSwordMissDoesNotMineBlock(context.Game, context.Player, context.World)),
            GameCase(nameof(InteractionTests), nameof(InteractionTests.RunLeafDecay), context => InteractionTests.RunLeafDecay(context.Game, context.Player, context.World)),
            GameCase(nameof(InteractionTests), nameof(InteractionTests.RunSaplingGrowth), context => InteractionTests.RunSaplingGrowth(context.Game, context.Player, context.World)),

            GameCase(nameof(SaveTests), nameof(SaveTests.RunWorldSaveRoundTrip), context => SaveTests.RunWorldSaveRoundTrip(context.Game, context.Player, context.World)),
            GameCase(nameof(SaveTests), nameof(SaveTests.RunImprovedBuildingSaveRoundTrip), context => SaveTests.RunImprovedBuildingSaveRoundTrip(context.Game, context.Player, context.World)),
            GameCase(nameof(SaveTests), nameof(SaveTests.RunPlayerStatisticsRoundTrip), context => SaveTests.RunPlayerStatisticsRoundTrip(context.Game, context.Player, context.World)),

            GameCase(nameof(AnimalCombatTests), nameof(AnimalCombatTests.RunAnimalGravity), context => AnimalCombatTests.RunAnimalGravity(context.World)),
            GameCase(nameof(AnimalCombatTests), nameof(AnimalCombatTests.RunAnimalWanderCollision), context => AnimalCombatTests.RunAnimalWanderCollision(context.World)),
            GameCase(nameof(AnimalCombatTests), nameof(AnimalCombatTests.RunAnimalSpawnCap), context => AnimalCombatTests.RunAnimalSpawnCap(context.World)),
            GameCase(nameof(AnimalCombatTests), nameof(AnimalCombatTests.RunPlayerTakeDamage), context => AnimalCombatTests.RunPlayerTakeDamage(context.Player)),
            GameCase(nameof(AnimalCombatTests), nameof(AnimalCombatTests.RunEntityRaycast), context => AnimalCombatTests.RunEntityRaycast(context.World)),
            GameCase(nameof(AnimalCombatTests), nameof(AnimalCombatTests.RunMeleeKillAnimal), context => AnimalCombatTests.RunMeleeKillAnimal(context.Game, context.Player, context.World)),

            GameCase(nameof(SurvivalTests), nameof(SurvivalTests.RunHungerDrain), context => SurvivalTests.RunHungerDrain(context.Player)),
            GameCase(nameof(SurvivalTests), nameof(SurvivalTests.RunEatFood), context => SurvivalTests.RunEatFood(context.Player)),
            GameCase(nameof(SurvivalTests), nameof(SurvivalTests.RunAnimalLoot), context => SurvivalTests.RunAnimalLoot(context.Player, context.Game.Animals, context.World)),
            GameCase(nameof(SurvivalTests), nameof(SurvivalTests.RunNightSpawn), context => SurvivalTests.RunNightSpawn(context.World, context.Player, context.Game.Animals)),
            GameCase(nameof(SurvivalTests), nameof(SurvivalTests.RunDeathPenalty), context => SurvivalTests.RunDeathPenalty(context.Player)),
            GameCase(nameof(SurvivalTests), nameof(SurvivalTests.RunHungerSaveRoundTrip), context => SurvivalTests.RunHungerSaveRoundTrip(context.Game, context.Player, context.World)),
            GameCase(nameof(SurvivalTests), nameof(SurvivalTests.RunVillageRations), context => SurvivalTests.RunVillageRations(context.Player, context.Game.Session.Villages.GetPrimaryVillage())),

            GameCase(nameof(CraftingTests), nameof(CraftingTests.RunSigilBenchActivation), context => CraftingTests.RunSigilBenchActivation(context.Game, context.World)),
            GameCase(nameof(CraftingTests), nameof(CraftingTests.RunCruciblePlankRecipe), context => CraftingTests.RunCruciblePlankRecipe(context.Game, context.Player, context.World)),
            GameCase(nameof(CraftingTests), nameof(CraftingTests.RunNewCraftRecipes), context => CraftingTests.RunNewCraftRecipes(context.Game, context.Player, context.World)),
            GameCase(nameof(CraftingTests), nameof(CraftingTests.RunPlayerCraftGrid), context => CraftingTests.RunPlayerCraftGrid(context.Game, context.Player)),
            GameCase(nameof(CraftingTests), nameof(CraftingTests.RunShapedToolBenchCraft), context => CraftingTests.RunShapedToolBenchCraft(context.Game, context.Player, context.World)),
            GameCase(nameof(CraftingTests), nameof(CraftingTests.RunSticksCrafting), context => CraftingTests.RunSticksCrafting(context.Game, context.Player)),
            GameCase(nameof(CraftingTests), nameof(CraftingTests.RunRecipeUnlockOnDiscovery), context => CraftingTests.RunRecipeUnlockOnDiscovery(context.Game, context.Player)),
            GameCase(nameof(CraftingTests), nameof(CraftingTests.RunRecipeBookToolResolve), context => CraftingTests.RunRecipeBookToolResolve(context.Player)),
            GameCase(nameof(CraftingTests), nameof(CraftingTests.RunStorageInventory), context => CraftingTests.RunStorageInventory(context.Player)),

            GameCase(nameof(FluidTests), nameof(FluidTests.RunSwimThroughWater), context => FluidTests.RunSwimThroughWater(context.Player, context.World)),
            GameCase(nameof(FluidTests), nameof(FluidTests.RunDrowning), context => FluidTests.RunDrowning(context.Game, context.Player, context.World)),
            GameCase(nameof(FluidTests), nameof(FluidTests.RunFallDamageInWater), context => FluidTests.RunFallDamageInWater(context.Game, context.Player, context.World)),
            GameCase(nameof(FluidTests), nameof(FluidTests.RunFluidSpread), context => FluidTests.RunFluidSpread(context.Game, context.World)),
            GameCase(nameof(FluidTests), nameof(FluidTests.RunBucketPlaceAndPickup), context => FluidTests.RunBucketPlaceAndPickup(context.Game, context.Player, context.World)),
            GameCase(nameof(FluidTests), nameof(FluidTests.RunFluidSaveRoundTrip), context => FluidTests.RunFluidSaveRoundTrip(context.Game, context.World)),
            GameCase(nameof(FluidTests), nameof(FluidTests.RunNoWalkOnWater), context => FluidTests.RunNoWalkOnWater(context.Player, context.World)),
            GameCase(nameof(FluidTests), nameof(FluidTests.RunWaterFillsExcavatedGap), context => FluidTests.RunWaterFillsExcavatedGap(context.World)),

            GameCase(nameof(VillageTests), nameof(VillageTests.RunStarterSettlementOnNewWorld), context => VillageTests.RunStarterSettlementOnNewWorld(context.Game)),
            GameCase(nameof(VillageTests), nameof(VillageTests.RunVillageFoundAndRecruit), context => VillageTests.RunVillageFoundAndRecruit(context.Game)),
            GameCase(nameof(VillageTests), nameof(VillageTests.RunVillageSaveRoundTripV6), context => VillageTests.RunVillageSaveRoundTripV6(context.Game)),
            GameCase(nameof(VillageTests), nameof(VillageTests.RunVillageSaveRoundTripV7), context => VillageTests.RunVillageSaveRoundTripV7(context.Game)),

            GameCase(nameof(SnowSystemTests), nameof(SnowSystemTests.RunSnowAccumulationAndMeltingTests), context => SnowSystemTests.RunSnowAccumulationAndMeltingTests(context.Game.Session, context.Player, context.World)),
        };

        return new IntegrationSuite("GameBackedTests", cases);
    }

    private static IntegrationCase GameCase(string suiteName, string methodName, Action<IntegrationContext> execute)
    {
        return new IntegrationCase(
            suiteName,
            methodName,
            Weight: 2,
            RequiresGame: true,
            context =>
            {
                ArgumentNullException.ThrowIfNull(context);
                context.EnsureWorldLoaded();
                execute(context);
            });
    }

    private static IReadOnlyList<IntegrationCase> SelectShard(IReadOnlyList<IntegrationSuite> suites, int shardIndex, int shardCount)
    {
        var cases = suites.SelectMany(suite => suite.Cases).ToList();

        if (shardCount > cases.Count)
        {
            throw new InvalidOperationException(
                $"Integration test shard count {shardCount} exceeds discovered test count {cases.Count}.");
        }

        var buckets = new List<IntegrationCase>[shardCount];
        var totals = new int[shardCount];

        for (var i = 0; i < shardCount; i++)
        {
            buckets[i] = [];
        }

        foreach (var testCase in cases
                     .OrderByDescending(testCase => testCase.Weight)
                     .ThenBy(testCase => testCase.DisplayName, StringComparer.Ordinal))
        {
            var targetIndex = 0;
            for (var i = 1; i < shardCount; i++)
            {
                if (totals[i] < totals[targetIndex])
                {
                    targetIndex = i;
                }
            }

            buckets[targetIndex].Add(testCase);
            totals[targetIndex] += testCase.Weight;
        }

        return buckets[shardIndex]
            .OrderBy(testCase => testCase.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    public static bool Run()
    {
        var (shardIndex, shardCount) = GetShardConfig();
        var suites = DiscoverIntegrationSuites();
        var selectedCases = shardCount == 1
            ? suites.SelectMany(suite => suite.Cases).OrderBy(testCase => testCase.DisplayName, StringComparer.Ordinal).ToList()
            : SelectShard(suites, shardIndex, shardCount).ToList();

        if (selectedCases.Count == 0)
        {
            throw new InvalidOperationException(
                $"Integration shard {shardIndex + 1}/{shardCount} did not receive any tests.");
        }

        Console.WriteLine("\n==================================================================");
        Console.WriteLine("RUNNING AUTONOCRAFT AUTOMATED INTEGRATION TESTS");
        Console.WriteLine("==================================================================");
        Console.WriteLine($"Shard {shardIndex + 1}/{shardCount}: {selectedCases.Count} tests");
        foreach (var suite in selectedCases.GroupBy(testCase => testCase.SuiteName).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  - {suite.Key}: {suite.Count()} tests");
        }

        using var host = new TestHost();
        using var sharedContext = new IntegrationContext();

        try
        {
            foreach (var testCase in selectedCases)
            {
                RunTimed(testCase.DisplayName, () =>
                {
                    if (!testCase.RequiresGame)
                    {
                        testCase.Execute(null);
                        return;
                    }

                    sharedContext.Reset();
                    testCase.Execute(sharedContext);
                });
            }

            Console.WriteLine("\n==================================================================");
            Console.WriteLine("ALL TESTS PASSED SUCCESSFULLY! (EXIT CODE: 0)");
            Console.WriteLine("==================================================================\n");
            return true;
        }
        catch (Exception ex)
        {
            var realEx = ex.InnerException ?? ex;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nTEST FAILURE: {realEx.Message}");
            Console.WriteLine(realEx.StackTrace);
            Console.ResetColor();
            Console.WriteLine("\n==================================================================");
            Console.WriteLine("TEST SUITE FAILED! (EXIT CODE: 1)");
            Console.WriteLine("==================================================================\n");
            return false;
        }
    }
}
