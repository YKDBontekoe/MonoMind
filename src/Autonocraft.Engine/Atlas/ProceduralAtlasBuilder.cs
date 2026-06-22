using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace Autonocraft.Engine
{
    public sealed partial class ProceduralAtlasBuilder
    {
        private readonly World.AtlasLayout _layout;
        private readonly int _tileSize;
        private readonly int _gridCols;
        private readonly int _gridRows;
        private readonly int _paletteSeed;

        public ProceduralAtlasBuilder(int paletteSeed = 0)
            : this(World.BlockAtlas.LayoutData, paletteSeed)
        {
        }

        public ProceduralAtlasBuilder(World.AtlasLayout layout, int paletteSeed = 0)
        {
            _layout = layout;
            _tileSize = layout.TileSize;
            _gridCols = layout.GridCols;
            _gridRows = layout.GridRows;
            _paletteSeed = paletteSeed;
        }

        public static Texture2D Generate(GraphicsDevice device, int paletteSeed = 0)
        {
            var builder = new ProceduralAtlasBuilder(paletteSeed);
            return builder.Build(device);
        }

        public static Texture2D LoadOrGenerate(GraphicsDevice device, int paletteSeed = 0)
        {
            if (paletteSeed != 0)
            {
                return Generate(device, paletteSeed);
            }

            string path = Path.Combine(AppContext.BaseDirectory, "atlas.png");
            if (!File.Exists(path))
            {
                return Generate(device, paletteSeed);
            }

            try
            {
                using FileStream stream = File.OpenRead(path);
                var layout = World.BlockAtlas.LayoutData;
                var texture = Texture2D.FromStream(device, stream);
                if (texture.Width == (int)layout.AtlasWidth && texture.Height == (int)layout.AtlasHeight)
                {
                    return texture;
                }

                texture.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Atlas] Failed to load atlas.png, using procedural fallback: {ex.Message}");
            }

            return Generate(device, paletteSeed);
        }

        public Texture2D Build(GraphicsDevice device)
        {
            var pixels = BuildPixels();
            int width = _gridCols * _tileSize;
            int height = _gridRows * _tileSize;
            var atlas = new Texture2D(device, width, height);
            atlas.SetData(pixels);
            return atlas;
        }

        /// <summary>
        /// Builds the full atlas pixel buffer without a graphics device (for offline atlas export).
        /// </summary>
        public Color[] BuildPixels()
        {
            int width = _gridCols * _tileSize;
            int height = _gridRows * _tileSize;
            var pixels = new Color[width * height];

            foreach (var entry in _layout.Tiles.OrderBy(e => e.Value.Row).ThenBy(e => e.Value.Col).ThenBy(e => e.Key))
            {
                var tilePixels = GenerateTile(entry.Value.File);
                int originX = entry.Value.Col * _tileSize;
                int originY = entry.Value.Row * _tileSize;
                for (int y = 0; y < _tileSize; y++)
                {
                    Array.Copy(
                        tilePixels,
                        y * _tileSize,
                        pixels,
                        (originY + y) * width + originX,
                        _tileSize);
                }
            }

            return pixels;
        }

        private Color[] GenerateTile(string filename)
        {
            Image? image = MakeProceduralTile(filename);
            if (image == null)
            {
                image = FillSolid(new Color(180, 80, 180));
            }

            return image.Pixels;
        }

        private Image FillSolid(Color color)
        {
            var pixels = new Color[_tileSize * _tileSize];
            Array.Fill(pixels, color);
            return new Image(pixels);
        }

        private Image ComposeGrassSide(Image dirt, Image grassFringe)
        {
            var result = dirt.Clone();
            int baseFringeHeight = Math.Max(1, _tileSize * 43 / 100);
            for (int x = 0; x < _tileSize; x++)
            {
                int overhang = NoiseValue("grass_side_overhang", x / 4, _tileSize, 5) % (_tileSize / 9);
                int fringeHeight = Math.Clamp(baseFringeHeight + overhang - _tileSize / 18, _tileSize / 5, _tileSize * 3 / 5);
                for (int y = 0; y < fringeHeight; y++)
                {
                    int srcY = y * grassFringe.Height / fringeHeight;
                    Color fringeColor = grassFringe.Pixels[srcY * grassFringe.Width + x];
                    if (fringeColor.A > 0)
                    {
                        int noise = NoiseValue("grass_side_blend", x, y, 11) % 9 - 4;
                        result.Pixels[y * _tileSize + x] = Shade(fringeColor, noise);
                    }
                }

                int shadowY = Math.Min(_tileSize - 1, fringeHeight + 1);
                for (int sy = shadowY; sy < Math.Min(_tileSize, shadowY + 5); sy++)
                {
                    int idx = sy * _tileSize + x;
                    result.Pixels[idx] = Shade(result.Pixels[idx], -18 + (sy - shadowY) * 3);
                }
            }

            return result;
        }

        private Image ComposeSnowSide(Image dirt, Image snowFringe)
        {
            var result = dirt.Clone();
            int baseFringeHeight = Math.Max(1, _tileSize * 40 / 100);
            for (int x = 0; x < _tileSize; x++)
            {
                int drift = NoiseValue("snow_side_drift", x / 5, _tileSize, 7) % (_tileSize / 7);
                int fringeHeight = Math.Clamp(baseFringeHeight + drift - _tileSize / 20, _tileSize / 5, _tileSize * 3 / 5);
                for (int y = 0; y < fringeHeight; y++)
                {
                    int srcY = y * snowFringe.Height / fringeHeight;
                    Color fringeColor = snowFringe.Pixels[srcY * snowFringe.Width + x];
                    if (fringeColor.A > 0)
                    {
                        int glint = NoiseValue("snow_side_glint", x, y, 13) % 10;
                        result.Pixels[y * _tileSize + x] = Shade(fringeColor, glint == 0 ? 18 : -2);
                    }
                }

                int shadowY = Math.Min(_tileSize - 1, fringeHeight + 1);
                for (int sy = shadowY; sy < Math.Min(_tileSize, shadowY + 4); sy++)
                {
                    int idx = sy * _tileSize + x;
                    result.Pixels[idx] = Shade(result.Pixels[idx], -12 + (sy - shadowY) * 3);
                }
            }

            return result;
        }

        // Pre-baked water animation frames: computed once at startup, indexed by frame 0-255.
        // Avoids both per-frame CPU computation and per-frame atlas.SetData Rectangle calls
        // which cause GPU pipeline stalls on macOS (OpenGL -> Metal translation layer).
        private static Color[][]? _waterFrameCache;
        private static readonly object _waterCacheLock = new();

        private static Color[][] EnsureWaterFrameCache(int tileSize)
        {
            if (_waterFrameCache != null)
            {
                return _waterFrameCache;
            }

            lock (_waterCacheLock)
            {
                if (_waterFrameCache != null)
                {
                    return _waterFrameCache;
                }

                var cache = new Color[256][];
                for (int f = 0; f < 256; f++)
                {
                    cache[f] = ProceduralTextureSynth.Water(tileSize, $"water.png#{f}", f);
                }

                _waterFrameCache = cache;
                return cache;
            }
        }

        /// <summary>
        /// Eagerly pre-generates all 256 water animation frames. Safe to call from a background thread.
        /// </summary>
        public static void PreWarmWaterCache()
        {
            var layout = World.BlockAtlas.LayoutData;
            EnsureWaterFrameCache(layout.TileSize);
        }

        public static void UpdateWaterTile(Texture2D atlas, float time, int tileSize)
        {
            var layout = World.BlockAtlas.LayoutData;
            var waterTile = layout.GetTile("water");
            var cache = EnsureWaterFrameCache(tileSize);
            int frame = (int)(time * 8f) & 255;
            var pixels = cache[frame];
            int x = waterTile.Col * tileSize;
            int y = waterTile.Row * tileSize;
            atlas.SetData(0, new Rectangle(x, y, tileSize, tileSize), pixels, 0, pixels.Length);
        }

        public Color[] GenerateWaterPixels(float time)
        {
            int frame = (int)(time * 8f) & 255;
            var cache = EnsureWaterFrameCache(_tileSize);
            return cache[frame];
        }

        private static Image FromSynth(Color[] pixels) => new(pixels);



    }
}
