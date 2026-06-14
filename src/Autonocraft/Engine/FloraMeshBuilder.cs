using System;
using System.Collections.Generic;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
using Vector2 = System.Numerics.Vector2;

namespace Autonocraft.Engine
{
    internal static class FloraMeshBuilder
    {
        public static void Build(Chunk chunk, List<Vertex> vertices, List<uint> indices)
        {
            int worldOffsetX = chunk.ChunkX * Chunk.Width;
            int worldOffsetZ = chunk.ChunkZ * Chunk.Depth;

            for (int x = 0; x < Chunk.Width; x++)
            {
                for (int z = 0; z < Chunk.Depth; z++)
                {
                    for (int y = Chunk.Height - 1; y >= 0; y--)
                    {
                        BlockType type = chunk.GetBlock(x, y, z);
                        if (type.IsFloraModel())
                        {
                            AddCrossBillboard(
                                vertices,
                                indices,
                                worldOffsetX + x,
                                y,
                                worldOffsetZ + z,
                                type);
                        }
                        else if (type != BlockType.Air)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private static void AddCrossBillboard(
            List<Vertex> vertices,
            List<uint> indices,
            int wx,
            int wy,
            int wz,
            BlockType type)
        {
            float cx = wx + 0.5f;
            float cz = wz + 0.5f;
            float baseY = wy;
            string tileId = type switch
            {
                BlockType.Sunflower => "sunflower",
                BlockType.Flower => "flower",
                BlockType.Reed => "reed",
                BlockType.Cactus => "cactus",
                _ => "tall_grass"
            };
            var uv = BlockAtlas.GetTileUVs(tileId);

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
                _ => 0.88f
            };

            var p0 = new Vector3(cx - halfW, baseY, cz);
            var p1 = new Vector3(cx + halfW, baseY, cz);
            var p2 = new Vector3(cx + halfW, baseY + height, cz);
            var p3 = new Vector3(cx - halfW, baseY + height, cz);
            var normal = new Vector3(0f, 1f, 0f);
            var bottomTint = new Vector3(0.86f, 0.90f, 0.86f);
            var topTint = new Vector3(1.02f, 1.04f, 1.0f);
            AddQuad(vertices, indices, p0, p1, p2, p3, normal, bottomTint, topTint, uv);

            p0 = new Vector3(cx, baseY, cz - halfW);
            p1 = new Vector3(cx, baseY, cz + halfW);
            p2 = new Vector3(cx, baseY + height, cz + halfW);
            p3 = new Vector3(cx, baseY + height, cz - halfW);
            AddQuad(vertices, indices, p0, p1, p2, p3, normal, bottomTint, topTint, uv);
        }

        private static float CactusHeight(int wx, int wz)
        {
            int hash = wx * 734287 ^ wz * 912271;
            return 1.65f + Math.Abs(hash) % 3 * 0.55f;
        }

        private static void AddQuad(
            List<Vertex> vertices,
            List<uint> indices,
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            Vector3 normal,
            Vector3 colorBottom,
            Vector3 colorTop,
            (float uMin, float vMin, float uMax, float vMax) uv)
        {
            uint start = (uint)vertices.Count;
            vertices.Add(new Vertex(p0, colorBottom, normal, new Vector2(uv.uMin, uv.vMax)));
            vertices.Add(new Vertex(p1, colorBottom, normal, new Vector2(uv.uMax, uv.vMax)));
            vertices.Add(new Vertex(p2, colorTop, normal, new Vector2(uv.uMax, uv.vMin)));
            vertices.Add(new Vertex(p3, colorTop, normal, new Vector2(uv.uMin, uv.vMin)));

            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }
    }
}
