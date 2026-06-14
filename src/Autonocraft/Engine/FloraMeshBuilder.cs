using System;
using System.Collections.Generic;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
using Vector2 = System.Numerics.Vector2;

namespace Autonocraft.Engine
{
    internal static class FloraMeshBuilder
    {
        public static void Build(Chunk chunk, BiomeMap? biomeMap, List<FloraVertex> vertices, List<uint> indices)
        {
            int worldOffsetX = chunk.ChunkX * Chunk.Width;
            int worldOffsetZ = chunk.ChunkZ * Chunk.Depth;
            biomeMap ??= new BiomeMap(chunk.ChunkX * 997 + chunk.ChunkZ * 131, WorldGenParams.ForType(WorldType.Default));

            for (int x = 0; x < Chunk.Width; x++)
            {
                for (int z = 0; z < Chunk.Depth; z++)
                {
                    for (int y = Chunk.Height - 1; y >= 0; y--)
                    {
                        BlockType type = chunk.GetBlock(x, y, z);
                        if (type.IsFloraModel())
                        {
                            int wx = worldOffsetX + x;
                            int wz = worldOffsetZ + z;
                            var biome = biomeMap.Sample(wx, wz);
                            AddFloraBillboard(
                                vertices,
                                indices,
                                wx,
                                y,
                                wz,
                                type,
                                biome);
                        }
                        else if (type != BlockType.Air)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private static void AddFloraBillboard(
            List<FloraVertex> vertices,
            List<uint> indices,
            int wx,
            int wy,
            int wz,
            BlockType type,
            BiomeSample biome)
        {
            int hash = Hash(wx, wz);
            int variant = hash % 4;
            float cx = wx + 0.5f;
            float cz = wz + 0.5f;
            float baseY = wy;
            float windPhase = (hash % 997) / 997f;

            string tileId = type switch
            {
                BlockType.Sunflower => "sunflower",
                BlockType.Flower => "flower",
                BlockType.Reed => "reed",
                BlockType.Cactus => "cactus",
                _ => "tall_grass"
            };
            var tileUv = BlockAtlas.GetTileUVs(tileId);
            var uv = GetVariantUVs(tileUv, variant);

            float halfW = type switch
            {
                BlockType.Sunflower => 0.42f,
                BlockType.Reed => 0.28f,
                BlockType.Flower => 0.32f,
                BlockType.Cactus => 0.30f,
                _ => 0.38f
            };
            float height = type switch
            {
                BlockType.Sunflower => 1.05f,
                BlockType.Reed => 1.15f,
                BlockType.Flower => 0.55f,
                BlockType.Cactus => CactusHeight(wx, wz),
                BlockType.TallGrass when biome.Primary == BiomeType.Forest => 0.62f,
                BlockType.TallGrass when biome.Primary == BiomeType.Swamp => 0.72f,
                _ => 0.88f
            };

            float widthScale = 0.85f + (hash % 31) / 100f;
            float heightScale = 0.9f + (hash % 23) / 115f;
            halfW *= widthScale;
            height *= heightScale;

            float yaw = ((hash % 25) - 12) * (MathF.PI / 180f);
            int crossCount = type switch
            {
                BlockType.Sunflower or BlockType.Reed => 3,
                BlockType.Flower or BlockType.Cactus => 2,
                BlockType.TallGrass => 2,
                _ => 2
            };

            var bottomTint = new Vector3(0.86f, 0.90f, 0.86f);
            var topTint = new Vector3(1.02f, 1.04f, 1.0f);

            for (int cross = 0; cross < crossCount; cross++)
            {
                float angle = yaw + cross * (MathF.PI / crossCount);
                float sin = MathF.Sin(angle);
                float cos = MathF.Cos(angle);
                float dx = halfW * cos;
                float dz = halfW * sin;
                AddQuad(
                    vertices,
                    indices,
                    new Vector3(cx - dx, baseY, cz - dz),
                    new Vector3(cx + dx, baseY, cz + dz),
                    new Vector3(cx + dx, baseY + height, cz + dz),
                    new Vector3(cx - dx, baseY + height, cz - dz),
                    bottomTint,
                    topTint,
                    uv,
                    windPhase);
            }
        }

        private static (float uMin, float vMin, float uMax, float vMax) GetVariantUVs(
            (float uMin, float vMin, float uMax, float vMax) tileUv,
            int variant)
        {
            float du = (tileUv.uMax - tileUv.uMin) * 0.5f;
            float dv = (tileUv.vMax - tileUv.vMin) * 0.5f;
            int col = variant % 2;
            int row = variant / 2;
            return (
                tileUv.uMin + col * du,
                tileUv.vMin + row * dv,
                tileUv.uMin + (col + 1) * du,
                tileUv.vMin + (row + 1) * dv);
        }

        private static float CactusHeight(int wx, int wz)
        {
            int hash = wx * 734287 ^ wz * 912271;
            return 1.65f + Math.Abs(hash) % 3 * 0.55f;
        }

        private static int Hash(int wx, int wz)
        {
            unchecked
            {
                return Math.Abs(wx * 92821 + wz * 68917 + 17);
            }
        }

        private static void AddQuad(
            List<FloraVertex> vertices,
            List<uint> indices,
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            Vector3 colorBottom,
            Vector3 colorTop,
            (float uMin, float vMin, float uMax, float vMax) uv,
            float windPhase)
        {
            uint start = (uint)vertices.Count;
            vertices.Add(new FloraVertex(p0, colorBottom, new Vector2(uv.uMin, uv.vMax), windPhase, 0f));
            vertices.Add(new FloraVertex(p1, colorBottom, new Vector2(uv.uMax, uv.vMax), windPhase, 0f));
            vertices.Add(new FloraVertex(p2, colorTop, new Vector2(uv.uMax, uv.vMin), windPhase, 1f));
            vertices.Add(new FloraVertex(p3, colorTop, new Vector2(uv.uMin, uv.vMin), windPhase, 1f));

            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }
    }
}
