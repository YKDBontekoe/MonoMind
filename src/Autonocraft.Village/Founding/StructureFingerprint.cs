using System;
using System.Collections.Generic;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Village
{
    public static class StructureFingerprint
    {
        public const float MinMatchRatio = 0.8f;
        private static readonly int[] PlacementSalts = { 11, 29, 53, 71 };

        public static bool TryMatchWorldStructure(
            VoxelWorld world,
            int anchorX,
            int anchorY,
            int anchorZ,
            out StructureDefinition matched,
            out float matchRatio,
            IReadOnlyList<StructureDefinition>? candidates = null,
            int maxBlocksToCheck = 0)
        {
            matched = StructureRegistry.All[0];
            matchRatio = 0f;
            float bestRatio = 0f;
            StructureDefinition? best = null;
            var biome = world.SampleBiome(anchorX, anchorZ).Primary;
            candidates ??= StructureRegistry.All;

            foreach (var definition in candidates)
            {
                float ratio = BestRatioForDefinition(
                    world, anchorX, anchorY, anchorZ, definition, biome, maxBlocksToCheck);
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    best = definition;
                }
            }

            if (best == null || bestRatio < MinMatchRatio)
            {
                return false;
            }

            matched = best;
            matchRatio = bestRatio;
            return true;
        }

        public static StructureTemplate ResolveTemplateForAnchor(
            VoxelWorld world,
            int anchorX,
            int anchorZ,
            StructureDefinition definition)
        {
            var biome = world.SampleBiome(anchorX, anchorZ).Primary;
            float bestRatio = 0f;
            StructureTemplate bestTemplate = definition.Template;

            foreach (int placementSalt in PlacementSalts)
            {
                int placementHash = StructureHash(anchorX, anchorZ, world.Seed, placementSalt);
                int variantSalt = StructurePlacementKeys.VariantSaltForStructure(
                    world.Seed, anchorX, anchorZ, definition.Id, placementHash);
                var template = definition.ResolveTemplate(world.Seed, anchorX, anchorZ, variantSalt, biome);
                float ratio = ComputeMatchRatio(world, anchorX, FindSurfaceAnchorY(world, anchorX, anchorZ), anchorZ, template);
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    bestTemplate = template;
                }
            }

            if (StructureGallery.IsGalleryWorld(world.GenerationParams.WorldType))
            {
                for (int i = 0; i < StructureRegistry.All.Count; i++)
                {
                    if (StructureRegistry.All[i].Id != definition.Id)
                    {
                        continue;
                    }

                    int variantSalt = StructureGallery.VariantSaltFor(i);
                    var template = definition.ResolveTemplate(world.Seed, anchorX, anchorZ, variantSalt, biome);
                    float ratio = ComputeMatchRatio(world, anchorX, FindSurfaceAnchorY(world, anchorX, anchorZ), anchorZ, template);
                    if (ratio > bestRatio)
                    {
                        bestRatio = ratio;
                        bestTemplate = template;
                    }
                }
            }

            return bestTemplate;
        }

        private static float BestRatioForDefinition(
            VoxelWorld world,
            int anchorX,
            int anchorY,
            int anchorZ,
            StructureDefinition definition,
            BiomeType biome,
            int maxBlocksToCheck = 0)
        {
            if (StructureGallery.IsGalleryWorld(world.GenerationParams.WorldType))
            {
                for (int i = 0; i < StructureRegistry.All.Count; i++)
                {
                    if (StructureRegistry.All[i].Id != definition.Id)
                    {
                        continue;
                    }

                    int variantSalt = StructureGallery.VariantSaltFor(i);
                    var template = definition.ResolveTemplate(world.Seed, anchorX, anchorZ, variantSalt, biome);
                    return ComputeMatchRatio(world, anchorX, anchorY, anchorZ, template, maxBlocksToCheck);
                }

                return 0f;
            }

            float bestRatio = 0f;

            foreach (int placementSalt in PlacementSalts)
            {
                int placementHash = StructureHash(anchorX, anchorZ, world.Seed, placementSalt);
                int variantSalt = StructurePlacementKeys.VariantSaltForStructure(
                    world.Seed, anchorX, anchorZ, definition.Id, placementHash);
                var template = definition.ResolveTemplate(world.Seed, anchorX, anchorZ, variantSalt, biome);
                float ratio = ComputeMatchRatio(world, anchorX, anchorY, anchorZ, template, maxBlocksToCheck);
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                }
            }

            return bestRatio;
        }

        public static float ComputeMatchRatio(
            VoxelWorld world,
            int anchorX,
            int anchorY,
            int anchorZ,
            StructureTemplate template,
            int maxBlocksToCheck = 0)
        {
            if (template.Blocks.Length == 0)
            {
                return 0f;
            }

            int matches = 0;
            int checkedBlocks = 0;
            int totalBlocks = template.Blocks.Length;
            int checkLimit = maxBlocksToCheck > 0 ? Math.Min(maxBlocksToCheck, totalBlocks) : totalBlocks;
            int earlyRejectThreshold = checkLimit < totalBlocks ? checkLimit / 2 : 0;

            foreach (var block in template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                if (world.GetBlock(wx, wy, wz) == block.Type)
                {
                    matches++;
                }

                checkedBlocks++;
                if (checkLimit < totalBlocks)
                {
                    if (checkedBlocks == earlyRejectThreshold && matches * 2 < checkedBlocks)
                    {
                        return 0f;
                    }

                    if (checkedBlocks >= checkLimit)
                    {
                        break;
                    }
                }
            }

            return matches / (float)totalBlocks;
        }

        public static bool TryMatchBlueprint(
            VoxelWorld world,
            int anchorX,
            int anchorY,
            int anchorZ,
            BuildingBlueprint blueprint,
            out float matchRatio)
        {
            matchRatio = ComputeMatchRatio(world, anchorX, anchorY, anchorZ, blueprint.Template);
            return matchRatio >= MinMatchRatio;
        }

        private static int StructureHash(int wx, int wz, int seed, int salt)
        {
            unchecked
            {
                int h = wx * 92821 + wz * 68917 + seed + salt;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return Math.Abs(h);
            }
        }

        public static int StructureHashForTests(int wx, int wz, int seed, int salt) => StructureHash(wx, wz, seed, salt);

        private static bool IsNaturalGround(BlockType type)
        {
            return type == BlockType.Grass || type == BlockType.Dirt || type == BlockType.Stone ||
                   type == BlockType.Sand || type == BlockType.Snow || type == BlockType.Gravel ||
                   type == BlockType.Clay || type == BlockType.Sandstone || type == BlockType.Cobblestone ||
                   type == BlockType.Mud || type == BlockType.MossStone || type == BlockType.Ice ||
                   type == BlockType.CoalOre || type == BlockType.IronOre || type == BlockType.GoldOre;
        }

        public static int FindSurfaceAnchorY(VoxelWorld world, int anchorX, int anchorZ, int searchRadius = 8)
        {
            int highestY = world.GetHighestSolidY(anchorX, anchorZ);

            for (int y = highestY; y >= 0; y--)
            {
                BlockType type = world.GetBlock(anchorX, y, anchorZ);
                if (IsNaturalGround(type))
                {
                    return y + 1;
                }
            }

            return highestY >= 0 ? highestY + 1 : 65;
        }
    }
}
