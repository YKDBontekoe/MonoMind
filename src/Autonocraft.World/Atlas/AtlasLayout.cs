using System.Numerics;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Autonocraft.Domain.World;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.World
{
    public sealed class AtlasLayout
    {
        public int GridCols { get; init; }
        public int GridRows { get; init; }
        public int TileSize { get; init; }
        public Dictionary<string, TileSlot> Tiles { get; init; } = new();
        public Dictionary<string, BlockFaceMapping> BlockFaces { get; init; } = new();
        public Dictionary<string, AnimalTileMapping> Animals { get; init; } = new();
        public Dictionary<string, AnimalTileMapping> Villagers { get; init; } = new();

        public float AtlasWidth => GridCols * TileSize;
        public float AtlasHeight => GridRows * TileSize;
        public float GridWidth => GridCols;
        public float GridHeight => GridRows;

        public static AtlasLayout Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "atlas_layout.json");
            if (File.Exists(path))
            {
                return LoadFromFile(path);
            }

            using Stream stream = TitleContainer.OpenStream("atlas_layout.json");
            var layout = JsonSerializer.Deserialize<AtlasLayout>(stream, JsonOptions)
                ?? throw new InvalidOperationException("Failed to parse atlas_layout.json");
            layout.Validate();
            return layout;
        }

        public static AtlasLayout LoadFromFile(string path)
        {
            using Stream stream = File.OpenRead(path);
            var layout = JsonSerializer.Deserialize<AtlasLayout>(stream, JsonOptions)
                ?? throw new InvalidOperationException($"Failed to parse atlas layout: {path}");
            layout.Validate();
            return layout;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private void Validate()
        {
            if (GridCols <= 0 || GridRows <= 0 || TileSize <= 0)
            {
                throw new InvalidOperationException("Invalid atlas grid dimensions.");
            }

            if (Tiles.Count == 0)
            {
                throw new InvalidOperationException("atlas_layout.json must define at least one tile.");
            }

            var seenSlots = new HashSet<(int Col, int Row)>();
            foreach (var (tileId, slot) in Tiles)
            {
                if (string.IsNullOrWhiteSpace(slot.File))
                {
                    throw new InvalidOperationException($"Tile '{tileId}' is missing a file name.");
                }

                var coord = (slot.Col, slot.Row);
                if (!seenSlots.Add(coord))
                {
                    throw new InvalidOperationException($"Duplicate atlas slot at ({slot.Col}, {slot.Row}).");
                }

                if (slot.Col >= GridCols || slot.Row >= GridRows)
                {
                    throw new InvalidOperationException(
                        $"Tile '{tileId}' slot ({slot.Col}, {slot.Row}) is outside grid bounds.");
                }
            }
        }

        public TileSlot GetTile(string id)
        {
            if (!Tiles.TryGetValue(id, out var tile))
            {
                throw new KeyNotFoundException($"Unknown atlas tile '{id}'.");
            }

            return tile;
        }

        public (float uMin, float vMin, float uMax, float vMax) GetTileUvs(string tileId)
        {
            var tile = GetTile(tileId);
            return GetTileUvs(tile.Col, tile.Row);
        }

        public (float uMin, float vMin, float uMax, float vMax) GetTileUvs(int col, int row)
        {
            float uMin = col / GridWidth;
            float vMin = row / GridHeight;
            float uMax = uMin + 1f / GridWidth;
            float vMax = vMin + 1f / GridHeight;

            float halfPixelU = 0.5f / AtlasWidth;
            float halfPixelV = 0.5f / AtlasHeight;
            return (uMin + halfPixelU, vMin + halfPixelV, uMax - halfPixelU, vMax - halfPixelV);
        }

        public string ResolveBlockTile(BlockType type, Vector3 normal)
        {
            if (type == BlockType.SnowSide)
            {
                return "snow_side";
            }

            string typeName = type.ToString();
            if (typeName.EndsWith("Slab"))
            {
                typeName = typeName.Substring(0, typeName.Length - 4);
            }
            // SnowLayer1..9 all use the Snow texture
            if (typeName.StartsWith("SnowLayer"))
            {
                typeName = "Snow";
            }

            if (!BlockFaces.TryGetValue(typeName, out var mapping))
            {
                return "stone";
            }

            if (normal.Y > 0.5f && mapping.Top != null)
            {
                return mapping.Top;
            }

            if (normal.Y < -0.5f && mapping.Bottom != null)
            {
                return mapping.Bottom;
            }

            if (MathF.Abs(normal.Y) <= 0.5f && mapping.Side != null)
            {
                return mapping.Side;
            }

            return mapping.All ?? mapping.Top ?? mapping.Side ?? mapping.Bottom ?? "stone";
        }

        public sealed class TileSlot
        {
            public int Col { get; init; }
            public int Row { get; init; }
            public string File { get; init; } = string.Empty;
        }

        public sealed class BlockFaceMapping
        {
            public string? All { get; init; }
            public string? Top { get; init; }
            public string? Bottom { get; init; }
            public string? Side { get; init; }
        }

        public sealed class AnimalTileMapping
        {
            public string Body { get; init; } = string.Empty;
            public string Head { get; init; } = string.Empty;
        }
    }
}
