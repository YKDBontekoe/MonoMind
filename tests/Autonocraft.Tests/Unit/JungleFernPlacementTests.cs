using Autonocraft.Tests.Integration;
using Autonocraft.World;
using Autonocraft.World.Generation;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class JungleFernPlacementTests
{
    [Fact]
    public void JungleBiome_GeneratesFern_WithDefaultSeed()
    {
        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));
        var anchor = WorldGenTestHelpers.FindPreviewCoord(
            generator,
            column => column.Biome.Primary == BiomeType.Jungle && !column.IsRiver && !column.IsLake,
            radius: 768,
            step: 4);
        Assert.NotNull(anchor);

        int centerChunkX = anchor.Value.x >> 4;
        int centerChunkZ = anchor.Value.z >> 4;
        bool found = false;
        for (int chunkZ = centerChunkZ - 2; chunkZ <= centerChunkZ + 2 && !found; chunkZ++)
        {
            for (int chunkX = centerChunkX - 2; chunkX <= centerChunkX + 2; chunkX++)
            {
                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                for (int lx = 0; lx < Chunk.Width; lx++)
                {
                    for (int lz = 0; lz < Chunk.Depth; lz++)
                    {
                        for (int y = 1; y < Chunk.Height; y++)
                        {
                            if (chunk.GetBlock(lx, y, lz) == BlockType.Fern)
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        Assert.True(found);
    }
}
