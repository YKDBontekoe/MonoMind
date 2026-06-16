using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public readonly struct SigilCell
    {
        public int Dx { get; init; }
        public int Dy { get; init; }
        public int Dz { get; init; }
        public BlockType? ExactBlock { get; init; }
        public MaterialTag? Tag { get; init; }
        public bool IsCenter { get; init; }

        public bool Matches(BlockType block)
        {
            if (ExactBlock.HasValue)
            {
                return block == ExactBlock.Value;
            }

            if (Tag.HasValue)
            {
                return block.MatchesTag(Tag.Value) || (Tag.Value == MaterialTag.Wood && block.IsAnyLog());
            }

            return false;
        }
    }

    public sealed class SigilPattern
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public BlockType OutputStation { get; init; }
        public bool RequiresAdjacentWater { get; init; }
        public IReadOnlyList<SigilCell> Cells { get; init; } = Array.Empty<SigilCell>();

        public bool TryMatch(VoxelWorld world, int cx, int cy, int cz, out float partialScore)
        {
            partialScore = 0f;
            if (Cells.Count == 0)
            {
                return false;
            }

            int matched = 0;
            foreach (var cell in Cells)
            {
                BlockType block = world.GetBlock(cx + cell.Dx, cy + cell.Dy, cz + cell.Dz);
                if (cell.Matches(block))
                {
                    matched++;
                }
            }

            partialScore = matched / (float)Cells.Count;
            if (partialScore < 1f)
            {
                return false;
            }

            if (RequiresAdjacentWater && !HasWaterNearby(world, cx, cy, cz, radius: 2))
            {
                partialScore = 0.85f;
                return false;
            }

            return true;
        }

        public IEnumerable<(int x, int y, int z)> GetConsumedPositions(int cx, int cy, int cz)
        {
            foreach (var cell in Cells)
            {
                if (cell.IsCenter)
                {
                    continue;
                }

                yield return (cx + cell.Dx, cy + cell.Dy, cz + cell.Dz);
            }
        }

        private static bool HasWaterNearby(VoxelWorld world, int cx, int cy, int cz, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (world.GetBlock(cx + dx, cy + dy, cz + dz) == BlockType.Water)
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
