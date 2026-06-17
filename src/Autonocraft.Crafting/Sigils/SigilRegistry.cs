using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public static class SigilRegistry
    {
        public static IReadOnlyList<SigilPattern> All { get; } = BuildAll();

        private static IReadOnlyList<SigilPattern> BuildAll()
        {
            return new[]
            {
                BuildBenchSigil(),
                BuildForgeSigil(),
                BuildCrucibleSigil()
            };
        }

        private static SigilPattern BuildBenchSigil()
        {
            var cells = new List<SigilCell>();
            for (int dx = -1; dx <= 1; dx++)
            {
                cells.Add(new SigilCell { Dx = dx, Dy = 0, Dz = 0, Tag = MaterialTag.Wood, IsCenter = dx == 0 });
                cells.Add(new SigilCell { Dx = dx, Dy = -1, Dz = 0, ExactBlock = BlockType.Stone });
            }

            return new SigilPattern
            {
                Id = "sigil:bench",
                DisplayName = "Workbench Sigil",
                OutputStation = BlockType.StationBench,
                Cells = cells
            };
        }

        private static SigilPattern BuildForgeSigil()
        {
            var cells = new List<SigilCell>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    bool isCenter = dx == 0 && dz == 0;
                    cells.Add(new SigilCell
                    {
                        Dx = dx,
                        Dy = 0,
                        Dz = dz,
                        ExactBlock = isCenter ? BlockType.CoalOre : BlockType.Stone,
                        IsCenter = isCenter
                    });
                }
            }

            return new SigilPattern
            {
                Id = "sigil:forge",
                DisplayName = "Forge Sigil",
                OutputStation = BlockType.StationForge,
                Cells = cells
            };
        }

        private static SigilPattern BuildCrucibleSigil()
        {
            var cells = new List<SigilCell>();
            int[,] footprint =
            {
                { 1, 0, 1 },
                { 1, 1, 1 },
                { 1, 1, 1 }
            };

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (footprint[dz + 1, dx + 1] == 0)
                    {
                        continue;
                    }

                    bool isCenter = dx == 0 && dz == 0;
                    cells.Add(new SigilCell
                    {
                        Dx = dx,
                        Dy = 0,
                        Dz = dz,
                        ExactBlock = BlockType.Stone,
                        IsCenter = isCenter
                    });
                }
            }

            return new SigilPattern
            {
                Id = "sigil:crucible",
                DisplayName = "Crucible Sigil",
                OutputStation = BlockType.StationCrucible,
                RequiresAdjacentWater = true,
                Cells = cells
            };
        }
    }

    public static class SigilMatcher
    {
        public static bool TryMatch(VoxelWorld world, int cx, int cy, int cz, out SigilPattern? matched, out float partialScore)
        {
            matched = null;
            partialScore = 0f;
            float bestPartial = 0f;
            SigilPattern? bestPartialPattern = null;

            foreach (var pattern in SigilRegistry.All)
            {
                if (pattern.TryMatch(world, cx, cy, cz, out float score))
                {
                    matched = pattern;
                    partialScore = 1f;
                    return true;
                }

                if (score > bestPartial)
                {
                    bestPartial = score;
                    bestPartialPattern = pattern;
                }
            }

            if (bestPartial >= 0.5f && bestPartial < 1f)
            {
                matched = bestPartialPattern;
                partialScore = bestPartial;
            }

            return false;
        }
    }
}
