using System;

namespace Autonocraft.World.Generation.Trees
{
    public static class TreeSpeciesRegistry
    {
        public static TreeSpecies PickSpecies(BiomeSample biome, int wx, int wz, int seed)
        {
            int treeTypeRand = Math.Abs((wx * 73 + wz * 101 + seed) % 100);

            if (biome.Primary == BiomeType.Swamp || biome.Primary == BiomeType.Mangrove)
            {
                return TreeSpecies.Willow();
            }

            if (biome.Primary is BiomeType.Desert or BiomeType.Beach or BiomeType.Badlands)
            {
                return TreeSpecies.Palm();
            }

            if (biome.Primary == BiomeType.BorealTaiga)
            {
                if (treeTypeRand < 72)
                {
                    return TreeSpecies.Pine();
                }

                if (treeTypeRand < 88)
                {
                    return TreeSpecies.Birch();
                }

                return TreeSpecies.Maple();
            }

            if (biome.Primary == BiomeType.SnowyPeaks || biome.Temperature < -0.05f)
            {
                return TreeSpecies.Pine();
            }

            if (biome.Primary == BiomeType.MushroomForest)
            {
                if (treeTypeRand < 34)
                {
                    return TreeSpecies.Birch();
                }

                if (treeTypeRand < 66)
                {
                    return TreeSpecies.Maple();
                }

                if (treeTypeRand < 82)
                {
                    return TreeSpecies.Cherry();
                }

                return TreeSpecies.Oak();
            }

            if (biome.Primary == BiomeType.Plains && treeTypeRand < 15)
            {
                return TreeSpecies.Cherry();
            }

            if (biome.Primary == BiomeType.Forest)
            {
                if (treeTypeRand < 25)
                {
                    return TreeSpecies.Mahogany();
                }

                if (treeTypeRand < 50)
                {
                    return TreeSpecies.Maple();
                }

                if (treeTypeRand < 75)
                {
                    return TreeSpecies.Birch();
                }

                return TreeSpecies.Oak();
            }

            if (biome.Primary == BiomeType.Jungle)
            {
                if (treeTypeRand < 45)
                {
                    return TreeSpecies.Mahogany();
                }

                if (treeTypeRand < 65)
                {
                    return TreeSpecies.Palm();
                }

                return TreeSpecies.Oak();
            }

            if (treeTypeRand < 33)
            {
                return TreeSpecies.Birch();
            }

            if (treeTypeRand < 66)
            {
                return TreeSpecies.Pine();
            }

            return TreeSpecies.Oak();
        }

        public static TreeSpecies PickMegaSpecies(BiomeSample biome, int wx, int wz, int seed)
        {
            if (biome.Primary == BiomeType.Swamp)
            {
                return TreeSpecies.Willow();
            }

            if (biome.Primary == BiomeType.Plains)
            {
                return TreeSpecies.Cherry();
            }

            int roll = Math.Abs((wx * 113 + wz * 157 + seed) % 100);
            return roll < 50 ? TreeSpecies.Mahogany() : TreeSpecies.Oak();
        }
    }
}
