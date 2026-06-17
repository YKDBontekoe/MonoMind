using System;
using System.Collections.Generic;

namespace Autonocraft.World.Generation.Flora
{
    public readonly struct FloraPlacementEntry
    {
        public BlockType Block { get; init; }
        public int Weight { get; init; }
        public float SampleThreshold { get; init; }
        public int HashMod { get; init; }
        public bool UnderstoryOnly { get; init; }
        public bool RequiresFlowers { get; init; }
        public bool RequiresCactus { get; init; }
    }

    public static class FloraPlacementRules
    {
        public static IReadOnlyList<FloraPlacementEntry> For(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Forest => ForestEntries,
                BiomeType.Plains => PlainsEntries,
                BiomeType.Desert => DesertEntries,
                BiomeType.Swamp => SwampEntries,
                BiomeType.SnowyPeaks => SnowyPeaksEntries,
                BiomeType.Mountains => MountainsEntries,
                BiomeType.Beach => BeachEntries,
                _ => GenericEntries
            };
        }

        private static readonly FloraPlacementEntry[] ForestEntries =
        [
            new() { Block = BlockType.Fern, Weight = 14, SampleThreshold = 0.52f, HashMod = 4, UnderstoryOnly = true },
            new() { Block = BlockType.MushroomRed, Weight = 5, SampleThreshold = 0.48f, HashMod = 19, UnderstoryOnly = true },
            new() { Block = BlockType.MushroomBrown, Weight = 5, SampleThreshold = 0.48f, HashMod = 23, UnderstoryOnly = true },
            new() { Block = BlockType.BerryBush, Weight = 10, SampleThreshold = 0.54f, HashMod = 7 },
            new() { Block = BlockType.Shrub, Weight = 12, SampleThreshold = 0.50f, HashMod = 5 },
            new() { Block = BlockType.MossCarpet, Weight = 14, SampleThreshold = 0.48f, HashMod = 4 },
            new() { Block = BlockType.Poppy, Weight = 10, SampleThreshold = 0.50f, RequiresFlowers = true },
            new() { Block = BlockType.WildRose, Weight = 10, SampleThreshold = 0.52f, RequiresFlowers = true },
            new() { Block = BlockType.BlueFlax, Weight = 8, SampleThreshold = 0.50f, RequiresFlowers = true },
            new() { Block = BlockType.Flower, Weight = 10, SampleThreshold = 0.50f, RequiresFlowers = true },
            new() { Block = BlockType.Daisy, Weight = 12, SampleThreshold = 0.48f, RequiresFlowers = true },
            new() { Block = BlockType.Sunflower, Weight = 4, SampleThreshold = 0.58f, HashMod = 9, RequiresFlowers = true }
        ];

        private static readonly FloraPlacementEntry[] PlainsEntries =
        [
            new() { Block = BlockType.Daisy, Weight = 18, SampleThreshold = 0.46f, RequiresFlowers = true },
            new() { Block = BlockType.Poppy, Weight = 14, SampleThreshold = 0.46f, RequiresFlowers = true },
            new() { Block = BlockType.Tulip, Weight = 12, SampleThreshold = 0.48f, RequiresFlowers = true },
            new() { Block = BlockType.BlueFlax, Weight = 14, SampleThreshold = 0.46f, RequiresFlowers = true },
            new() { Block = BlockType.Flower, Weight = 16, SampleThreshold = 0.46f, RequiresFlowers = true },
            new() { Block = BlockType.WildRose, Weight = 10, SampleThreshold = 0.48f, RequiresFlowers = true },
            new() { Block = BlockType.Sunflower, Weight = 8, SampleThreshold = 0.54f, HashMod = 7, RequiresFlowers = true },
            new() { Block = BlockType.Lavender, Weight = 14, SampleThreshold = 0.48f, HashMod = 5 },
            new() { Block = BlockType.BerryBush, Weight = 12, SampleThreshold = 0.52f, HashMod = 7 },
            new() { Block = BlockType.Shrub, Weight = 12, SampleThreshold = 0.48f, HashMod = 5 },
            new() { Block = BlockType.MossCarpet, Weight = 8, SampleThreshold = 0.46f, HashMod = 6 }
        ];

        private static readonly FloraPlacementEntry[] DesertEntries =
        [
            new() { Block = BlockType.DeadBush, Weight = 55, SampleThreshold = 0.42f, HashMod = 2 },
            new() { Block = BlockType.Cactus, Weight = 30, SampleThreshold = 0.72f, HashMod = 9, RequiresCactus = true },
            new() { Block = BlockType.DeadBush, Weight = 15, SampleThreshold = 0.38f, HashMod = 1 }
        ];

