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
        var candidates = WorldGenTestHelpers.FindBiomeCoordsFast(
            generator,
            biome => biome.Primary == BiomeType.Jungle,
            radius: 1536,
            step: 32,
            maxResults: 15);
        Assert.NotEmpty(candidates);

        bool found = false;
        foreach (var anchor in candidates)
        {
            int centerChunkX = anchor.x >> 4;
            int centerChunkZ = anchor.z >> 4;
            for (int chunkZ = centerChunkZ - 2; chunkZ <= centerChunkZ + 2 && !found; chunkZ++)
            {
                for (int chunkX = centerChunkX - 2; chunkX <= centerChunkX + 2 && !found; chunkX++)
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

            if (found)
            {
                break;
            }
        }

        Assert.True(found, $"Expected fern near jungle anchors {string.Join(", ", candidates)}");
    }
}
