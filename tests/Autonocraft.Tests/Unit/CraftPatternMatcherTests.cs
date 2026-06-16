using Autonocraft.Crafting;
using Autonocraft.Items;
using Autonocraft.World;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class CraftPatternMatcherTests
{
    [Fact]
    public void WoodPickaxePatternMatchesValidGrid()
    {
        var slots = new ItemStack[9];
        slots[0] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        slots[1] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        slots[2] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        slots[4] = ItemStack.CreateMaterial(ItemId.Stick, 1);
        slots[7] = ItemStack.CreateMaterial(ItemId.Stick, 1);

        bool matched = CraftPatternMatcher.TryMatch(
            new[] { "PPP", " T ", " T " },
            3,
            slots,
            out var consumption);

        Assert.True(matched);
        Assert.Equal(5, consumption.Count);
        Assert.Equal(1, consumption[0]);
        Assert.Equal(1, consumption[7]);
    }

    [Fact]
    public void WoodPickaxePatternRejectsMisplacedIngredients()
    {
        var slots = new ItemStack[9];
        slots[0] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        slots[1] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        slots[2] = ItemStack.CreateBlock(BlockType.OakLog, 1);
        slots[4] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        slots[7] = ItemStack.CreateBlock(BlockType.OakLog, 1);

        bool matched = CraftPatternMatcher.TryMatch(
            new[] { "PPP", " T ", " T " },
            3,
            slots,
            out _);

        Assert.False(matched);
    }

    [Fact]
    public void ShapelessPlankMatchesSingleLogInTwoByTwo()
    {
        var recipe = CraftRecipeRegistry.All.Single(r => r.Id == "recipe:plank");
        var slots = new BlockType[4];
        slots[1] = BlockType.OakLog;

        Assert.True(recipe.TryMatchGrid(slots, 2, out var consumption));
        Assert.Single(consumption);
        Assert.Equal(1, consumption[1]);
    }
}
