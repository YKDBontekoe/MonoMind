using System;
using System.Numerics;

namespace Autonocraft.World
{
    public struct LavaBodyState
    {
        public bool InLava;
        public bool HeadInLava;
        public bool OnSurface;
        public float SurfaceY;
    }

    public static class LavaQuery
    {
        public static bool IsBlockLava(VoxelWorld world, int x, int y, int z)
        {
            var type = world.GetBlock(x, y, z);
            return type.IsLava();
        }

        public static LavaBodyState GetBodyState(
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

            bool inLava = false;
            float highestSurface = float.MinValue;

            for (int y = startY; y <= endY; y++)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        if (!IsBlockLava(world, x, y, z))
                        {
                            continue;
                        }

                        inLava = true;
                        highestSurface = MathF.Max(highestSurface, y + 1f);
                    }
                }
            }

            int eyeBlockX = (int)MathF.Floor(position.X);
            int eyeBlockY = (int)MathF.Floor(eyeY);
            int eyeBlockZ = (int)MathF.Floor(position.Z);
            bool headInLava = IsBlockLava(world, eyeBlockX, eyeBlockY, eyeBlockZ);

            bool onSurface = inLava && !headInLava;

            return new LavaBodyState
            {
                InLava = inLava,
                HeadInLava = headInLava,
                OnSurface = onSurface,
                SurfaceY = onSurface ? highestSurface : 0f
            };
        }

        public static bool IsCameraUnderLava(VoxelWorld world, Vector3 cameraPosition)
        {
            int x = (int)MathF.Floor(cameraPosition.X);
            int y = (int)MathF.Floor(cameraPosition.Y);
            int z = (int)MathF.Floor(cameraPosition.Z);
            return IsBlockLava(world, x, y, z);
        }

        public static bool IsLandingInLava(VoxelWorld world, Vector3 position)
        {
            int x = (int)MathF.Floor(position.X);
            int y = (int)MathF.Floor(position.Y);
            int z = (int)MathF.Floor(position.Z);

            if (IsBlockLava(world, x, y, z))
            {
                return true;
            }

            return IsBlockLava(world, x, y - 1, z);
        }
    }
}
