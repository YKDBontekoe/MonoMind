using Autonocraft.Domain.Core;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public sealed class CraftEnvironment
    {
        public BiomeType Biome { get; init; }
        public TimePhase TimePhase { get; init; }
        public bool HasAdjacentWater { get; init; }
        public bool HasAdjacentHeat { get; init; }
        public bool HasFuelInInputs { get; init; }

        public static CraftEnvironment Sample(VoxelWorld world, int wx, int wy, int wz, float timeOfDay, bool fuelInInputs = false)
        {
            var biomeSample = world.SampleBiome(wx, wz);
            return new CraftEnvironment
            {
                Biome = biomeSample.Primary,
                TimePhase = DayNightCycle.GetTimePhase(timeOfDay),
                HasAdjacentWater = HasBlockNearby(world, wx, wy, wz, BlockType.Water, radius: 2),
                HasAdjacentHeat = HasHeatNearby(world, wx, wy, wz),
                HasFuelInInputs = fuelInInputs
            };
        }

        private static bool HasBlockNearby(VoxelWorld world, int wx, int wy, int wz, BlockType target, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (world.GetBlock(wx + dx, wy + dy, wz + dz) == target)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasHeatNearby(VoxelWorld world, int wx, int wy, int wz)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        BlockType block = world.GetBlock(wx + dx, wy + dy, wz + dz);
                        if (block == BlockType.CoalOre || block == BlockType.StationForge)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
