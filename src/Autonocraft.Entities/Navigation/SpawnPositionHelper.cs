using System.Numerics;
using Autonocraft.World;

namespace Autonocraft.Entities.Navigation
{
    public static class SpawnPositionHelper
    {
        public static Vector3 FindSafeSpawnPosition(VoxelWorld world, int x, int z, float width, float height)
        {
            float spawnX = x + 0.5f;
            float spawnZ = z + 0.5f;
            int surfaceY = world.GetHighestSolidY(x, z);
            if (surfaceY < 0)
            {
                surfaceY = 64;
            }

            for (int offset = 0; offset < 16; offset++)
            {
                float spawnY = surfaceY + 1f + offset;
                var candidate = new Vector3(spawnX, spawnY, spawnZ);
                if (EntityCollision.IsSpaceClearAt(world, candidate, width, height))
                {
                    return candidate;
                }
            }

            return new Vector3(spawnX, surfaceY + 1f, spawnZ);
        }
    }
}
