using System.Numerics;
using Autonocraft.Items;

namespace Autonocraft.World
{
    public static class BlockAtlas
    {
        private static readonly AtlasLayout Layout = AtlasLayout.Load();

        public static float GridWidth => Layout.GridWidth;
        public static float GridHeight => Layout.GridHeight;
        public static float AtlasWidth => Layout.AtlasWidth;
        public static float AtlasHeight => Layout.AtlasHeight;

        public static AtlasLayout LayoutData => Layout;
        public static bool UseCpuBlockVariation { get; set; } = true;

        public static (float uMin, float vMin, float uMax, float vMax) GetFaceUVs(BlockType type, Vector3 normal)
        {
            string tileId = Layout.ResolveBlockTile(type, normal);
            return Layout.GetTileUvs(tileId);
        }

        public static (float uMin, float vMin, float uMax, float vMax) GetTileUVs(string tileId)
        {
            return Layout.GetTileUvs(tileId);
        }

        public static (float uMin, float vMin, float uMax, float vMax) GetTileUVs(int col, int row)
        {
            return Layout.GetTileUvs(col, row);
        }

        public static (float uMin, float vMin, float uMax, float vMax) GetToolUVs(ItemId toolId)
        {
            string tileId = ToolRegistry.GetAtlasTileId(toolId);
            if (Layout.Tiles.ContainsKey(tileId))
            {
                return Layout.GetTileUvs(tileId);
            }

            return Layout.GetTileUvs("tool_wood_pickaxe");
        }

        public static (float uMin, float vMin, float uMax, float vMax) GetWaterTileUvs()
        {
            return Layout.GetTileUvs("water");
        }
    }
}
