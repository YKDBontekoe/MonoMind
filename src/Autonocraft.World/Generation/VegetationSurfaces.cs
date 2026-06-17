namespace Autonocraft.World.Generation
{
    internal static class VegetationSurfaces
    {
        public static BlockType GetSurfaceBlock(Chunk chunk, VoxelWorld? world, int wx, int wz, int lx, int lz, int surfaceHeight)
        {
            if (world != null)
            {
                return world.GetBlock(wx, surfaceHeight, wz);
            }

            if (lx >= 0 && lx < Chunk.Width && lz >= 0 && lz < Chunk.Depth)
            {
                return chunk.GetBlockUnchecked(lx, surfaceHeight, lz);
            }

            return BlockType.Air;
        }

        public static bool CanPlaceTree(BlockType surface, BiomeType biome)
        {
            return surface switch
            {
                BlockType.Grass or BlockType.Dirt or BlockType.Mud or BlockType.Sand or BlockType.RedSand => true,
                BlockType.Snow when biome is BiomeType.SnowyPeaks => true,
                _ => false
            };
        }

        public static bool CanPlaceFlora(BlockType surface, BiomeType biome, BlockType flora)
        {
            if (flora == BlockType.DeadBush)
            {
                return surface == BlockType.Sand && biome is BiomeType.Desert or BiomeType.Beach or BiomeType.Badlands
                    || surface is BlockType.RedSand
                    || surface is BlockType.Grass or BlockType.Dirt;
            }

            if (flora == BlockType.MossCarpet)
            {
                return surface is BlockType.Grass or BlockType.Dirt or BlockType.Mud or BlockType.MossStone;
            }

            if (flora == BlockType.Lichen)
            {
                return surface is BlockType.Stone or BlockType.MossStone or BlockType.Cobblestone or BlockType.Gravel;
            }

            if (surface is BlockType.Grass or BlockType.Dirt or BlockType.Mud or BlockType.MossStone)
            {
                return true;
            }

            if (surface == BlockType.RedSand)
            {
                return biome is BiomeType.Badlands && flora is BlockType.Cactus or BlockType.DeadBush or BlockType.Lichen;
            }

            if (surface == BlockType.Sand)
            {
                return biome is BiomeType.Desert or BiomeType.Beach or BiomeType.Ocean
                    && flora is BlockType.Cactus or BlockType.DeadBush;
            }

            if (surface == BlockType.Snow)
            {
                return biome is BiomeType.SnowyPeaks
                    && flora is BlockType.Heather or BlockType.Juniper;
            }

            return false;
        }

        public static bool CanPlaceMoss(BlockType surface)
        {
            return surface is BlockType.MossStone or BlockType.Mud;
        }
    }
}
