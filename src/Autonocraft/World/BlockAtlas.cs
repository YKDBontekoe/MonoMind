using System.Numerics;

namespace Autonocraft.World
{
    public static class BlockAtlas
    {
        public const float GridWidth = 8f;
        public const float GridHeight = 8f;
        public const float AtlasWidth = 1024f;
        public const float AtlasHeight = 1024f;

        public static (float uMin, float vMin, float uMax, float vMax) GetFaceUVs(BlockType type, Vector3 normal)
        {
            int col = 0;
            int row = 0;

            switch (type)
            {
                case BlockType.Grass:
                    if (normal.Y > 0.5f) { col = 0; row = 0; }
                    else if (normal.Y < -0.5f) { col = 0; row = 1; }
                    else { col = 1; row = 0; }
                    break;
                case BlockType.OakLog:
                    col = 2; row = 0;
                    break;
                case BlockType.Stone:
                    col = 3; row = 0;
                    break;
                case BlockType.Dirt:
                    col = 0; row = 1;
                    break;
                case BlockType.OakLeaves:
                    col = 1; row = 1;
                    break;
                case BlockType.BirchLog:
                    col = 2; row = 1;
                    break;
                case BlockType.BirchLeaves:
                    col = 3; row = 1;
                    break;
                case BlockType.PineLog:
                    col = 0; row = 2;
                    break;
                case BlockType.PineLeaves:
                    col = 1; row = 2;
                    break;
                case BlockType.Water:
                    col = 2; row = 2;
                    break;
                case BlockType.Sand:
                    col = 3; row = 2;
                    break;
                case BlockType.Snow:
                    col = 0; row = 3;
                    break;
                case BlockType.Gravel:
                    col = 1; row = 3;
                    break;
                case BlockType.CoalOre:
                    col = 2; row = 3;
                    break;
                case BlockType.IronOre:
                    col = 3; row = 3;
                    break;
                case BlockType.GoldOre:
                    col = 0; row = 4;
                    break;
                case BlockType.TallGrass:
                    col = 1; row = 4;
                    break;
                case BlockType.Flower:
                    col = 2; row = 4;
                    break;
                case BlockType.Cactus:
                    if (normal.Y > 0.5f || normal.Y < -0.5f) { col = 3; row = 4; }
                    else { col = 2; row = 5; }
                    break;
            }

            float uMin = col / GridWidth;
            float vMin = row / GridHeight;
            float uMax = uMin + 1f / GridWidth;
            float vMax = vMin + 1f / GridHeight;

            float halfPixelU = 0.5f / AtlasWidth;
            float halfPixelV = 0.5f / AtlasHeight;
            return (uMin + halfPixelU, vMin + halfPixelV, uMax - halfPixelU, vMax - halfPixelV);
        }
    }
}
