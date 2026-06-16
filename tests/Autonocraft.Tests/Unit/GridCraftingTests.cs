using Autonocraft.Crafting;
using Autonocraft.Items;
using Autonocraft.World;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class GridCraftingTests
{
    [Fact]
    public void BenchGridFindsWoodPickaxeRecipe()
    {
        var journal = new DiscoveryJournal();
        journal.Unlock("recipe:wood_pickaxe");

        var slots = new ItemStack[9];
        slots[0] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        slots[1] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        slots[2] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        slots[4] = ItemStack.CreateMaterial(ItemId.Stick, 1);
        slots[7] = ItemStack.CreateMaterial(ItemId.Stick, 1);

        var recipe = GridCrafting.FindMatch(
            slots,
            CraftGridSize.ThreeByThree,
            BlockType.StationBench,
            journal);

        Assert.NotNull(recipe);
        Assert.Equal("recipe:wood_pickaxe", recipe!.Id);
    }

    [Fact]
    public void PreviewReturnsWoodPickaxeStack()
    {
        var journal = new DiscoveryJournal();
        var grid = new CraftingGrid();
        grid.SetSize(CraftGridSize.ThreeByThree);
        grid.SetSlot(0, ItemStack.CreateBlock(BlockType.OakPlank, 1));
        grid.SetSlot(1, ItemStack.CreateBlock(BlockType.OakPlank, 1));
        grid.SetSlot(2, ItemStack.CreateBlock(BlockType.OakPlank, 1));
        grid.SetSlot(4, ItemStack.CreateMaterial(ItemId.Stick, 1));
        grid.SetSlot(7, ItemStack.CreateMaterial(ItemId.Stick, 1));

        var preview = GridCrafting.Preview(grid, BlockType.StationBench, journal);
        Assert.True(preview.HasMatch);
        Assert.True(preview.Result.IsTool());
        Assert.Equal(ItemId.WoodPickaxe, preview.Result.ToolId);
    }

    [Fact]
    public void SticksRecipeMatchesTwoPlanks()
    {
        var journal = new DiscoveryJournal();
        var grid = new CraftingGrid();
        grid.SetSize(CraftGridSize.TwoByTwo);
        grid.SetSlot(0, ItemStack.CreateBlock(BlockType.OakPlank, 1));
        grid.SetSlot(2, ItemStack.CreateBlock(BlockType.OakPlank, 1));

        var preview = GridCrafting.Preview(grid, BlockType.StationBench, journal);
        Assert.True(preview.HasMatch);
        Assert.True(preview.Result.IsMaterial());
        Assert.Equal(ItemId.Stick, preview.Result.MaterialId);
        Assert.Equal(4, preview.Result.Count);
    }
}
