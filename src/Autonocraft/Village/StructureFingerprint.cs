using System;
using System.Collections.Generic;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Village
{
    public static class StructureFingerprint
    {
        public const float MinMatchRatio = 0.8f;

        public static bool TryMatchWorldStructure(
            VoxelWorld world,
            int anchorX,
            int anchorY,
            int anchorZ,
            out StructureDefinition matched,
            out float matchRatio)
        {
            matched = StructureRegistry.All[0];
            matchRatio = 0f;
            float bestRatio = 0f;
            StructureDefinition? best = null;

            foreach (var definition in StructureRegistry.All)
            {
                float ratio = ComputeMatchRatio(world, anchorX, anchorY, anchorZ, definition.Template);
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

        public static float ComputeMatchRatio(
            VoxelWorld world,
            int anchorX,
            int anchorY,
            int anchorZ,
            StructureTemplate template)
        {
            if (template.Blocks.Length == 0)
            {
                return 0f;
            }

            int matches = 0;
            foreach (var block in template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                if (world.GetBlock(wx, wy, wz) == block.Type)
                {
                    matches++;
                }
            }

            return matches / (float)template.Blocks.Length;
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
            
            // Scan down to bypass tree logs/leaves or other non-ground blocks
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