        private static readonly FloraPlacementEntry[] BeachEntries =
        [
            new() { Block = BlockType.DeadBush, Weight = 45, SampleThreshold = 0.40f, HashMod = 2 },
            new() { Block = BlockType.DeadBush, Weight = 25, SampleThreshold = 0.36f, HashMod = 1 }
        ];

        private static readonly FloraPlacementEntry[] SwampEntries =
        [
            new() { Block = BlockType.Reed, Weight = 22, SampleThreshold = 0.50f, HashMod = 4 },
            new() { Block = BlockType.MushroomRed, Weight = 8, SampleThreshold = 0.48f, HashMod = 19, UnderstoryOnly = true },
            new() { Block = BlockType.MushroomBrown, Weight = 8, SampleThreshold = 0.48f, HashMod = 23, UnderstoryOnly = true },
            new() { Block = BlockType.Fern, Weight = 16, SampleThreshold = 0.48f, HashMod = 4, UnderstoryOnly = true },
            new() { Block = BlockType.MossCarpet, Weight = 18, SampleThreshold = 0.44f, HashMod = 3 },
            new() { Block = BlockType.Lichen, Weight = 6, SampleThreshold = 0.50f, HashMod = 9 },
            new() { Block = BlockType.WildRose, Weight = 8, SampleThreshold = 0.50f, HashMod = 5 }
        ];

        private static readonly FloraPlacementEntry[] SnowyPeaksEntries =
        [
            new() { Block = BlockType.Heather, Weight = 55, SampleThreshold = 0.65f, HashMod = 5 },
            new() { Block = BlockType.Juniper, Weight = 35, SampleThreshold = 0.65f, HashMod = 5 },
            new() { Block = BlockType.Lichen, Weight = 10, SampleThreshold = 0.62f, HashMod = 7 }
        ];

        private static readonly FloraPlacementEntry[] MountainsEntries =
        [
            new() { Block = BlockType.Lichen, Weight = 40, SampleThreshold = 0.48f, HashMod = 3 },
            new() { Block = BlockType.Heather, Weight = 25, SampleThreshold = 0.58f, HashMod = 5 },
            new() { Block = BlockType.Juniper, Weight = 15, SampleThreshold = 0.62f, HashMod = 7 },
            new() { Block = BlockType.DeadBush, Weight = 12, SampleThreshold = 0.52f, HashMod = 9 }
        ];

        private static readonly FloraPlacementEntry[] GenericEntries =
        [
            new() { Block = BlockType.TallGrass, Weight = 50, SampleThreshold = 0.45f }
        ];

        public static bool TryPick(
            IReadOnlyList<FloraPlacementEntry> entries,
            BiomeProfile profile,
            float floraSample,
            int hash,
            bool underCanopy,
            out FloraPlacementEntry picked)
        {
            picked = default;
            int totalWeight = 0;
            Span<FloraPlacementEntry> candidates = stackalloc FloraPlacementEntry[entries.Count];
            int candidateCount = 0;

            foreach (var entry in entries)
            {
                if (entry.RequiresCactus && !profile.AllowCactus)
                {
                    continue;
                }

                if (entry.RequiresFlowers && !profile.AllowFlowers)
                {
                    continue;
                }

                if (!profile.AllowTallGrass && entry.Block == BlockType.TallGrass)
                {
                    continue;
                }

                if (entry.UnderstoryOnly != underCanopy)
                {
                    continue;
                }

                candidates[candidateCount++] = entry;
                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
            {
                return false;
            }

            int roll = Math.Abs(hash % totalWeight);
            for (int i = 0; i < candidateCount; i++)
            {
                var entry = candidates[i];
                roll -= entry.Weight;
                if (roll >= 0)
                {
                    continue;
                }

                float threshold = entry.SampleThreshold - profile.FloraDensity * 0.12f;
                if (floraSample <= threshold)
                {
                    return false;
                }

                if (entry.HashMod > 0 && !PassesRarityGate(hash, entry.HashMod))
                {
                    return false;
                }

                picked = entry;
                return true;
            }

            return false;
        }

        private static bool PassesRarityGate(int hash, int rarity)
        {
            return Math.Abs(hash % rarity) == 0;
        }
    }
}
