using System.Numerics;

namespace Autonocraft.World
{
    /// <summary>
    /// Neighbor-aware tinting and UV mapping so block textures tile seamlessly across faces.
    /// </summary>
    internal static class BlockTextureBlend
    {
        private static readonly (int Dx, int Dy, int Dz)[] NeighborOffsets =
        {
            (1, 0, 0), (-1, 0, 0),
            (0, 1, 0), (0, -1, 0),
            (0, 0, 1), (0, 0, -1)
        };

        public static Vector3 GetTint(BlockType type)
        {
            type = type.GetBaseBlockType();
            return type switch
            {
                BlockType.Grass => new Vector3(0.86f, 0.94f, 0.82f),
                BlockType.Dirt => new Vector3(1.02f, 0.96f, 0.88f),
                BlockType.Sand => new Vector3(1.06f, 1.02f, 0.92f),
                BlockType.RedSand => new Vector3(1.08f, 0.94f, 0.88f),
                BlockType.Snow => new Vector3(1.05f, 1.05f, 1.08f),
                BlockType.Stone => new Vector3(0.97f, 0.97f, 0.99f),
                BlockType.Gravel => new Vector3(0.98f, 0.98f, 0.96f),
                BlockType.Water => new Vector3(0.92f, 0.98f, 1.06f),
                BlockType.OakLeaves => new Vector3(0.86f, 0.96f, 0.82f),
                BlockType.BirchLeaves => new Vector3(0.88f, 0.96f, 0.84f),
                BlockType.PineLeaves => new Vector3(0.82f, 0.93f, 0.84f),
                BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog
                    or BlockType.WillowLog or BlockType.PalmLog => new Vector3(1.02f, 0.98f, 0.94f),
                BlockType.WillowLeaves => new Vector3(0.84f, 0.95f, 0.86f),
                BlockType.PalmLeaves => new Vector3(0.88f, 0.98f, 0.84f),
                BlockType.BirchPlank => new Vector3(1.04f, 1.02f, 0.96f),
                BlockType.PinePlank => new Vector3(1.02f, 0.96f, 0.88f),
                BlockType.Cobblestone => new Vector3(0.96f, 0.96f, 0.98f),
                BlockType.Brick => new Vector3(1.02f, 0.94f, 0.90f),
                BlockType.MossStone => new Vector3(0.94f, 1.02f, 0.92f),
                BlockType.Mud => new Vector3(0.98f, 0.94f, 0.88f),
                BlockType.HayBale => new Vector3(1.04f, 0.98f, 0.86f),
                BlockType.Ice => new Vector3(0.94f, 1.02f, 1.06f),
                BlockType.CoalOre or BlockType.IronOre or BlockType.GoldOre => new Vector3(0.99f, 0.99f, 1.0f),
                BlockType.OakPlank => new Vector3(1.02f, 0.96f, 0.88f),
                BlockType.Glass => new Vector3(0.92f, 0.98f, 1.04f),
                BlockType.Clay => new Vector3(1.02f, 0.94f, 0.88f),
                BlockType.Sandstone => new Vector3(1.04f, 0.98f, 0.90f),
                BlockType.IronBlock or BlockType.GoldBlock => new Vector3(0.98f, 0.98f, 1.0f),
                BlockType.StationBench or BlockType.StationForge or BlockType.StationCrucible => new Vector3(1.0f, 0.98f, 0.94f),
                BlockType.Cactus => new Vector3(0.92f, 1.05f, 0.90f),
                _ => Vector3.One
            };
        }

        public static float GetSmoothedVariation(MeshBuildContext context, int wx, int wy, int wz)
        {
            BlockType center = context.GetBlock(wx, wy, wz);
            float sum = SampleVariation(wx, wy, wz, context.Seed);
            int count = 1;

            foreach (var (dx, dy, dz) in NeighborOffsets)
            {
                BlockType neighbor = context.GetBlock(wx + dx, wy + dy, wz + dz);
                if (!SharesMaterial(neighbor, center))
                {
                    continue;
                }

                sum += SampleVariation(wx + dx, wy + dy, wz + dz, context.Seed);
                count++;
            }

            return sum / count;
        }

        public static Vector2 ComputeWorldAlignedUv(
            Vector3 blockPos,
            Vector3 cornerOffset,
            Vector3 normal,
            float uMin,
            float vMin,
            float uMax,
            float vMax)
        {
            float du = uMax - uMin;
            float dv = vMax - vMin;
            float vx = blockPos.X + cornerOffset.X;
            float vy = blockPos.Y + cornerOffset.Y;
            float vz = blockPos.Z + cornerOffset.Z;
            float bx = blockPos.X;
            float by = blockPos.Y;
            float bz = blockPos.Z;

            if (normal.Y > 0.5f)
            {
                return new Vector2(uMin + (vx - bx) * du, vMin + (vz - bz) * dv);
            }

            if (normal.Y < -0.5f)
            {
                return new Vector2(uMin + (vx - bx) * du, vMin + (1f - (vz - bz)) * dv);
            }

            if (normal.X > 0.5f)
            {
                return new Vector2(uMin + (1f - (vz - bz)) * du, vMin + (1f - (vy - by)) * dv);
            }

            if (normal.X < -0.5f)
            {
                return new Vector2(uMin + (vz - bz) * du, vMin + (1f - (vy - by)) * dv);
            }

            if (normal.Z > 0.5f)
            {
                return new Vector2(uMin + (1f - (vx - bx)) * du, vMin + (1f - (vy - by)) * dv);
            }

            return new Vector2(uMin + (vx - bx) * du, vMin + (1f - (vy - by)) * dv);
        }

