using Autonocraft.Domain.World;
using Autonocraft.Tests.Integration;
using Autonocraft.World;
using Autonocraft.World.Generation;
using Xunit;

namespace Autonocraft.Tests.Unit;

public sealed class SnowyPeaksFloraPlacementTests
{
    [Fact]
    public void SnowyPeaks_GeneratesHeather()
    {
        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));
        var anchor = WorldGenTestHelpers.FindPreviewCoord(
            generator,
            c => c.Biome.Primary == BiomeType.SnowyPeaks && !c.IsRiver && !c.IsLake,
            radius: 768,
            step: 4);
        Assert.NotNull(anchor);

        int centerChunkX = anchor.Value.x >> 4;
        int centerChunkZ = anchor.Value.z >> 4;
        bool found = false;
        for (int chunkZ = centerChunkZ - 4; chunkZ <= centerChunkZ + 4 && !found; chunkZ++)
        {
            for (int chunkX = centerChunkX - 4; chunkX <= centerChunkX + 4; chunkX++)
            {
                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                for (int y = 0; y < Chunk.Height; y++)
                {
                    for (int lx = 0; lx < Chunk.Width; lx++)
                    {
                        for (int lz = 0; lz < Chunk.Depth; lz++)
                        {
                            if (chunk.GetBlockUnchecked(lx, y, lz) == BlockType.Heather)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                        {
                            break;
                        }
                    }

                    if (found)
                    {
                        break;
                    }
                }
            }
        }

        Assert.True(found, $"Expected Heather near snowy peaks anchor {anchor}");
    }
}
