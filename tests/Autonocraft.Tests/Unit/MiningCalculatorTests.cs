using Autonocraft.Items;
using Autonocraft.World;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class MiningCalculatorTests
{
    [Fact]
    public void WoodPickaxeMinesStoneFasterThanBareHands()
    {
        var skills = new PlayerSkills();
        float bareHands = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, ItemStack.Empty, skills);
        var pickaxe = ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood);
        float withPickaxe = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, pickaxe, skills);

        Assert.True(withPickaxe < bareHands);
    }

    [Fact]
    public void WrongToolAppliesHardBlockPenaltyForStone()
    {
        var skills = new PlayerSkills();
        var pickaxe = ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood);
        var axe = ToolRegistry.CreateStack(ToolType.Axe, ToolTier.Wood);
        float withPickaxe = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, pickaxe, skills);
        float withAxe = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, axe, skills);
        float bareHands = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, ItemStack.Empty, skills);

        Assert.True(withPickaxe < withAxe);
        Assert.Equal(bareHands, withAxe);
        Assert.True(withPickaxe < bareHands);
    }

    [Fact]
    public void GetXpForBlockReturnsExpectedValues()
    {
        Assert.Equal(2f, MiningCalculator.GetXpForBlock(BlockType.OakLog));
        Assert.Equal(1f, MiningCalculator.GetXpForBlock(BlockType.Dirt));
        Assert.Equal(3f, MiningCalculator.GetXpForBlock(BlockType.Stone));
        Assert.Equal(0f, MiningCalculator.GetXpForBlock(BlockType.Air));
    }

    [Fact]
    public void GetSkillForBlockMapsHarvestCategories()
    {
        Assert.Equal(PlayerSkill.Woodcutting, MiningCalculator.GetSkillForBlock(BlockType.OakLog));
        Assert.Equal(PlayerSkill.Mining, MiningCalculator.GetSkillForBlock(BlockType.Dirt));
        Assert.Equal(PlayerSkill.Mining, MiningCalculator.GetSkillForBlock(BlockType.Stone));
    }
}