        public static Vector3 ComputeCornerColor(
            MeshBuildContext context,
            int wx,
            int wy,
            int wz,
            BlockType blockType,
            int cx,
            int cy,
            int cz,
            Vector3 normal,
            float smoothedVariation,
            float ao)
        {
            BlockType effectiveType = blockType.GetBaseBlockType();
            if (effectiveType == BlockType.Grass && System.MathF.Abs(normal.Y) < 0.1f)
            {
                effectiveType = cy == 1 ? BlockType.Grass : context.GetBlock(wx, wy, wz).GetBaseBlockType();
            }
            else if (effectiveType == BlockType.SnowSide && System.MathF.Abs(normal.Y) < 0.1f)
            {
                effectiveType = cy == 1 ? BlockType.Snow : context.GetBlock(wx, wy, wz).GetBaseBlockType();
            }

            Vector3 tintSum = GetTint(effectiveType);
            int tintCount = 1;
            AccumulateNeighborTints(context, wx, wy, wz, effectiveType, cx, cy, cz, normal, ref tintSum, ref tintCount);

            Vector3 blendedTint = tintSum / tintCount;
            float shade = smoothedVariation * ao * GetFaceShade(normal);
            Vector3 result = blendedTint * shade;
            if (blockType.IsWater())
            {
                result.Y = 2f;
            }
            else if (blockType == BlockType.Glass)
            {
                result.Y = 4f;
            }
            else if (blockType.IsAlphaCutout())
            {
                result.Y = 3f;
            }

            return result;
        }

        private static float GetFaceShade(Vector3 normal)
        {
            // Top faces must not exceed 1.0 so elevated blocks don't appear self-illuminated.
            if (normal.Y > 0.5f)
            {
                return 0.96f;
            }

            if (normal.Y < -0.5f)
            {
                return 0.66f;
            }

            if (MathF.Abs(normal.X) > 0.5f)
            {
                return 0.83f;
            }

            return 0.88f;
        }

        private static void AccumulateNeighborTints(
            MeshBuildContext context,
            int wx,
            int wy,
            int wz,
            BlockType self,
            int cx,
            int cy,
            int cz,
            Vector3 normal,
            ref Vector3 tintSum,
            ref int tintCount)
        {
            if (MathF.Abs(normal.Y) > 0.5f)
            {
                if (cx == 0)
                {
                    AddNeighborTint(context, wx - 1, wy, wz, self, ref tintSum, ref tintCount);
                }
                else
                {
                    AddNeighborTint(context, wx + 1, wy, wz, self, ref tintSum, ref tintCount);
                }

                if (cz == 0)
                {
                    AddNeighborTint(context, wx, wy, wz - 1, self, ref tintSum, ref tintCount);
                }
                else
                {
                    AddNeighborTint(context, wx, wy, wz + 1, self, ref tintSum, ref tintCount);
                }

                return;
            }

            if (MathF.Abs(normal.X) > 0.5f)
            {
                if (cy == 0)
                {
                    AddNeighborTint(context, wx, wy - 1, wz, self, ref tintSum, ref tintCount);
                }
                else
                {
                    AddNeighborTint(context, wx, wy + 1, wz, self, ref tintSum, ref tintCount);
                }

                if (cz == 0)
                {
                    AddNeighborTint(context, wx, wy, wz - 1, self, ref tintSum, ref tintCount);
                }
                else
                {
                    AddNeighborTint(context, wx, wy, wz + 1, self, ref tintSum, ref tintCount);
                }

                return;
            }

            if (cx == 0)
            {
                AddNeighborTint(context, wx - 1, wy, wz, self, ref tintSum, ref tintCount);
            }
            else
            {
                AddNeighborTint(context, wx + 1, wy, wz, self, ref tintSum, ref tintCount);
            }

            if (cy == 0)
            {
                AddNeighborTint(context, wx, wy - 1, wz, self, ref tintSum, ref tintCount);
            }
            else
            {
                AddNeighborTint(context, wx, wy + 1, wz, self, ref tintSum, ref tintCount);
            }
        }

        private static void AddNeighborTint(
            MeshBuildContext context,
            int nx,
            int ny,
            int nz,
            BlockType self,
            ref Vector3 tintSum,
            ref int tintCount)
        {
            BlockType neighbor = context.GetBlock(nx, ny, nz);
            if (neighbor == BlockType.Air || neighbor.IsTransparent())
            {
                return;
            }

            tintSum += GetTint(neighbor);
            tintCount++;
        }

        private static bool SharesMaterial(BlockType a, BlockType b)
        {
            a = a.GetBaseBlockType();
            b = b.GetBaseBlockType();
            if (a == b)
            {
                return true;
            }

            return IsEarthMaterial(a) && IsEarthMaterial(b);
        }

        private static bool IsEarthMaterial(BlockType type)
        {
            return type is BlockType.Grass or BlockType.Dirt or BlockType.Sand or BlockType.RedSand or BlockType.Snow
                or BlockType.Gravel or BlockType.Mud;
        }

        private static float SampleVariation(int wx, int wy, int wz, int seed)
        {
            unchecked
            {
                uint hash = (uint)(wx * 734287 + wy * 912271 + wz * 438289 + seed);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash = (hash ^ (hash >> 16)) & 255u;
                return 0.94f + hash / 255f * 0.10f;
            }
        }
    }
}
