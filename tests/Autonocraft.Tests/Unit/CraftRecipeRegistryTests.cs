using Autonocraft.Crafting;
using Autonocraft.Domain.Core;
using Autonocraft.Items;
using Autonocraft.World;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class CraftRecipeRegistryTests
{
    [Fact]
    public void AllContainsCoreBenchRecipes()
    {
        Assert.Contains(CraftRecipeRegistry.All, r => r.Id == "recipe:plank");
        Assert.Contains(CraftRecipeRegistry.All, r => r.Id == "recipe:birch_plank");
        Assert.Contains(CraftRecipeRegistry.All, r => r.Id == "recipe:cobblestone");
    }

    [Fact]
    public void ForStationBenchReturnsBenchRecipesOnly()
    {
        var benchRecipes = CraftRecipeRegistry.ForStation(BlockType.StationBench);
        Assert.NotEmpty(benchRecipes);
        Assert.All(benchRecipes, r => Assert.Equal(BlockType.StationBench, r.StationType));
    }

    [Fact]
    public void AvailableForStationIgnoresUnlockRequirements()
    {
        var journal = new DiscoveryJournal();
        var available = CraftRecipeRegistry.AvailableForStation(BlockType.StationBench, journal).ToList();
        Assert.Contains(available, r => r.Id == "recipe:plank");
        Assert.Contains(available, r => r.Id == "recipe:stone_pickaxe");

        journal.Unlock("recipe:stone_pickaxe");
        var afterUnlock = CraftRecipeRegistry.AvailableForStation(BlockType.StationBench, journal).ToList();
        Assert.Equal(available.Select(r => r.Id), afterUnlock.Select(r => r.Id));
    }

    [Fact]
    public void GoldBlockRecipeRequiresNightPhase()
    {
        var recipe = CraftRecipeRegistry.All.Single(r => r.Id == "recipe:gold_block");
        var nightEnv = new CraftEnvironment
        {
            Biome = BiomeType.Plains,
            TimePhase = TimePhase.Night,
            HasAdjacentHeat = true,
            HasFuelInInputs = true
        };
        var dayEnv = new CraftEnvironment
        {
            Biome = BiomeType.Plains,
            TimePhase = TimePhase.Day,
            HasAdjacentHeat = true,
            HasFuelInInputs = true
        };

        Assert.True(recipe.EnvironmentMatches(nightEnv));
        Assert.False(recipe.EnvironmentMatches(dayEnv));
    }
}
