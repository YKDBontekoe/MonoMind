using System;
using System.Collections.Generic;
using Autonocraft.Domain.World;

namespace Autonocraft.World.Structures
{
    /// <summary>
    /// Flat showcase world that places every registered structure on a grid for inspection.
    /// </summary>
    public static class StructureGallery
    {
        public const int Seed = 4242;
        public const int Columns = 8;
        public const int MegaColumns = 2;
        public const int SurfaceY = WorldConstants.SeaLevel + 1;
        public const int Padding = 6;
        public const int MegaMinCell = 96;

        public static bool IsGalleryWorld(WorldType worldType) => worldType == WorldType.StructureGallery;

        public static int VariantSaltFor(int index)
        {
            var definition = StructureRegistry.All[index];
            return StructurePlacementKeys.MixSeed(Seed, index * 31, index * 17, StableOrdinalHash.Hash(definition.Id));
        }

        public readonly struct Placement
        {
            public string Id { get; init; }
            public StructureTier Tier { get; init; }
            public int Index { get; init; }
            public int AnchorX { get; init; }
            public int AnchorZ { get; init; }
            public int SurfaceY { get; init; }
            public int FootprintRadius { get; init; }
            public int CellSize { get; init; }
        }

        public static TerrainColumn CreateFlatColumn(int wx, int wz)
        {
            return new TerrainColumn
            {
                SurfaceHeight = SurfaceY,
                Biome = new BiomeSample { Primary = BiomeType.Plains },
                SurfaceBlock = BlockType.Grass,
                SubsurfaceBlock = BlockType.Dirt,
                FillerBlock = BlockType.Stone,
                SmoothedHeight = SurfaceY
            };
        }

        public static IReadOnlyList<Placement> GetPlacements()
        {
            var structures = StructureRegistry.All;
            var placements = new List<Placement>(structures.Count);
            int col = 0;
            int rowStartX = 0;
            int rowBaseZ = Padding;
            int rowMaxCell = 0;
            StructureTier? rowTier = null;

            for (int i = 0; i < structures.Count; i++)
            {
                var structure = structures[i];
                int variantSalt = VariantSaltFor(i);
                var template = structure.ResolveTemplate(Seed, 0, 0, variantSalt, BiomeType.Plains);
                int cellSize = template.FootprintRadius * 2 + Padding * 2;
                if (structure.Tier == StructureTier.Mega)
                {
                    cellSize = Math.Max(cellSize, MegaMinCell);
                }

                int rowColumns = structure.Tier == StructureTier.Mega ? MegaColumns : Columns;
                if (rowTier != null && rowTier != structure.Tier && col == 0)
                {
                    rowBaseZ += rowMaxCell + Padding;
                    rowMaxCell = 0;
                }

                if (col >= rowColumns)
                {
                    rowBaseZ += rowMaxCell + Padding;
                    col = 0;
                    rowStartX = 0;
                    rowMaxCell = 0;
                }

                rowTier = structure.Tier;
                int anchorX = rowStartX + cellSize / 2;
                int anchorZ = rowBaseZ + cellSize - Padding;

                placements.Add(new Placement
                {
                    Id = structure.Id,
                    Tier = structure.Tier,
                    Index = i,
                    AnchorX = anchorX,
                    AnchorZ = anchorZ,
                    SurfaceY = SurfaceY,
                    FootprintRadius = template.FootprintRadius,
                    CellSize = cellSize
                });

                rowStartX += cellSize;
                rowMaxCell = Math.Max(rowMaxCell, cellSize);
                col++;
            }

            CenterPlacements(placements);
            return placements;
        }

        public static (int X, int Z) GetPlayerSpawn()
        {
            var placements = GetPlacements();
            if (placements.Count == 0)
            {
                return (0, Padding);
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minZ = int.MaxValue;

            foreach (var placement in placements)
            {
                minX = Math.Min(minX, placement.AnchorX - placement.FootprintRadius);
                maxX = Math.Max(maxX, placement.AnchorX + placement.FootprintRadius);
                minZ = Math.Min(minZ, placement.AnchorZ - placement.FootprintRadius);
            }

            return ((minX + maxX) / 2, minZ - Padding * 2);
        }

        private static void CenterPlacements(List<Placement> placements)
        {
            if (placements.Count == 0)
            {
                return;
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                minX = Math.Min(minX, placement.AnchorX - placement.FootprintRadius);
                maxX = Math.Max(maxX, placement.AnchorX + placement.FootprintRadius);
            }

            int offsetX = -((minX + maxX) / 2);
            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                placements[i] = placement with { AnchorX = placement.AnchorX + offsetX };
            }
        }
    }
}
