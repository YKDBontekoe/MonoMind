using Autonocraft.Domain.World;
using Autonocraft.Tests.Integration;
using Autonocraft.World;
using Autonocraft.World.Generation;
using Xunit;

namespace Autonocraft.Tests.Unit;

public sealed class SwampLilyPadPlacementTests
{
    [Fact]
    public void SwampLakeColumns_GenerateLilyPads()
    {
        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));
        var lake = WorldGenTestHelpers.FindPreviewCoord(
            generator,
            c => c.Biome.Primary == BiomeType.Swamp && c.IsLake,
            radius: 768,
            step: 4);
        Assert.NotNull(lake);

        int centerChunkX = lake.Value.x >> 4;
        int centerChunkZ = lake.Value.z >> 4;
        bool found = false;
        for (int chunkZ = centerChunkZ - 4; chunkZ <= centerChunkZ + 4 && !found; chunkZ++)
        {
            for (int chunkX = centerChunkX - 4; chunkX <= centerChunkX + 4 && !found; chunkX++)
            {
                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                found = ChunkContainsBlock(chunk, BlockType.LilyPad);
            }
        }

        Assert.True(found, $"Expected LilyPad near swamp lake anchor {lake}");
    }

    private static bool ChunkContainsBlock(Chunk chunk, BlockType type)
    {
        for (int y = 0; y < Chunk.Height; y++)
        {
            for (int lx = 0; lx < Chunk.Width; lx++)
            {
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    if (chunk.GetBlockUnchecked(lx, y, lz) == type)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
