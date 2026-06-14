using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace Autonocraft.Engine
{
    public sealed class ProceduralAtlasBuilder
    {
        private readonly int _tileSize;
        private readonly int _gridCols;
        private readonly int _gridRows;
        private readonly int _paletteSeed;

        public ProceduralAtlasBuilder(int paletteSeed = 0)
        {
            var layout = World.BlockAtlas.LayoutData;
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

        public Texture2D Build(GraphicsDevice device)
        {
            int width = _gridCols * _tileSize;
            int height = _gridRows * _tileSize;
            var atlas = new Texture2D(device, width, height);
            var pixels = new Color[width * height];

            foreach (var entry in World.BlockAtlas.LayoutData.Tiles)
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

            atlas.SetData(pixels);
            return atlas;
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
            int fringeHeight = Math.Max(1, _tileSize * 36 / 100);
            for (int y = 0; y < fringeHeight; y++)
            {
                for (int x = 0; x < _tileSize; x++)
                {
                    int srcY = y * grassFringe.Height / fringeHeight;
                    result.Pixels[y * _tileSize + x] = grassFringe.Pixels[srcY * _tileSize + x];
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

        private Image? MakeProceduralTile(string name)
        {
            int seedShift = ApplyPaletteSeed(name);
            name = SeedName(name, seedShift);

            switch (name.Split('.')[0])
            {
                case "grass_top":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(38, 92, 34), seedShift),
                        ShiftColor(new Color(50, 114, 42), seedShift),
                        ShiftColor(new Color(60, 128, 48), seedShift),
                        ShiftColor(new Color(32, 82, 30), seedShift),
                        ShiftColor(new Color(70, 138, 52), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.GrassTop(_tileSize, name, palette));
                    }
                case "dirt":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(88, 64, 40), seedShift),
                        ShiftColor(new Color(108, 80, 50), seedShift),
                        ShiftColor(new Color(128, 96, 62), seedShift),
                        ShiftColor(new Color(78, 56, 34), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.Dirt(_tileSize, name, palette));
                    }
                case "grass_side":
                    {
                        var dirtPalette = new[]
                        {
                        ShiftColor(new Color(88, 64, 40), seedShift),
                        ShiftColor(new Color(108, 80, 50), seedShift),
                        ShiftColor(new Color(128, 96, 62), seedShift),
                        ShiftColor(new Color(78, 56, 34), seedShift)
                    };
                        var fringePalette = new[]
                        {
                        ShiftColor(new Color(46, 106, 40), seedShift),
                        ShiftColor(new Color(58, 124, 46), seedShift),
                        ShiftColor(new Color(68, 134, 50), seedShift)
                    };
                        var dirt = ProceduralTextureSynth.Dirt(_tileSize, SeedName("dirt.png", seedShift), dirtPalette);
                        var fringe = ProceduralTextureSynth.GrassFringe(_tileSize, name + "_fringe", fringePalette);
                        return ComposeGrassSide(new Image(dirt), new Image(fringe));
                    }
                case "stone":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(96, 96, 100), seedShift),
                        ShiftColor(new Color(118, 118, 122), seedShift),
                        ShiftColor(new Color(142, 142, 148), seedShift),
                        ShiftColor(new Color(108, 108, 112), seedShift),
                        ShiftColor(new Color(84, 84, 88), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.Stone(
                            _tileSize,
                            name,
                            palette,
                            ShiftColor(new Color(72, 72, 76), seedShift)));
                    }
                case "oak_log":
                    return FromSynth(ProceduralTextureSynth.WoodLog(
                        _tileSize,
                        name,
                        ShiftColor(new Color(96, 72, 48), seedShift),
                        ShiftColor(new Color(68, 48, 30), seedShift),
                        ShiftColor(new Color(124, 96, 64), seedShift),
                        12));
                case "birch_log":
                    {
                        var pixels = ProceduralTextureSynth.WoodLog(
                            _tileSize,
                            name,
                            ShiftColor(new Color(210, 205, 188), seedShift),
                            ShiftColor(new Color(184, 178, 162), seedShift),
                            ShiftColor(new Color(236, 232, 218), seedShift),
                            14);
                        var image = new Image(pixels);
                        for (int i = 0; i < 16; i++)
                        {
                            int x = NoiseValue(name, i, 3, 11) % _tileSize;
                            int y = NoiseValue(name, i, 7, 13) % _tileSize;
                            FillRect(image, x, y, 3, 9, new Color(42, 38, 34));
                            SetPixel(image, x + 1, y + 2, new Color(28, 24, 22));
                        }

                        return image;
                    }
                case "pine_log":
                    return FromSynth(ProceduralTextureSynth.WoodLog(
                        _tileSize,
                        name,
                        ShiftColor(new Color(78, 58, 36), seedShift),
                        ShiftColor(new Color(52, 38, 22), seedShift),
                        ShiftColor(new Color(102, 78, 50), seedShift),
                        10));
                case "oak_leaves":
                    return FromSynth(ProceduralTextureSynth.Leaves(
                        _tileSize,
                        name,
                        new[]
                        {
                            ShiftColor(new Color(40, 96, 36), seedShift),
                            ShiftColor(new Color(62, 138, 52), seedShift),
                            ShiftColor(new Color(32, 84, 30), seedShift),
                            ShiftColor(new Color(78, 158, 62), seedShift)
                        }));
                case "birch_leaves":
                    return FromSynth(ProceduralTextureSynth.Leaves(
                        _tileSize,
                        name,
                        new[]
                        {
                            ShiftColor(new Color(56, 122, 48), seedShift),
                            ShiftColor(new Color(84, 158, 68), seedShift),
                            ShiftColor(new Color(46, 106, 42), seedShift),
                            ShiftColor(new Color(96, 172, 78), seedShift)
                        }));
                case "pine_leaves":
                    return FromSynth(ProceduralTextureSynth.Leaves(
                        _tileSize,
                        name,
                        new[]
                        {
                            ShiftColor(new Color(22, 74, 36), seedShift),
                            ShiftColor(new Color(42, 104, 52), seedShift),
                            ShiftColor(new Color(16, 62, 28), seedShift),
                            ShiftColor(new Color(54, 118, 58), seedShift)
                        }));
                case "water":
                    return FromSynth(ProceduralTextureSynth.Water(_tileSize, name));
                case "water_side":
                    return FromSynth(ProceduralTextureSynth.WaterSide(_tileSize, name));
                case "sand":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(180, 162, 102), seedShift),
                        ShiftColor(new Color(210, 196, 132), seedShift),
                        ShiftColor(new Color(236, 222, 158), seedShift),
                        ShiftColor(new Color(194, 176, 118), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.Sand(_tileSize, name, palette));
                    }
                case "snow":
                    return FromSynth(ProceduralTextureSynth.Snow(_tileSize, name));
                case "gravel":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(88, 86, 82), seedShift),
                        ShiftColor(new Color(118, 116, 110), seedShift),
                        ShiftColor(new Color(148, 146, 140), seedShift),
                        ShiftColor(new Color(104, 102, 98), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.Gravel(
                            _tileSize,
                            name,
                            palette,
                            ShiftColor(new Color(68, 66, 62), seedShift)));
                    }
                case "coal_ore":
                    return FromSynth(ProceduralTextureSynth.Ore(
                        _tileSize,
                        name,
                        new Color(108, 108, 112),
                        ShiftColor(new Color(38, 38, 42), seedShift),
                        ShiftColor(new Color(72, 72, 76), seedShift)));
                case "iron_ore":
                    return FromSynth(ProceduralTextureSynth.Ore(
                        _tileSize,
                        name,
                        new Color(108, 108, 112),
                        ShiftColor(new Color(188, 132, 86), seedShift),
                        ShiftColor(new Color(228, 178, 128), seedShift)));
                case "gold_ore":
                    return FromSynth(ProceduralTextureSynth.Ore(
                        _tileSize,
                        name,
                        new Color(108, 108, 112),
                        ShiftColor(new Color(224, 184, 62), seedShift),
                        ShiftColor(new Color(255, 220, 98), seedShift)));
                case "cactus":
                    return FromSynth(ProceduralTextureSynth.CactusSprite(
                        _tileSize,
                        name,
                        ShiftColor(new Color(58, 132, 52), seedShift),
                        ShiftColor(new Color(42, 104, 46), seedShift),
                        ShiftColor(new Color(78, 154, 76), seedShift),
                        new Color(228, 236, 214)));
                case "tall_grass":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(48, 112, 42), seedShift),
                        ShiftColor(new Color(62, 138, 52), seedShift),
                        ShiftColor(new Color(78, 158, 58), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.FloraSprite(_tileSize, name, palette, 30));
                    }
                case "flower":
                    {
                        var grassPalette = new[]
                        {
                        ShiftColor(new Color(58, 132, 48), seedShift),
                        ShiftColor(new Color(74, 158, 56), seedShift),
                        ShiftColor(new Color(88, 172, 64), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.FlowerPatch(
                            _tileSize,
                            name,
                            grassPalette,
                            new[]
                            {
                            new Color(214, 76, 106),
                            new Color(238, 208, 72),
                            new Color(220, 120, 214)
                            }));
                    }
                case "station_bench":
                    {
                        var image = FromSynth(ProceduralTextureSynth.WoodPlank(
                            _tileSize,
                            name,
                            ShiftColor(new Color(118, 92, 58), seedShift),
                            ShiftColor(new Color(88, 64, 38), seedShift),
                            ShiftColor(new Color(104, 78, 48), seedShift)));
                        int margin = _tileSize / 6;
                        DrawRectOutline(image, margin, margin, _tileSize - margin, _tileSize - margin, ShiftColor(new Color(72, 52, 34), seedShift), 4);
                        DrawHorizontalLine(image, margin, _tileSize / 2, _tileSize - margin, _tileSize / 2, ShiftColor(new Color(148, 112, 72), seedShift), 3);
                        return image;
                    }
                case "station_forge":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(78, 42, 34), seedShift),
                        ShiftColor(new Color(88, 48, 38), seedShift),
                        ShiftColor(new Color(102, 58, 46), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.ForgeStation(
                            _tileSize,
                            name,
                            palette,
                            ShiftColor(new Color(58, 38, 30), seedShift),
                            new Color(224, 120, 48)));
                    }
                case "station_crucible":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(88, 96, 108), seedShift),
                        ShiftColor(new Color(96, 104, 118), seedShift),
                        ShiftColor(new Color(108, 116, 128), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.CrucibleStation(
                            _tileSize,
                            name,
                            palette,
                            new Color(58, 132, 176),
                            new Color(42, 98, 176, 200)));
                    }
                case "oak_plank":
                    return FromSynth(ProceduralTextureSynth.WoodPlank(
                        _tileSize,
                        name,
                        ShiftColor(new Color(156, 118, 72), seedShift),
                        ShiftColor(new Color(118, 86, 52), seedShift),
                        ShiftColor(new Color(138, 104, 64), seedShift)));
                case "glass":
                    return FromSynth(ProceduralTextureSynth.GlassTile(_tileSize, name));
                case "clay":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(148, 96, 72), seedShift),
                        ShiftColor(new Color(168, 112, 88), seedShift),
                        ShiftColor(new Color(188, 128, 98), seedShift),
                        ShiftColor(new Color(132, 86, 64), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.Dirt(_tileSize, name, palette));
                    }
                case "iron_block":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(
                        _tileSize,
                        name,
                        ShiftColor(new Color(168, 172, 178), seedShift),
                        ShiftColor(new Color(108, 112, 120), seedShift)));
                case "sandstone":
                    return FromSynth(ProceduralTextureSynth.WoodPlank(
                        _tileSize,
                        name,
                        ShiftColor(new Color(196, 168, 108), seedShift),
                        ShiftColor(new Color(148, 122, 78), seedShift),
                        ShiftColor(new Color(176, 148, 96), seedShift)));
                case "gold_block":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(
                        _tileSize,
                        name,
                        ShiftColor(new Color(224, 188, 64), seedShift),
                        ShiftColor(new Color(168, 132, 28), seedShift)));
                case "willow_log":
                    return FromSynth(ProceduralTextureSynth.WoodLog(
                        _tileSize,
                        name,
                        ShiftColor(new Color(68, 52, 38), seedShift),
                        ShiftColor(new Color(44, 32, 22), seedShift),
                        ShiftColor(new Color(92, 72, 50), seedShift),
                        11));
                case "willow_leaves":
                    return FromSynth(ProceduralTextureSynth.Leaves(
                        _tileSize,
                        name,
                        new[]
                        {
                            ShiftColor(new Color(38, 78, 44), seedShift),
                            ShiftColor(new Color(62, 112, 58), seedShift),
                            ShiftColor(new Color(30, 68, 38), seedShift),
                            ShiftColor(new Color(74, 128, 68), seedShift)
                        }));
                case "palm_log":
                    {
                        var pixels = ProceduralTextureSynth.WoodLog(
                            _tileSize,
                            name,
                            ShiftColor(new Color(168, 138, 88), seedShift),
                            ShiftColor(new Color(138, 108, 68), seedShift),
                            ShiftColor(new Color(198, 168, 118), seedShift),
                            14);
                        var image = new Image(pixels);
                        for (int y = 6; y < _tileSize; y += 14)
                        {
                            DrawHorizontalLine(image, _tileSize / 3, y, _tileSize * 2 / 3, y + 4, ShiftColor(new Color(128, 98, 58), seedShift), 2);
                        }

                        return image;
                    }
                case "palm_leaves":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(42, 98, 38), seedShift),
                        ShiftColor(new Color(58, 118, 48), seedShift),
                        ShiftColor(new Color(72, 142, 52), seedShift),
                        ShiftColor(new Color(36, 88, 34), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.PalmLeaves(_tileSize, name, palette));
                    }
                case "birch_plank":
                    return FromSynth(ProceduralTextureSynth.WoodPlank(
                        _tileSize,
                        name,
                        ShiftColor(new Color(198, 188, 168), seedShift),
                        ShiftColor(new Color(158, 148, 128), seedShift),
                        ShiftColor(new Color(178, 168, 148), seedShift)));
                case "pine_plank":
                    return FromSynth(ProceduralTextureSynth.WoodPlank(
                        _tileSize,
                        name,
                        ShiftColor(new Color(128, 96, 58), seedShift),
                        ShiftColor(new Color(92, 68, 40), seedShift),
                        ShiftColor(new Color(108, 82, 50), seedShift)));
                case "cobblestone":
                    {
                        var stones = new[]
                        {
                        ShiftColor(new Color(96, 96, 100), seedShift),
                        ShiftColor(new Color(118, 118, 122), seedShift),
                        ShiftColor(new Color(88, 88, 92), seedShift),
                        ShiftColor(new Color(132, 132, 136), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.Cobble(
                            _tileSize,
                            name,
                            stones,
                            ShiftColor(new Color(64, 64, 68), seedShift)));
                    }
                case "brick":
                    return FromSynth(ProceduralTextureSynth.Brick(
                        _tileSize,
                        name,
                        ShiftColor(new Color(148, 72, 52), seedShift),
                        ShiftColor(new Color(92, 88, 84), seedShift),
                        ShiftColor(new Color(168, 88, 62), seedShift),
                        ShiftColor(new Color(128, 58, 42), seedShift)));
                case "moss_stone":
                    {
                        var stonePalette = new[]
                        {
                        ShiftColor(new Color(92, 98, 86), seedShift),
                        ShiftColor(new Color(108, 114, 100), seedShift),
                        ShiftColor(new Color(84, 90, 78), seedShift)
                    };
                        var mossPalette = new[]
                        {
                        ShiftColor(new Color(48, 98, 42), seedShift),
                        ShiftColor(new Color(62, 118, 52), seedShift),
                        ShiftColor(new Color(38, 84, 36), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.MossStone(
                            _tileSize,
                            name,
                            stonePalette,
                            ShiftColor(new Color(68, 72, 62), seedShift),
                            mossPalette));
                    }
                case "mud":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(58, 46, 34), seedShift),
                        ShiftColor(new Color(72, 58, 42), seedShift),
                        ShiftColor(new Color(88, 72, 52), seedShift),
                        ShiftColor(new Color(64, 50, 38), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.Dirt(_tileSize, name, palette));
                    }
                case "reed":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(38, 98, 48), seedShift),
                        ShiftColor(new Color(48, 112, 52), seedShift),
                        ShiftColor(new Color(62, 138, 58), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.FloraSprite(_tileSize, name, palette, 32, addHeads: true));
                    }
                case "sunflower":
                    return FromSynth(ProceduralTextureSynth.SunflowerSprite(
                        _tileSize,
                        name,
                        ShiftColor(new Color(42, 98, 38), seedShift),
                        new Color(238, 198, 42),
                        new Color(68, 48, 28)));
                case "hay_bale":
                    {
                        var palette = new[]
                        {
                        ShiftColor(new Color(168, 138, 58), seedShift),
                        ShiftColor(new Color(188, 158, 68), seedShift),
                        ShiftColor(new Color(208, 178, 78), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.HayBale(
                            _tileSize,
                            name,
                            palette,
                            ShiftColor(new Color(158, 128, 48), seedShift),
                            ShiftColor(new Color(138, 108, 38), seedShift)));
                    }
                case "ice":
                    return FromSynth(ProceduralTextureSynth.IceTile(_tileSize, name));
                case "sheep_body":
                    return FromSynth(ProceduralTextureSynth.AnimalHide(_tileSize, name, new Color(220, 220, 220), new Color(180, 180, 180)));
                case "sheep_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHide(_tileSize, name, new Color(210, 210, 210), new Color(170, 170, 170)));
                case "pig_body":
                    return FromSynth(ProceduralTextureSynth.AnimalHide(_tileSize, name, new Color(240, 170, 170), new Color(200, 120, 120)));
                case "pig_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHide(_tileSize, name, new Color(230, 160, 160), new Color(190, 110, 110)));
                case "chicken_body":
                    return FromSynth(ProceduralTextureSynth.AnimalHide(_tileSize, name, new Color(240, 220, 120), new Color(200, 160, 60)));
                case "chicken_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHide(_tileSize, name, new Color(230, 210, 110), new Color(190, 150, 50)));
                default:
                    {
                        string stem = name.Split('.')[0];
                        if (stem.StartsWith("tool_", StringComparison.Ordinal))
                        {
                            return MakeToolIcon(stem);
                        }

                        return null;
                    }
            }
        }

        private Image MakeToolIcon(string stem)
        {
            string[] parts = stem.Split('_');
            if (parts.Length < 3)
            {
                return FillSolid(new Color(0, 0, 0, 0));
            }

            string tier = parts[1];
            string toolType = parts[2];
            var image = FillSolid(new Color(0, 0, 0, 0));

            (Color head, Color headDark) = tier switch
            {
                "wood" => (new Color(168, 122, 72), new Color(118, 82, 48)),
                "stone" => (new Color(156, 156, 162), new Color(104, 104, 110)),
                "iron" => (new Color(204, 210, 218), new Color(142, 148, 158)),
                "gold" => (new Color(252, 214, 72), new Color(198, 158, 28)),
                _ => (new Color(160, 160, 160), new Color(110, 110, 110))
            };

            Color handle = new Color(124, 88, 52);
            Color handleDark = new Color(84, 58, 34);
            Color highlight = Shade(head, 36);
            int s = Math.Max(4, _tileSize / 16);
            int cx = _tileSize / 2;
            int cy = _tileSize / 2;

            switch (toolType)
            {
                case "pickaxe":
                    DrawToolLine(image, cx - 3 * s, cy + 4 * s, cx + s, cy - s, handle, s);
                    DrawToolLine(image, cx - 3 * s + 1, cy + 4 * s + 1, cx + s + 1, cy - s + 1, handleDark, Math.Max(1, s / 3));
                    FillRect(image, cx - 4 * s, cy - 3 * s, 8 * s + 1, 2 * s + 1, head);
                    FillRect(image, cx - 4 * s, cy - 3 * s, 8 * s + 1, s, highlight);
                    FillTriangle(image, cx - 4 * s, cy - 2 * s, cx - 5 * s, cy + s, cx - 3 * s, cy - s, headDark);
                    FillTriangle(image, cx + 4 * s, cy - 2 * s, cx + 5 * s, cy + s, cx + 3 * s, cy - s, headDark);
                    break;
                case "axe":
                    DrawToolLine(image, cx - s / 2, cy + 4 * s, cx - s / 2, cy - 2 * s, handle, s);
                    FillPolygon(image, new[]
                    {
                        (cx + s, cy - 4 * s),
                        (cx + 5 * s, cy - s),
                        (cx + 4 * s, cy + 2 * s),
                        (cx + 2 * s, cy - s)
                    }, head);
                    DrawToolLine(image, cx + 2 * s, cy - 3 * s, cx + 4 * s, cy, highlight, Math.Max(2, s / 2));
                    FillTriangle(image, cx + 4 * s, cy + 2 * s, cx + 5 * s, cy + 3 * s, cx + 3 * s, cy + s, headDark);
                    break;
                case "shovel":
                    DrawToolLine(image, cx, cy + 4 * s, cx, cy - s, handle, s);
                    FillPolygon(image, new[]
                    {
                        (cx, cy - 4 * s),
                        (cx - 3 * s, cy - s),
                        (cx - 2 * s, cy + s),
                        (cx + 2 * s, cy + s),
                        (cx + 3 * s, cy - s)
                    }, head);
                    DrawToolLine(image, cx - s, cy - 2 * s, cx + s, cy - 2 * s, highlight, Math.Max(2, s / 2));
                    FillRect(image, cx - 2 * s, cy, 4 * s + 1, s + 1, headDark);
                    break;
                case "sword":
                    FillRect(image, cx - s / 2, cy + 3 * s, s + 1, 2 * s + 1, handle);
                    FillRect(image, cx - 2 * s, cy + 2 * s, 4 * s + 1, s + 1, headDark);
                    FillRect(image, cx - s, cy - 4 * s, 2 * s + 1, 6 * s + 1, head);
                    FillRect(image, cx - s + 1, cy - 3 * s, 2 * s - 1, s, highlight);
                    FillTriangle(image, cx, cy - 5 * s, cx - s, cy - 4 * s, cx + s, cy - 4 * s, highlight);
                    break;
            }

            FillEllipse(image, cx - 4 * s, cy + 5 * s, cx + 4 * s, cy + 7 * s, new Color(0, 0, 0, 48));
            return image;
        }

        private static void DrawToolLine(Image image, int x0, int y0, int x1, int y1, Color color, int width)
        {
            DrawHorizontalLine(image, x0, y0, x1, y1, color, width);
        }

        private static void FillTriangle(Image image, int x0, int y0, int x1, int y1, int x2, int y2, Color color)
        {
            FillPolygon(image, new[] { (x0, y0), (x1, y1), (x2, y2) }, color);
        }

        private static void FillPolygon(Image image, (int x, int y)[] points, Color color)
        {
            int minY = points.Min(p => p.y);
            int maxY = points.Max(p => p.y);
            for (int y = minY; y <= maxY; y++)
            {
                var intersections = new List<int>();
                for (int i = 0; i < points.Length; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Length];
                    if (a.y == b.y)
                    {
                        continue;
                    }

                    if (y >= Math.Min(a.y, b.y) && y < Math.Max(a.y, b.y))
                    {
                        int x = a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y);
                        intersections.Add(x);
                    }
                }

                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    DrawHorizontalLine(image, intersections[i], y, intersections[i + 1], y, color, 1);
                }
            }
        }

        private static void FillEllipse(Image image, int x0, int y0, int x1, int y1, Color color)
        {
            int cx = (x0 + x1) / 2;
            int cy = (y0 + y1) / 2;
            int rx = Math.Max(1, (x1 - x0) / 2);
            int ry = Math.Max(1, (y1 - y0) / 2);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cx) / (float)rx;
                    float dy = (y - cy) / (float)ry;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        SetPixel(image, x, y, color);
                    }
                }
            }
        }

        private int ApplyPaletteSeed(string name)
        {
            if (_paletteSeed == 0)
            {
                return 0;
            }

            return NoiseValue("palette", _paletteSeed, name.GetHashCode(), 0) % 17 - 8;
        }

        private static string SeedName(string name, int seedShift)
        {
            return seedShift == 0 ? name : $"{name}#{seedShift}";
        }

        private Color ShiftColor(Color color, int amount)
        {
            return new Color(
                Clamp(color.R + amount),
                Clamp(color.G + amount / 2),
                Clamp(color.B + amount / 3));
        }

        private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, 255);

        private static Color Shade(Color color, int amount)
        {
            return new Color(Clamp(color.R + amount), Clamp(color.G + amount), Clamp(color.B + amount));
        }

        private static int NoiseValue(string name, int x, int y, int salt = 0)
        {
            int seed = 0;
            for (int i = 0; i < name.Length; i++)
            {
                seed += (i + 1) * name[i];
            }

            seed += salt * 131;
            unchecked
            {
                uint value = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                value = (value ^ (value >> 13)) * 1274126177u;
                return (int)((value ^ (value >> 16)) & 255u);
            }
        }

        private static void SetPixel(Image image, int x, int y, Color color)
        {
            if (x >= 0 && x < image.Size && y >= 0 && y < image.Size)
            {
                image.Pixels[y * image.Size + x] = color;
            }
        }

        private static void FillRect(Image image, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height && py < image.Size; py++)
            {
                for (int px = x; px < x + width && px < image.Size; px++)
                {
                    if (px >= 0 && py >= 0)
                    {
                        image.Pixels[py * image.Size + px] = color;
                    }
                }
            }
        }

        private static void DrawRectOutline(Image image, int x0, int y0, int x1, int y1, Color color, int width)
        {
            for (int t = 0; t < width; t++)
            {
                DrawHorizontalLine(image, x0, y0 + t, x1, y0 + t, color, 1);
                DrawHorizontalLine(image, x0, y1 - t, x1, y1 - t, color, 1);
                DrawVerticalLine(image, x0 + t, color, y1 - y0);
                DrawVerticalLine(image, x1 - t, color, y1 - y0);
            }
        }

        private static void DrawVerticalLine(Image image, int x, Color color, int height)
        {
            for (int y = 0; y < height && y < image.Size; y++)
            {
                SetPixel(image, x, y, color);
            }
        }

        private static void DrawHorizontalLine(Image image, int x0, int y0, int x1, int y1, Color color, int width)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                int x = x0 + (x1 - x0) * i / Math.Max(1, steps);
                int y = y0 + (y1 - y0) * i / Math.Max(1, steps);
                FillRect(image, x, y, width, width, color);
            }
        }

        private sealed class Image
        {
            public Image(Color[] pixels)
            {
                Pixels = pixels;
                Size = (int)Math.Sqrt(pixels.Length);
            }

            public Color[] Pixels { get; }
            public int Size { get; }
            public int Height => Size;

            public Image Clone()
            {
                var copy = new Color[Pixels.Length];
                Array.Copy(Pixels, copy, Pixels.Length);
                return new Image(copy);
            }
        }
    }
}
