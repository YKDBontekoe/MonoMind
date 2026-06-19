using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace Autonocraft.Engine
{

    public sealed partial class ProceduralAtlasBuilder
    {
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
                        ShiftColor(new Color(92, 68, 42), seedShift),
                        ShiftColor(new Color(110, 82, 52), seedShift),
                        ShiftColor(new Color(128, 96, 62), seedShift),
                        ShiftColor(new Color(84, 60, 38), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.Dirt(_tileSize, name, palette));
                    }
                case "grass_side":
                    {
                        var dirtPalette = new[]
                        {
                        ShiftColor(new Color(92, 68, 42), seedShift),
                        ShiftColor(new Color(110, 82, 52), seedShift),
                        ShiftColor(new Color(128, 96, 62), seedShift),
                        ShiftColor(new Color(84, 60, 38), seedShift)
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
                case "oak_log_top":
                    return FromSynth(ProceduralTextureSynth.WoodLogTop(
                        _tileSize,
                        name,
                        ShiftColor(new Color(96, 72, 48), seedShift),
                        ShiftColor(new Color(68, 48, 30), seedShift),
                        ShiftColor(new Color(156, 118, 72), seedShift),
                        ShiftColor(new Color(118, 86, 52), seedShift)));
                case "birch_log_top":
                    return FromSynth(ProceduralTextureSynth.WoodLogTop(
                        _tileSize,
                        name,
                        ShiftColor(new Color(210, 205, 188), seedShift),
                        ShiftColor(new Color(184, 178, 162), seedShift),
                        ShiftColor(new Color(198, 188, 168), seedShift),
                        ShiftColor(new Color(158, 148, 128), seedShift)));
                case "pine_log_top":
                    return FromSynth(ProceduralTextureSynth.WoodLogTop(
                        _tileSize,
                        name,
                        ShiftColor(new Color(78, 58, 36), seedShift),
                        ShiftColor(new Color(52, 38, 22), seedShift),
                        ShiftColor(new Color(128, 96, 58), seedShift),
                        ShiftColor(new Color(92, 68, 40), seedShift)));
                case "willow_log_top":
                    return FromSynth(ProceduralTextureSynth.WoodLogTop(
                        _tileSize,
                        name,
                        ShiftColor(new Color(68, 52, 38), seedShift),
                        ShiftColor(new Color(44, 32, 22), seedShift),
                        ShiftColor(new Color(136, 106, 68), seedShift),
                        ShiftColor(new Color(102, 78, 48), seedShift)));
                case "palm_log_top":
                    return FromSynth(ProceduralTextureSynth.WoodLogTop(
                        _tileSize,
                        name,
                        ShiftColor(new Color(168, 138, 88), seedShift),
                        ShiftColor(new Color(138, 108, 68), seedShift),
                        ShiftColor(new Color(208, 182, 128), seedShift),
                        ShiftColor(new Color(178, 148, 96), seedShift)));
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
                case "snow_side":
                    {
                        var dirtPalette = new[]
                        {
                        ShiftColor(new Color(92, 68, 42), seedShift),
                        ShiftColor(new Color(110, 82, 52), seedShift),
                        ShiftColor(new Color(128, 96, 62), seedShift),
                        ShiftColor(new Color(84, 60, 38), seedShift)
                    };
                        var fringePalette = new[]
                        {
                        ShiftColor(new Color(240, 246, 252), seedShift),
                        ShiftColor(new Color(252, 254, 255), seedShift),
                        ShiftColor(new Color(228, 236, 244), seedShift)
                    };
                        var dirt = ProceduralTextureSynth.Dirt(_tileSize, SeedName("dirt.png", seedShift), dirtPalette);
                        var fringe = ProceduralTextureSynth.SnowFringe(_tileSize, name + "_fringe", fringePalette);
                        return ComposeSnowSide(new Image(dirt), new Image(fringe));
                    }
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
                case "copper_ore":
                    return FromSynth(ProceduralTextureSynth.Ore(
                        _tileSize,
                        name,
                        new Color(108, 108, 112),
                        ShiftColor(new Color(200, 95, 40), seedShift),
                        ShiftColor(new Color(230, 135, 80), seedShift)));
                case "silver_ore":
                    return FromSynth(ProceduralTextureSynth.Ore(
                        _tileSize,
                        name,
                        new Color(108, 108, 112),
                        ShiftColor(new Color(180, 190, 195), seedShift),
                        ShiftColor(new Color(220, 230, 235), seedShift)));
                case "diamond_ore":
                    return FromSynth(ProceduralTextureSynth.Ore(
                        _tileSize,
                        name,
                        new Color(108, 108, 112),
                        ShiftColor(new Color(60, 200, 220), seedShift),
                        ShiftColor(new Color(120, 240, 255), seedShift)));
                case "emerald_ore":
                    return FromSynth(ProceduralTextureSynth.Ore(
                        _tileSize,
                        name,
                        new Color(108, 108, 112),
                        ShiftColor(new Color(40, 200, 100), seedShift),
                        ShiftColor(new Color(100, 240, 160), seedShift)));
                case "ruby_ore":
                    return FromSynth(ProceduralTextureSynth.Ore(
                        _tileSize,
                        name,
                        new Color(108, 108, 112),
                        ShiftColor(new Color(220, 40, 80), seedShift),
                        ShiftColor(new Color(255, 120, 150), seedShift)));
                case "quartz_ore":
                    return FromSynth(ProceduralTextureSynth.Ore(
                        _tileSize,
                        name,
                        new Color(108, 108, 112),
                        ShiftColor(new Color(225, 225, 230), seedShift),
                        ShiftColor(new Color(255, 255, 255), seedShift)));
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
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.TallGrassClump(half, $"tall_grass_v{variant}", palette)));
                    }
                case "fern":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(34, 82, 30), seedShift),
                            ShiftColor(new Color(46, 106, 40), seedShift),
                            ShiftColor(new Color(58, 122, 50), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.FernSprite(half, $"fern_v{variant}", palette)));
                    }
                case "mushroom_red":
                    {
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.MushroomSprite(half, $"mush_r_v{variant}", ShiftColor(new Color(220, 40, 40), seedShift), new Color(255, 255, 255))));
                    }
                case "mushroom_brown":
                    {
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.MushroomSprite(half, $"mush_b_v{variant}", ShiftColor(new Color(150, 110, 80), seedShift), null)));
                    }
                case "dead_bush":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(130, 100, 70), seedShift),
                            ShiftColor(new Color(150, 120, 90), seedShift),
                            ShiftColor(new Color(110, 80, 50), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.DeadBushSprite(half, $"dead_bush_v{variant}", palette)));
                    }
                case "lily_pad":
                    {
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.LilyPadSprite(half, $"lily_pad_v{variant}")));
                    }
                case "vine":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(48, 102, 38), seedShift),
                            ShiftColor(new Color(62, 122, 48), seedShift),
                            ShiftColor(new Color(76, 142, 58), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.VineSprite(half, $"vine_v{variant}", palette)));
                    }
                case "berry_bush":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(42, 92, 34), seedShift),
                            ShiftColor(new Color(56, 114, 46), seedShift),
                            ShiftColor(new Color(70, 134, 58), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.BerryBushSprite(half, $"berry_bush_v{variant}", palette)));
                    }
                case "seagrass":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(32, 102, 90), seedShift),
                            ShiftColor(new Color(42, 122, 110), seedShift),
                            ShiftColor(new Color(52, 142, 130), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.SeagrassSprite(half, $"seagrass_v{variant}", palette)));
                    }
                case "flower":
                    {
                        var petalColors = new[]
                        {
                            ShiftColor(new Color(214, 76, 106), seedShift),
                            ShiftColor(new Color(238, 208, 72), seedShift),
                            ShiftColor(new Color(220, 120, 214), seedShift),
                            ShiftColor(new Color(120, 160, 230), seedShift)
                        };
                        var stem = ShiftColor(new Color(42, 98, 38), seedShift);
                        var center = new Color(246, 236, 180);
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.FlowerStemSprite(
                                half,
                                $"flower_v{variant}",
                                stem,
                                petalColors,
                                center)));
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

                        // Draw tiny hammer details on the workbench (matching Python exactly)
                        DrawHorizontalLine(image, margin + 12, margin + 12, margin + 12, margin + 28, ShiftColor(new Color(124, 88, 52), seedShift), 2);
                        DrawHorizontalLine(image, margin + 6, margin + 12, margin + 18, margin + 12, ShiftColor(new Color(156, 156, 162), seedShift), 4);

                        // Draw blueprint details
                        FillRect(image, _tileSize - margin - 30, _tileSize - margin - 22, 25, 17, ShiftColor(new Color(42, 98, 176), seedShift));
                        DrawRectOutline(image, _tileSize - margin - 26, _tileSize - margin - 18, _tileSize - margin - 10, _tileSize - margin - 10, new Color(220, 240, 255, 180), 1);
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
                        ShiftColor(new Color(78, 78, 84), seedShift),
                        ShiftColor(new Color(108, 108, 114), seedShift),
                        ShiftColor(new Color(68, 68, 74), seedShift),
                        ShiftColor(new Color(128, 128, 134), seedShift),
                        ShiftColor(new Color(92, 92, 98), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.Cobble(
                            _tileSize,
                            name,
                            stones,
                            ShiftColor(new Color(48, 48, 52), seedShift)));
                    }
                case "brick":
                    return FromSynth(ProceduralTextureSynth.Brick(
                        _tileSize,
                        name,
                        ShiftColor(new Color(132, 58, 42), seedShift),
                        ShiftColor(new Color(78, 74, 70), seedShift),
                        ShiftColor(new Color(158, 72, 52), seedShift),
                        ShiftColor(new Color(108, 46, 34), seedShift)));
                case "moss_stone":
                    {
                        var stonePalette = new[]
                        {
                        ShiftColor(new Color(82, 88, 76), seedShift),
                        ShiftColor(new Color(98, 104, 90), seedShift),
                        ShiftColor(new Color(72, 78, 68), seedShift),
                        ShiftColor(new Color(108, 114, 98), seedShift)
                    };
                        var mossPalette = new[]
                        {
                        ShiftColor(new Color(42, 92, 36), seedShift),
                        ShiftColor(new Color(58, 112, 48), seedShift),
                        ShiftColor(new Color(32, 78, 30), seedShift),
                        ShiftColor(new Color(68, 128, 56), seedShift)
                    };
                        return FromSynth(ProceduralTextureSynth.MossStone(
                            _tileSize,
                            name,
                            stonePalette,
                            ShiftColor(new Color(58, 62, 54), seedShift),
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
                            ShiftColor(new Color(88, 98, 48), seedShift),
                            ShiftColor(new Color(98, 112, 52), seedShift),
                            ShiftColor(new Color(112, 128, 58), seedShift)
                        };
                        var head = ShiftColor(new Color(148, 128, 62), seedShift);
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.ReedSprite(half, $"reed_v{variant}", palette, head)));
                    }
                case "sunflower":
                    {
                        var stem = ShiftColor(new Color(42, 98, 38), seedShift);
                        var petal = new Color(238, 198, 42);
                        var center = new Color(68, 48, 28);
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.SunflowerSprite(half, $"sunflower_v{variant}", stem, petal, center)));
                    }
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
                case "wheat_sprout":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(72, 118, 42), seedShift),
                            ShiftColor(new Color(88, 138, 48), seedShift),
                            ShiftColor(new Color(104, 158, 54), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.ShortGrassSprite(half, $"wheat_sprout_v{variant}", palette)));
                    }
                case "wheat_crop":
                    {
                        var stem = ShiftColor(new Color(58, 108, 36), seedShift);
                        var head = ShiftColor(new Color(196, 168, 58), seedShift);
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.WheatCropSprite(half, $"wheat_crop_v{variant}", stem, head)));
                    }
                case "carrot_sprout":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(62, 112, 40), seedShift),
                            ShiftColor(new Color(78, 132, 46), seedShift),
                            ShiftColor(new Color(92, 148, 52), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.ShortGrassSprite(half, $"carrot_sprout_v{variant}", palette)));
                    }
                case "carrot_crop":
                    {
                        var stem = ShiftColor(new Color(52, 102, 34), seedShift);
                        var root = ShiftColor(new Color(214, 108, 38), seedShift);
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.CarrotCropSprite(half, $"carrot_crop_v{variant}", stem, root)));
                    }
                case "ice":
                    return FromSynth(ProceduralTextureSynth.IceTile(_tileSize, name));
                case "sheep_body":
                    return FromSynth(ProceduralTextureSynth.SheepBody(_tileSize, name, new Color(220, 220, 220), new Color(180, 180, 180)));
                case "sheep_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHead(_tileSize, name, new Color(210, 210, 210), new Color(170, 170, 170), "sheep"));
                case "pig_body":
                    return FromSynth(ProceduralTextureSynth.PigBody(_tileSize, name, new Color(240, 170, 170), new Color(200, 120, 120)));
                case "pig_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHead(_tileSize, name, new Color(230, 160, 160), new Color(190, 110, 110), "pig"));
                case "chicken_body":
                    return FromSynth(ProceduralTextureSynth.ChickenBody(_tileSize, name, new Color(240, 220, 120), new Color(200, 160, 60)));
                case "chicken_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHead(_tileSize, name, new Color(230, 210, 110), new Color(190, 150, 50), "chicken"));
                case "marble":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(225, 225, 230), seedShift),
                            ShiftColor(new Color(240, 240, 245), seedShift),
                            ShiftColor(new Color(255, 255, 255), seedShift),
                            ShiftColor(new Color(210, 210, 215), seedShift),
                            ShiftColor(new Color(190, 190, 195), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Stone(_tileSize, name, palette, ShiftColor(new Color(160, 160, 165), seedShift)));
                    }
                case "basalt":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(45, 45, 50), seedShift),
                            ShiftColor(new Color(60, 60, 65), seedShift),
                            ShiftColor(new Color(75, 75, 80), seedShift),
                            ShiftColor(new Color(35, 35, 40), seedShift),
                            ShiftColor(new Color(25, 25, 30), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Stone(_tileSize, name, palette, ShiftColor(new Color(20, 20, 25), seedShift)));
                    }
                case "slate":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(70, 75, 85), seedShift),
                            ShiftColor(new Color(85, 90, 100), seedShift),
                            ShiftColor(new Color(100, 105, 115), seedShift),
                            ShiftColor(new Color(55, 60, 70), seedShift),
                            ShiftColor(new Color(45, 50, 60), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Stone(_tileSize, name, palette, ShiftColor(new Color(35, 40, 50), seedShift)));
                    }
                case "limestone":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(210, 200, 170), seedShift),
                            ShiftColor(new Color(225, 215, 185), seedShift),
                            ShiftColor(new Color(240, 230, 200), seedShift),
                            ShiftColor(new Color(195, 185, 155), seedShift),
                            ShiftColor(new Color(180, 170, 140), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Stone(_tileSize, name, palette, ShiftColor(new Color(160, 150, 120), seedShift)));
                    }
                case "granite":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(150, 100, 90), seedShift),
                            ShiftColor(new Color(170, 120, 110), seedShift),
                            ShiftColor(new Color(190, 140, 130), seedShift),
                            ShiftColor(new Color(130, 80, 70), seedShift),
                            ShiftColor(new Color(110, 60, 50), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Stone(_tileSize, name, palette, ShiftColor(new Color(90, 50, 40), seedShift)));
                    }
                case "obsidian":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(30, 20, 40), seedShift),
                            ShiftColor(new Color(45, 30, 60), seedShift),
                            ShiftColor(new Color(60, 40, 80), seedShift),
                            ShiftColor(new Color(20, 15, 30), seedShift),
                            ShiftColor(new Color(10, 5, 20), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Stone(_tileSize, name, palette, ShiftColor(new Color(5, 0, 10), seedShift)));
                    }
                case "amethyst":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(180, 100, 220), seedShift),
                            ShiftColor(new Color(200, 120, 240), seedShift),
                            ShiftColor(new Color(220, 140, 255), seedShift),
                            ShiftColor(new Color(150, 80, 190), seedShift),
                            ShiftColor(new Color(120, 60, 160), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Stone(_tileSize, name, palette, ShiftColor(new Color(90, 40, 130), seedShift)));
                    }
                case "magma_block":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(50, 20, 10), seedShift),
                            ShiftColor(new Color(80, 30, 15), seedShift),
                            ShiftColor(new Color(120, 40, 20), seedShift),
                            ShiftColor(new Color(30, 10, 5), seedShift),
                            ShiftColor(new Color(20, 5, 0), seedShift)
                        };
                        var pixels = ProceduralTextureSynth.Stone(_tileSize, name, palette, ShiftColor(new Color(10, 0, 0), seedShift));
                        var img = new Image(pixels);
                        for (int i = 0; i < 5; i++)
                        {
                            int cx = NoiseValue(name, i, 3, 7) % _tileSize;
                            int cy = NoiseValue(name, i, 5, 9) % _tileSize;
                            FillEllipse(img, cx - 3, cy - 3, cx + 3, cy + 3, new Color(255, 100, 0));
                        }
                        return img;
                    }
                case "cherry_log":
                    return FromSynth(ProceduralTextureSynth.WoodLog(_tileSize, name, ShiftColor(new Color(75, 45, 35), seedShift), ShiftColor(new Color(55, 30, 25), seedShift), ShiftColor(new Color(105, 65, 50), seedShift), 12));
                case "cherry_log_top":
                    return FromSynth(ProceduralTextureSynth.WoodLogTop(_tileSize, name, ShiftColor(new Color(75, 45, 35), seedShift), ShiftColor(new Color(55, 30, 25), seedShift), ShiftColor(new Color(240, 180, 190), seedShift), ShiftColor(new Color(220, 150, 160), seedShift)));
                case "cherry_leaves":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(220, 130, 150), seedShift),
                            ShiftColor(new Color(255, 190, 210), seedShift),
                            ShiftColor(new Color(200, 110, 130), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Leaves(_tileSize, name, palette));
                    }
                case "cherry_plank":
                    return FromSynth(ProceduralTextureSynth.WoodPlank(_tileSize, name, ShiftColor(new Color(220, 160, 170), seedShift), ShiftColor(new Color(160, 100, 110), seedShift), ShiftColor(new Color(190, 130, 140), seedShift)));
                case "mahogany_log":
                    return FromSynth(ProceduralTextureSynth.WoodLog(_tileSize, name, ShiftColor(new Color(60, 35, 25), seedShift), ShiftColor(new Color(45, 20, 15), seedShift), ShiftColor(new Color(85, 50, 35), seedShift), 14));
                case "mahogany_log_top":
                    return FromSynth(ProceduralTextureSynth.WoodLogTop(_tileSize, name, ShiftColor(new Color(60, 35, 25), seedShift), ShiftColor(new Color(45, 20, 15), seedShift), ShiftColor(new Color(140, 80, 60), seedShift), ShiftColor(new Color(110, 60, 40), seedShift)));
                case "mahogany_leaves":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(20, 55, 30), seedShift),
                            ShiftColor(new Color(45, 95, 60), seedShift),
                            ShiftColor(new Color(15, 45, 25), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Leaves(_tileSize, name, palette));
                    }
                case "mahogany_plank":
                    return FromSynth(ProceduralTextureSynth.WoodPlank(_tileSize, name, ShiftColor(new Color(120, 70, 50), seedShift), ShiftColor(new Color(80, 45, 30), seedShift), ShiftColor(new Color(100, 55, 40), seedShift)));
                case "maple_log":
                    return FromSynth(ProceduralTextureSynth.WoodLog(_tileSize, name, ShiftColor(new Color(110, 80, 50), seedShift), ShiftColor(new Color(85, 60, 35), seedShift), ShiftColor(new Color(135, 100, 65), seedShift), 10));
                case "maple_log_top":
                    return FromSynth(ProceduralTextureSynth.WoodLogTop(_tileSize, name, ShiftColor(new Color(110, 80, 50), seedShift), ShiftColor(new Color(85, 60, 35), seedShift), ShiftColor(new Color(200, 160, 110), seedShift), ShiftColor(new Color(175, 135, 90), seedShift)));
                case "maple_leaves":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(160, 50, 20), seedShift),
                            ShiftColor(new Color(220, 100, 45), seedShift),
                            ShiftColor(new Color(130, 35, 15), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Leaves(_tileSize, name, palette));
                    }
                case "maple_plank":
                    return FromSynth(ProceduralTextureSynth.WoodPlank(_tileSize, name, ShiftColor(new Color(185, 145, 95), seedShift), ShiftColor(new Color(135, 100, 60), seedShift), ShiftColor(new Color(160, 120, 75), seedShift)));
                case "glowshroom":
                    return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                        ProceduralTextureSynth.MushroomSprite(half, $"glowshroom_v{variant}", new Color(40, 120, 230), new Color(180, 220, 255))));
                case "lavender":
                    {
                        var stem = ShiftColor(new Color(50, 110, 45), seedShift);
                        var petalColors = new[]
                        {
                            ShiftColor(new Color(150, 90, 220), seedShift),
                            ShiftColor(new Color(170, 110, 240), seedShift),
                            ShiftColor(new Color(130, 70, 190), seedShift)
                        };
                        var center = ShiftColor(new Color(180, 120, 255), seedShift);
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.FlowerStemSprite(half, $"lavender_v{variant}", stem, petalColors, center)));
                    }
                case "bamboo":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(60, 140, 50), seedShift),
                            ShiftColor(new Color(80, 160, 60), seedShift),
                            ShiftColor(new Color(45, 110, 35), seedShift)
                        };
                        var head = ShiftColor(new Color(110, 180, 80), seedShift);
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.ReedSprite(half, $"bamboo_v{variant}", palette, head)));
                    }
                case "lantern":
                    {
                        var image = FillSolid(new Color(0, 0, 0, 0));
                        int cx = _tileSize / 2;
                        int cy = _tileSize / 2;
                        FillRect(image, cx - 10, cy - 12, 21, 25, new Color(40, 40, 45));
                        FillRect(image, cx - 6, cy - 8, 13, 17, new Color(255, 160, 40));
                        FillRect(image, cx - 3, cy - 5, 7, 11, new Color(255, 220, 120));
                        DrawToolLine(image, cx - 4, cy - 12, cx + 4, cy - 12, new Color(40, 40, 45), 2);
                        DrawToolLine(image, cx, cy - 12, cx, cy - 18, new Color(40, 40, 45), 2);
                        return image;
                    }
                case "red_stained_glass":
                    {
                        var image = FillSolid(new Color(220, 40, 40, 80));
                        DrawRectOutline(image, 0, 0, _tileSize - 1, _tileSize - 1, new Color(255, 80, 80, 180), 2);
                        return image;
                    }
                case "blue_stained_glass":
                    {
                        var image = FillSolid(new Color(40, 80, 220, 80));
                        DrawRectOutline(image, 0, 0, _tileSize - 1, _tileSize - 1, new Color(80, 120, 255, 180), 2);
                        return image;
                    }
                case "station_smoker":
                    {
                        var pixels = ProceduralTextureSynth.Brick(_tileSize, name, new Color(80, 80, 85), new Color(50, 50, 52), new Color(100, 100, 105), new Color(60, 60, 62));
                        var img = new Image(pixels);
                        int cx = _tileSize / 2;
                        int cy = _tileSize / 2;
                        FillRect(img, cx - 8, cy - 8, 17, 17, new Color(30, 30, 32));
                        DrawToolLine(img, cx - 8, cy - 4, cx + 8, cy - 4, new Color(10, 10, 12), 2);
                        DrawToolLine(img, cx - 8, cy + 4, cx + 8, cy + 4, new Color(10, 10, 12), 2);
                        return img;
                    }
                case "station_stonecutter":
                    {
                        var pixels = ProceduralTextureSynth.WoodPlank(_tileSize, name, new Color(140, 110, 80), new Color(100, 75, 50), new Color(120, 95, 65));
                        var img = new Image(pixels);
                        int cx = _tileSize / 2;
                        int cy = _tileSize / 2;
                        FillEllipse(img, cx - 16, cy - 16, cx + 16, cy + 16, new Color(160, 160, 165));
                        FillEllipse(img, cx - 4, cy - 4, cx + 4, cy + 4, new Color(80, 80, 85));
                        for (int angle = 0; angle < 360; angle += 45)
                        {
                            float rad = angle * MathF.PI / 180f;
                            int tx = (int)(cx + 18f * MathF.Cos(rad));
                            int ty = (int)(cy + 18f * MathF.Sin(rad));
                            DrawToolLine(img, cx, cy, tx, ty, new Color(120, 120, 125), 2);
                        }
                        return img;
                    }
                case "chest":
                    {
                        var pixels = ProceduralTextureSynth.WoodPlank(_tileSize, name, new Color(120, 82, 48), new Color(88, 58, 32), new Color(150, 108, 62));
                        var img = new Image(pixels);
                        int cx = _tileSize / 2;
                        int cy = _tileSize / 2;
                        FillRect(img, cx - 18, cy - 14, 37, 28, new Color(96, 64, 36));
                        FillRect(img, cx - 16, cy - 12, 33, 24, new Color(132, 92, 52));
                        FillRect(img, cx - 2, cy - 4, 5, 8, new Color(210, 180, 60));
                        DrawRectOutline(img, cx - 16, cy - 12, cx + 16, cy + 12, new Color(70, 48, 28), 2);
                        return img;
                    }
                case "slate_brick":
                    return FromSynth(ProceduralTextureSynth.Brick(_tileSize, name, ShiftColor(new Color(62, 68, 78), seedShift), ShiftColor(new Color(32, 36, 44), seedShift), ShiftColor(new Color(82, 88, 98), seedShift), ShiftColor(new Color(48, 54, 64), seedShift)));
                case "marble_brick":
                    return FromSynth(ProceduralTextureSynth.Brick(_tileSize, name, ShiftColor(new Color(210, 210, 218), seedShift), ShiftColor(new Color(148, 148, 156), seedShift), ShiftColor(new Color(238, 238, 244), seedShift), ShiftColor(new Color(178, 178, 186), seedShift)));
                case "basalt_brick":
                    return FromSynth(ProceduralTextureSynth.Brick(_tileSize, name, ShiftColor(new Color(45, 45, 50), seedShift), ShiftColor(new Color(20, 20, 25), seedShift), ShiftColor(new Color(65, 65, 70), seedShift), ShiftColor(new Color(30, 30, 35), seedShift)));
                case "polished_marble":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(_tileSize, name, ShiftColor(new Color(240, 240, 245), seedShift), ShiftColor(new Color(180, 180, 185), seedShift)));
                case "polished_granite":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(_tileSize, name, ShiftColor(new Color(180, 130, 120), seedShift), ShiftColor(new Color(130, 90, 80), seedShift)));
                case "diamond_block":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(_tileSize, name, ShiftColor(new Color(100, 220, 240), seedShift), ShiftColor(new Color(40, 170, 190), seedShift)));
                case "copper_block":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(_tileSize, name, ShiftColor(new Color(210, 110, 50), seedShift), ShiftColor(new Color(160, 70, 30), seedShift)));
                case "ruby_block":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(_tileSize, name, ShiftColor(new Color(230, 40, 80), seedShift), ShiftColor(new Color(170, 20, 50), seedShift)));
                case "quartz_block":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(_tileSize, name, ShiftColor(new Color(245, 245, 250), seedShift), ShiftColor(new Color(200, 200, 205), seedShift)));
                case "emerald_block":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(_tileSize, name, ShiftColor(new Color(60, 220, 120), seedShift), ShiftColor(new Color(30, 170, 80), seedShift)));
                case "silver_block":
                    return FromSynth(ProceduralTextureSynth.MetalBlock(_tileSize, name, ShiftColor(new Color(200, 210, 215), seedShift), ShiftColor(new Color(140, 150, 155), seedShift)));
                case "quicksand":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(140, 125, 80), seedShift),
                            ShiftColor(new Color(160, 145, 95), seedShift),
                            ShiftColor(new Color(180, 160, 110), seedShift),
                            ShiftColor(new Color(120, 105, 70), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Sand(_tileSize, name, palette));
                    }
                case "lava":
                    {
                        var pixels = new Color[_tileSize * _tileSize];
                        var deep = new Color(200, 30, 0);
                        var mid = new Color(235, 70, 0);
                        var shallow = new Color(255, 130, 0);
                        for (int y = 0; y < _tileSize; y++)
                        {
                            for (int x = 0; x < _tileSize; x++)
                            {
                                int n0 = NoiseValue(name, x / 4, y / 4, 3);
                                int n1 = NoiseValue(name, x / 2, y / 2, 7);
                                float blend = ((n0 & 255) / 255f * 0.6f) + ((n1 & 255) / 255f * 0.4f);
                                var baseColor = Color.Lerp(Color.Lerp(deep, mid, blend), shallow, blend * blend);
                                pixels[y * _tileSize + x] = baseColor;
                            }
                        }
                        return new Image(pixels);
                    }
                case "rope":
                    {
                        var img = FillSolid(new Color(0, 0, 0, 0));
                        int cx = _tileSize / 2;
                        for (int y = 0; y < _tileSize; y += 8)
                        {
                            var color = ((y / 8) % 2 == 0) ? new Color(130, 95, 65) : new Color(160, 120, 85);
                            FillEllipse(img, cx - 4, y, cx + 4, y + 10, color);
                        }
                        return img;
                    }
                case "kelp":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(40, 110, 50), seedShift),
                            ShiftColor(new Color(60, 135, 70), seedShift),
                            ShiftColor(new Color(25, 85, 35), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.VineSprite(half, $"kelp_v{variant}", palette)));
                    }
                case "shrub":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(42, 98, 36), seedShift),
                            ShiftColor(new Color(58, 118, 48), seedShift),
                            ShiftColor(new Color(72, 138, 58), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.BerryBushSprite(half, $"shrub_v{variant}", palette)));
                    }
                case "heather":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(140, 110, 150), seedShift),
                            ShiftColor(new Color(160, 130, 170), seedShift),
                            ShiftColor(new Color(120, 95, 130), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.FloraSprite(half, $"heather_v{variant}", palette, 8)));
                    }
                case "moss":
                    return FromSynth(ProceduralTextureSynth.Surface(
                        _tileSize,
                        name,
                        new[]
                        {
                            ShiftColor(new Color(48, 92, 42), seedShift),
                            ShiftColor(new Color(62, 112, 54), seedShift),
                            ShiftColor(new Color(38, 78, 34), seedShift)
                        },
                        12));
                case "juniper":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(34, 72, 38), seedShift),
                            ShiftColor(new Color(48, 92, 48), seedShift),
                            ShiftColor(new Color(58, 108, 56), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.FernSprite(half, $"juniper_v{variant}", palette)));
                    }
                case "poppy":
                    return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                        ProceduralTextureSynth.FlowerStemSprite(
                            half,
                            $"poppy_v{variant}",
                            ShiftColor(new Color(42, 98, 38), seedShift),
                            [ShiftColor(new Color(210, 48, 42), seedShift), ShiftColor(new Color(230, 62, 52), seedShift)],
                            ShiftColor(new Color(40, 30, 20), seedShift))));
                case "daisy":
                    return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                        ProceduralTextureSynth.FlowerStemSprite(
                            half,
                            $"daisy_v{variant}",
                            ShiftColor(new Color(48, 102, 42), seedShift),
                            [ShiftColor(new Color(245, 245, 238), seedShift), ShiftColor(new Color(252, 252, 246), seedShift)],
                            ShiftColor(new Color(240, 200, 48), seedShift))));
                case "blue_flax":
                    return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                        ProceduralTextureSynth.FlowerStemSprite(
                            half,
                            $"blue_flax_v{variant}",
                            ShiftColor(new Color(40, 92, 36), seedShift),
                            [ShiftColor(new Color(72, 118, 220), seedShift), ShiftColor(new Color(98, 142, 238), seedShift)],
                            ShiftColor(new Color(220, 220, 180), seedShift))));
                case "tulip":
                    return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                        ProceduralTextureSynth.FlowerStemSprite(
                            half,
                            $"tulip_v{variant}",
                            ShiftColor(new Color(44, 100, 40), seedShift),
                            [ShiftColor(new Color(228, 92, 48), seedShift), ShiftColor(new Color(248, 118, 62), seedShift)],
                            ShiftColor(new Color(32, 28, 24), seedShift))));
                case "wild_rose":
                    return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                        ProceduralTextureSynth.FlowerStemSprite(
                            half,
                            $"wild_rose_v{variant}",
                            ShiftColor(new Color(46, 96, 40), seedShift),
                            [ShiftColor(new Color(220, 88, 128), seedShift), ShiftColor(new Color(240, 108, 148), seedShift)],
                            ShiftColor(new Color(248, 210, 90), seedShift))));
                case "moss_carpet":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(42, 88, 36), seedShift),
                            ShiftColor(new Color(56, 108, 46), seedShift),
                            ShiftColor(new Color(34, 74, 30), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.FloraSprite(half, $"moss_carpet_v{variant}", palette, 10)));
                    }
                case "lichen":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(118, 128, 108), seedShift),
                            ShiftColor(new Color(138, 148, 126), seedShift),
                            ShiftColor(new Color(98, 108, 92), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.PackFloraVariants(_tileSize, (half, variant) =>
                            ProceduralTextureSynth.FloraSprite(half, $"lichen_v{variant}", palette, 12)));
                    }
                case "red_sand":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(168, 88, 52), seedShift),
                            ShiftColor(new Color(196, 108, 62), seedShift),
                            ShiftColor(new Color(214, 128, 72), seedShift),
                            ShiftColor(new Color(148, 72, 42), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Sand(_tileSize, name, palette));
                    }
                case "dripstone":
                    {
                        var palette = new[]
                        {
                            ShiftColor(new Color(148, 136, 118), seedShift),
                            ShiftColor(new Color(168, 156, 136), seedShift),
                            ShiftColor(new Color(128, 118, 102), seedShift),
                            ShiftColor(new Color(178, 166, 148), seedShift)
                        };
                        return FromSynth(ProceduralTextureSynth.Stone(
                            _tileSize,
                            name,
                            palette,
                            ShiftColor(new Color(108, 98, 86), seedShift)));
                    }
                case "cow_body":
                    return FromSynth(ProceduralTextureSynth.CowBody(_tileSize, name));
                case "cow_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHead(_tileSize, name, new Color(220, 220, 220), new Color(50, 50, 52), "cow"));
                case "bear_body":
                    return FromSynth(ProceduralTextureSynth.BearBody(_tileSize, name));
                case "bear_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHead(_tileSize, name, new Color(70, 42, 28), new Color(40, 24, 16), "bear"));
                case "fox_body":
                    return FromSynth(ProceduralTextureSynth.FoxBody(_tileSize, name));
                case "fox_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHead(_tileSize, name, new Color(220, 95, 30), new Color(245, 240, 230), "fox"));
                case "deer_body":
                    return FromSynth(ProceduralTextureSynth.DeerBody(_tileSize, name));
                case "deer_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHead(_tileSize, name, new Color(175, 115, 75), new Color(245, 245, 240), "deer"));
                case "wolf_body":
                    return FromSynth(ProceduralTextureSynth.FoxBody(_tileSize, name, new Color(85, 85, 90), new Color(160, 160, 165)));
                case "wolf_head":
                    return FromSynth(ProceduralTextureSynth.AnimalHead(_tileSize, name, new Color(85, 85, 90), new Color(160, 160, 165), "wolf"));
                case "villager_body":
                    return FromSynth(ProceduralTextureSynth.VillagerBody(_tileSize, name));
                case "villager_head":
                    return FromSynth(ProceduralTextureSynth.VillagerHead(_tileSize, name));
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
    }
}
