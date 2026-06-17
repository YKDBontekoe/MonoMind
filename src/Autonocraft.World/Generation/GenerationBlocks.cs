using System;

namespace Autonocraft.World
{
    internal static class GenerationBlocks
    {
        public static void SetBlock(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int y, BlockType type)
        {
            if (world != null)
            {
                world.SetBlockDuringGeneration(wx, y, wz, type);
                return;
            }

            if (lx >= 0 && lx < Chunk.Width && lz >= 0 && lz < Chunk.Depth)
            {
                chunk.SetBlockUnchecked(lx, y, lz, type);
            }
        }

        public static bool SetBlockIfAir(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int y, BlockType type)
        {
            if (y <= 0 || y >= Chunk.Height)
            {
                return false;
            }

            if (world != null)
            {
                if (world.GetBlock(wx, y, wz) == BlockType.Air)
                {
                    world.SetBlockDuringGeneration(wx, y, wz, type);
                    return true;
                }

                return false;
            }

            if (lx >= 0 && lx < Chunk.Width && lz >= 0 && lz < Chunk.Depth)
            {
                if (chunk.GetBlockUnchecked(lx, y, lz) == BlockType.Air)
                {
                    chunk.SetBlockUnchecked(lx, y, lz, type);
                    return true;
                }
            }

            return false;
        }

        public static int Hash(int wx, int wz, int seed, int salt)
        {
            unchecked
            {
                return Math.Abs((wx * 92821 + wz * 68917 + seed + salt) % 100000);
            }
        }

        public static int Hash3D(int wx, int y, int wz, int seed)
        {
            unchecked
            {
                int h = wx * 734287 + y * 912271 + wz * 438289 + seed;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return Math.Abs(h);
            }
        }
    }
}
