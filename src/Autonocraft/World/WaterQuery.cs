using System;
using System.Numerics;

namespace Autonocraft.World
{
    public struct WaterBodyState
    {
        public bool InWater;
        public bool HeadUnderwater;
        public bool OnSurface;
        public float SurfaceY;
    }

    public static class WaterQuery
    {
        public static bool IsBlockWater(VoxelWorld world, int x, int y, int z)
        {
            return world.GetBlock(x, y, z).IsWater();
        }

        public static WaterBodyState GetBodyState(
            VoxelWorld world,
            Vector3 position,
            float width,
            float height,
            float eyeHeight)
        {
            float minX = position.X - width / 2f;
            float maxX = position.X + width / 2f;
            float minY = position.Y;
            float maxY = position.Y + height;
            float eyeY = position.Y + eyeHeight;
            float minZ = position.Z - width / 2f;
            float maxZ = position.Z + width / 2f;

            int startX = (int)MathF.Floor(minX);
            int endX = (int)MathF.Floor(maxX);
            int startY = (int)MathF.Floor(minY);
            int endY = (int)MathF.Floor(maxY);
            int startZ = (int)MathF.Floor(minZ);
            int endZ = (int)MathF.Floor(maxZ);

            bool inWater = false;
            float highestSurface = float.MinValue;

            for (int y = startY; y <= endY; y++)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        if (!IsBlockWater(world, x, y, z))
                        {
                            continue;
                        }

                        inWater = true;
                        highestSurface = MathF.Max(highestSurface, y + 1f);
                    }
                }
            }

            int eyeBlockX = (int)MathF.Floor(position.X);
            int eyeBlockY = (int)MathF.Floor(eyeY);
            int eyeBlockZ = (int)MathF.Floor(position.Z);
            bool headUnderwater = IsBlockWater(world, eyeBlockX, eyeBlockY, eyeBlockZ);

            bool onSurface = inWater && !headUnderwater;

            return new WaterBodyState
            {
                InWater = inWater,
                HeadUnderwater = headUnderwater,
                OnSurface = onSurface,
                SurfaceY = onSurface ? highestSurface : 0f
            };
        }

        public static bool IsCameraUnderwater(VoxelWorld world, Vector3 cameraPosition)
        {
            int x = (int)MathF.Floor(cameraPosition.X);
            int y = (int)MathF.Floor(cameraPosition.Y);
            int z = (int)MathF.Floor(cameraPosition.Z);
            return IsBlockWater(world, x, y, z);
        }

        public static bool IsLandingInWater(VoxelWorld world, Vector3 position)
        {
            int x = (int)MathF.Floor(position.X);
            int y = (int)MathF.Floor(position.Y);
            int z = (int)MathF.Floor(position.Z);

            if (IsBlockWater(world, x, y, z))
            {
                return true;
            }

            return IsBlockWater(world, x, y - 1, z);
        }
    }
}
