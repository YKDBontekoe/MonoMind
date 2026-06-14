using Autonocraft.Crafting;
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
    public void AvailableForStationRespectsUnlockRequirements()
    {
        var journal = new DiscoveryJournal();
        var available = CraftRecipeRegistry.AvailableForStation(BlockType.StationBench, journal).ToList();
        Assert.Contains(available, r => r.Id == "recipe:plank");
        Assert.DoesNotContain(available, r => r.Id == "recipe:stone_pickaxe");

        journal.Unlock("recipe:stone_pickaxe");
        available = CraftRecipeRegistry.AvailableForStation(BlockType.StationBench, journal).ToList();
        Assert.Contains(available, r => r.Id == "recipe:stone_pickaxe");
    }

    [Fact]
    public void PlankRecipeProducesTwoPlanksFromOneLog()
    {
        var plank = CraftRecipeRegistry.All.Single(r => r.Id == "recipe:plank");
        Assert.Equal(BlockType.OakLog, plank.Inputs[0].ExactBlock);
        Assert.Equal(BlockType.OakPlank, plank.Output);
        Assert.Equal(2, plank.OutputCount);
    }
}
