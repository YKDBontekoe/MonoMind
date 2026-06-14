using Autonocraft.Crafting;
using Autonocraft.World;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class SigilRegistryTests
{
    [Fact]
    public void AllContainsThreeStationSigils()
    {
        Assert.Equal(3, SigilRegistry.All.Count);
        Assert.Contains(SigilRegistry.All, s => s.Id == "sigil:bench");
        Assert.Contains(SigilRegistry.All, s => s.Id == "sigil:forge");
        Assert.Contains(SigilRegistry.All, s => s.Id == "sigil:crucible");
    }

    [Fact]
    public void BenchSigilOutputsWorkbenchStation()
    {
        var bench = SigilRegistry.All.Single(s => s.Id == "sigil:bench");
        Assert.Equal(BlockType.StationBench, bench.OutputStation);
        Assert.Contains(bench.Cells, c => c.IsCenter);
    }

    [Fact]
    public void CrucibleSigilRequiresAdjacentWater()
    {
        var crucible = SigilRegistry.All.Single(s => s.Id == "sigil:crucible");
        Assert.True(crucible.RequiresAdjacentWater);
        Assert.Equal(BlockType.StationCrucible, crucible.OutputStation);
    }

    [Fact]
    public void ForgeSigilUsesCoalOreAtCenter()
    {
        var forge = SigilRegistry.All.Single(s => s.Id == "sigil:forge");
        var center = forge.Cells.Single(c => c.IsCenter);
        Assert.Equal(BlockType.CoalOre, center.ExactBlock);
    }
}
