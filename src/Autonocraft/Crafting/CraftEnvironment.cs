using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public enum TimePhase
    {
        Night,
        Dawn,
        Day,
        Dusk
    }

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
                TimePhase = GetTimePhase(timeOfDay),
                HasAdjacentWater = HasBlockNearby(world, wx, wy, wz, BlockType.Water, radius: 2),
                HasAdjacentHeat = HasHeatNearby(world, wx, wy, wz),
                HasFuelInInputs = fuelInInputs
            };
        }

        public static TimePhase GetTimePhase(float timeOfDay)
        {
            float t = timeOfDay - MathF.Floor(timeOfDay);
            if (t < 0f) t += 1f;

            if (t >= 0.2f && t < 0.3f) return TimePhase.Dawn;
            if (t >= 0.3f && t < 0.7f) return TimePhase.Day;
            if (t >= 0.7f && t < 0.82f) return TimePhase.Dusk;
            return TimePhase.Night;
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
