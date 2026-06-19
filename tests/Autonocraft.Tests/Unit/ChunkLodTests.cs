using Autonocraft.World;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class ChunkLodTests
{
    [Theory]
    [InlineData(4, 1, ChunkMeshDetail.Full)]
    [InlineData(4, 2, ChunkMeshDetail.Full)]
    [InlineData(4, 4, ChunkMeshDetail.Surface)]
    [InlineData(6, 2, ChunkMeshDetail.Full)]
    [InlineData(6, 4, ChunkMeshDetail.Surface)]
    [InlineData(6, 6, ChunkMeshDetail.Shell)]
    [InlineData(10, 3, ChunkMeshDetail.Surface)]
    [InlineData(10, 6, ChunkMeshDetail.Shell)]
    [InlineData(10, 10, ChunkMeshDetail.Shell)]
    public void SelectDetailMatchesExpectedBands(int renderDistance, int chunkDistance, ChunkMeshDetail expected)
    {
        Assert.Equal(expected, ChunkLod.SelectDetail(chunkDistance, renderDistance));
    }

    [Fact]
    public void GetBandThresholdsAreMonotonic()
    {
        var (lod0Max, lod1Max) = ChunkLod.GetBandThresholds(8);
        Assert.True(lod0Max >= 1);
        Assert.True(lod1Max > lod0Max);
    }

    [Fact]
    public void SelectBuildDetailStartsWithShellForFreshChunk()
    {
        var chunk = new Chunk(0, 0);
        var detail = ChunkLod.SelectBuildDetail(chunk, chunkDistance: 1, renderDistance: 8);
        Assert.Equal(ChunkMeshDetail.Shell, detail);
    }

    [Fact]
    public void SelectBuildDetailUpgradesAfterShellExists()
    {
        var chunk = new Chunk(0, 0);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Shell);
        var detail = ChunkLod.SelectBuildDetail(chunk, chunkDistance: 1, renderDistance: 8);
        Assert.Equal(ChunkMeshDetail.Surface, detail);
    }

    [Fact]
    public void TryGetRenderableDetailAllowsPlayableShellWhileFullTargetPending()
    {
        var chunk = new Chunk(0, 0);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Shell);

        Assert.True(ChunkLod.TryGetRenderableDetail(chunk, ChunkMeshDetail.Full, out var actual));
        Assert.Equal(ChunkMeshDetail.Shell, actual);
    }

    [Fact]
    public void NeedsHigherDetailBuildFalseWhenTargetMet()
    {
        var chunk = new Chunk(0, 0);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Shell);
        Assert.True(ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance: 4, renderDistance: 8));
        chunk.EnsureMeshForTest(ChunkMeshDetail.Surface);
        Assert.False(ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance: 4, renderDistance: 8));
    }

    [Fact]
    public void SelectBuildDetailRestrictLodSkipsFullBeyondNearRing()
    {
        var chunk = new Chunk(0, 0);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Shell);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Surface);
        var detail = ChunkLod.SelectBuildDetail(chunk, chunkDistance: 3, renderDistance: 8, restrictLod: true);
        Assert.Equal(ChunkMeshDetail.Surface, detail);
    }

    [Fact]
    public void SelectBuildDetailDeferFullDetailSkipsFullNearPlayer()
    {
        var chunk = new Chunk(0, 0);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Shell);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Surface);
        var detail = ChunkLod.SelectBuildDetail(chunk, chunkDistance: 1, renderDistance: 8, deferFullDetail: true);
        Assert.Equal(ChunkMeshDetail.Surface, detail);
        Assert.False(ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance: 1, renderDistance: 8, deferFullDetail: true));
    }

    [Fact]
    public void GetChunkDistanceUsesChebyshevMetric()
    {
        Assert.Equal(3, ChunkLod.GetChunkDistance(5, 8, 2, 5));
        Assert.Equal(0, ChunkLod.GetChunkDistance(2, 5, 2, 5));
    }

    [Fact]
    public void TryGetRenderableDetailFallsBackAfterSingleTierInvalidation()
    {
        var chunk = new Chunk(0, 0);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Shell);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Surface);
        chunk.EnsureMeshForTest(ChunkMeshDetail.Full);

        chunk.InvalidateMeshDetail(ChunkMeshDetail.Full);

        Assert.True(ChunkLod.TryGetRenderableDetail(chunk, ChunkMeshDetail.Full, out var actual));
        Assert.Equal(ChunkMeshDetail.Surface, actual);
    }

    [Fact]
    public void GetFogRangeShellIsCloserThanFull()
    {
        var full = ChunkLod.GetFogRange(8, ChunkMeshDetail.Full);
        var shell = ChunkLod.GetFogRange(8, ChunkMeshDetail.Shell);
        Assert.True(shell.end < full.end);
        Assert.True(shell.start < full.start);
    }
}
