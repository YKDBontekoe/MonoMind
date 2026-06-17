using System;
using System.Numerics;
using Autonocraft.Entities.Navigation;
using Autonocraft.World;

namespace Autonocraft.Village
{
    internal static class VillageSpawnHelper
    {
        public static Vector3 FindSpawnPosition(VoxelWorld world, Village village, int seed)
        {
            VillageSettlementHealth.EnsureVillageChunksLoaded(world, village);
            var rng = new Random(seed);
            float maxDist = MathF.Min(MathF.Max(village.Radius - 2f, 4f), 10f);

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                float dist = 1.5f + (float)rng.NextDouble() * (maxDist - 1.5f);
                int x = village.AnchorX + (int)MathF.Round(MathF.Cos(angle) * dist);
                int z = village.AnchorZ + (int)MathF.Round(MathF.Sin(angle) * dist);
                var candidate = SpawnPositionHelper.FindSafeSpawnPosition(world, x, z, 0.6f, 1.8f);
                if (IsNearTownHeart(village, candidate, maxDist + 2f))
                {
                    return candidate;
                }
            }

            return SpawnPositionHelper.FindSafeSpawnPosition(world, village.AnchorX, village.AnchorZ, 0.6f, 1.8f);
        }

        private static bool IsNearTownHeart(Village village, Vector3 position, float maxHorizontalDistance)
        {
            float dx = position.X - (village.AnchorX + 0.5f);
            float dz = position.Z - (village.AnchorZ + 0.5f);
            return MathF.Sqrt(dx * dx + dz * dz) <= maxHorizontalDistance;
        }
    }
}
