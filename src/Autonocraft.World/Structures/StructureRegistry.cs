using System;
using System.Collections.Generic;

namespace Autonocraft.World.Structures
{
    public static class StructureRegistry
    {
        private static readonly StructureDefinition[] AllDefinitions = BuildDefinitions();
        private static readonly Dictionary<(BiomeType, StructureTier), StructureDefinition[]> ByBiomeTier = BuildLookup();

        public static IReadOnlyList<StructureDefinition> All => AllDefinitions;

        public static IReadOnlyList<StructureDefinition> GetCandidates(BiomeType biome, StructureTier tier)
        {
            return ByBiomeTier.TryGetValue((biome, tier), out var list)
                ? list
                : Array.Empty<StructureDefinition>();
        }

        private static Dictionary<(BiomeType, StructureTier), StructureDefinition[]> BuildLookup()
        {
            var map = new Dictionary<(BiomeType, StructureTier), List<StructureDefinition>>();

            foreach (var definition in AllDefinitions)
            {
                foreach (var biome in definition.AllowedBiomes)
                {
                    var key = (biome, definition.Tier);
                    if (!map.TryGetValue(key, out var list))
                    {
                        list = new List<StructureDefinition>();
                        map[key] = list;
                    }

                    list.Add(definition);
                }
            }

            var result = new Dictionary<(BiomeType, StructureTier), StructureDefinition[]>();
            foreach (var entry in map)
            {
                result[entry.Key] = entry.Value.ToArray();
            }

            return result;
        }

        private static StructureDefinition[] BuildDefinitions()
        {
            return new[]
            {
                new StructureDefinition
                {
                    Id = "ForestShelter",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.Forest, BiomeType.Plains },
                    Template = BuildForestShelter()
                },
                new StructureDefinition
                {
                    Id = "PlainsWell",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.Plains },
                    Template = BuildPlainsWell()
                },
                new StructureDefinition
                {
                    Id = "DesertCairn",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.Desert },
                    Template = BuildDesertCairn()
                },
                new StructureDefinition
                {
                    Id = "SwampShrine",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.Swamp },
                    Template = BuildSwampShrine()
                },
                new StructureDefinition
                {
                    Id = "BeachPost",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.Beach },
                    Template = BuildBeachPost()
                },
                new StructureDefinition
                {
                    Id = "MountainCairn",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.Mountains },
                    Template = BuildMountainCairn()
                },
                new StructureDefinition
                {
                    Id = "PlainsCottage",
                    Tier = StructureTier.Medium,
                    AllowedBiomes = new[] { BiomeType.Plains },
                    Template = BuildPlainsCottage()
                },
                new StructureDefinition
                {
                    Id = "ForestWatchtower",
                    Tier = StructureTier.Medium,
                    AllowedBiomes = new[] { BiomeType.Forest },
                    Template = BuildForestWatchtower()
                },
                new StructureDefinition
                {
                    Id = "DesertShrine",
                    Tier = StructureTier.Medium,
                    AllowedBiomes = new[] { BiomeType.Desert },
                    Template = BuildDesertShrine()
                },
                new StructureDefinition
                {
                    Id = "SwampAltar",
                    Tier = StructureTier.Medium,
                    AllowedBiomes = new[] { BiomeType.Swamp },
                    Template = BuildSwampAltar()
                },
                new StructureDefinition
                {
                    Id = "SnowyHut",
                    Tier = StructureTier.Medium,
                    AllowedBiomes = new[] { BiomeType.SnowyPeaks },
                    Template = BuildSnowyHut()
                },
                new StructureDefinition
                {
                    Id = "VillageOutpost",
                    Tier = StructureTier.Medium,
                    AllowedBiomes = new[] { BiomeType.Plains, BiomeType.Forest },
                    Template = BuildVillageOutpost()
                },
                new StructureDefinition
                {
                    Id = "BadlandsSpire",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.Badlands },
                    Template = BuildBadlandsSpire()
                },
                new StructureDefinition
                {
                    Id = "MushroomCircle",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.MushroomForest },
                    Template = BuildMushroomCircle()
                },
                new StructureDefinition
                {
                    Id = "VolcanicVent",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.Volcanic },
                    Template = BuildVolcanicVent()
                },
                new StructureDefinition
                {
                    Id = "MangroveDock",
                    Tier = StructureTier.Small,
                    AllowedBiomes = new[] { BiomeType.Mangrove },
                    Template = BuildMangroveDock()
                }
            };
        }

        private static StructureTemplate BuildForestShelter()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int dy = dx == 0 && dz == 0 ? 2 : 1;
                    blocks.Add(new StructureBlock(dx, dy, dz, BlockType.OakLog));
                }
            }

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 3, dz, BlockType.OakLeaves, StructurePlacementMode.AirOnly));
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildPlainsWell()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        blocks.Add(new StructureBlock(dx, 1, dz, BlockType.Water));
                    }
                    else
                    {
                        blocks.Add(new StructureBlock(dx, 1, dz, BlockType.Cobblestone));
                    }
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildDesertCairn()
        {
            return new StructureTemplate
            {
                FootprintRadius = 1,
                Blocks = new[]
                {
                    new StructureBlock(0, 1, 0, BlockType.Sandstone),
                    new StructureBlock(0, 2, 0, BlockType.Sandstone),
                    new StructureBlock(0, 3, 0, BlockType.Sandstone)
                }
            };
        }

        private static StructureTemplate BuildSwampShrine()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Mud));
                }
            }

            blocks.Add(new StructureBlock(0, 1, 0, BlockType.MossStone));
            blocks.Add(new StructureBlock(0, 2, 0, BlockType.MossStone));
            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildBeachPost()
        {
            var blocks = new List<StructureBlock>
            {
                new StructureBlock(0, 1, 0, BlockType.PalmLog),
                new StructureBlock(0, 2, 0, BlockType.PalmLog)
            };

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    blocks.Add(new StructureBlock(dx, 2, dz, BlockType.OakPlank, StructurePlacementMode.AirOnly));
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildMountainCairn()
        {
            return new StructureTemplate
            {
                FootprintRadius = 1,
                Blocks = new[]
                {
                    new StructureBlock(0, 1, 0, BlockType.Stone),
                    new StructureBlock(0, 2, 0, BlockType.Stone),
                    new StructureBlock(0, 3, 0, BlockType.Stone)
                }
            };
        }

        private static StructureTemplate BuildPlainsCottage()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Cobblestone));
                }
            }

            for (int dy = 1; dy <= 3; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        bool perimeter = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                        if (!perimeter)
                        {
                            continue;
                        }

                        if (dz == 2 && dx == 0)
                        {
                            continue;
                        }

                        BlockType type = dy == 2 && Math.Abs(dx) == 2 && Math.Abs(dz) <= 1
                            ? BlockType.Glass
                            : BlockType.OakPlank;
                        blocks.Add(new StructureBlock(dx, dy, dz, type, StructurePlacementMode.AirOnly));
                    }
                }
            }

            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildForestWatchtower()
        {
            var blocks = new List<StructureBlock>();
            for (int dy = 1; dy <= 6; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        bool perimeter = Math.Abs(dx) == 1 || Math.Abs(dz) == 1;
                        if (!perimeter)
                        {
                            continue;
                        }

                        blocks.Add(new StructureBlock(dx, dy, dz, BlockType.OakLog));
                    }
                }
            }

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 6, dz, BlockType.OakPlank, StructurePlacementMode.AirOnly));
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildDesertShrine()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Sandstone));
                }
            }

            for (int dy = 1; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        bool perimeter = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                        if (!perimeter)
                        {
                            continue;
                        }

                        blocks.Add(new StructureBlock(dx, dy, dz, BlockType.Sandstone));
                    }
                }
            }

            blocks.Add(new StructureBlock(0, 1, 0, BlockType.GoldBlock));
            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildSwampAltar()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (Math.Abs(dx) <= 0 && Math.Abs(dz) <= 0)
                    {
                        blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Water));
                    }
                    else if (Math.Abs(dx) == 1 && Math.Abs(dz) == 1)
                    {
                        blocks.Add(new StructureBlock(dx, 1, dz, BlockType.MossStone));
                    }
                    else
                    {
                        blocks.Add(new StructureBlock(dx, 1, dz, BlockType.MossStone));
                    }
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildSnowyHut()
        {
            var blocks = new List<StructureBlock>();
            for (int dy = 1; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        bool perimeter = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                        if (!perimeter)
                        {
                            continue;
                        }

                        if (dz == 2 && dy == 1)
                        {
                            continue;
                        }

                        blocks.Add(new StructureBlock(dx, dy, dz, BlockType.Snow));
                    }
                }
            }

            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildVillageOutpost()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Cobblestone));
                }
            }

            for (int dy = 1; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        bool perimeter = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                        if (!perimeter)
                        {
                            continue;
                        }

                        blocks.Add(new StructureBlock(dx, dy, dz, BlockType.OakPlank, StructurePlacementMode.AirOnly));
                    }
                }
            }

            blocks.Add(new StructureBlock(-2, 1, 0, BlockType.OakPlank));
            blocks.Add(new StructureBlock(2, 1, 0, BlockType.OakPlank));
            blocks.Add(new StructureBlock(0, 1, -2, BlockType.OakPlank));
            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildBadlandsSpire()
        {
            return new StructureTemplate
            {
                FootprintRadius = 1,
                Blocks = new[]
                {
                    new StructureBlock(0, 1, 0, BlockType.RedSand),
                    new StructureBlock(0, 2, 0, BlockType.Sandstone),
                    new StructureBlock(0, 3, 0, BlockType.Sandstone),
                    new StructureBlock(0, 4, 0, BlockType.RedSand)
                }
            };
        }

        private static StructureTemplate BuildMushroomCircle()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        blocks.Add(new StructureBlock(dx, 1, dz, BlockType.Glowshroom));
                    }
                    else
                    {
                        blocks.Add(new StructureBlock(dx, 1, dz, (dx + dz) % 2 == 0 ? BlockType.MushroomRed : BlockType.MushroomBrown));
                    }
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildVolcanicVent()
        {
            return new StructureTemplate
            {
                FootprintRadius = 1,
                Blocks = new[]
                {
                    new StructureBlock(0, 0, 0, BlockType.Basalt),
                    new StructureBlock(0, 1, 0, BlockType.Obsidian),
                    new StructureBlock(0, 2, 0, BlockType.MagmaBlock)
                }
            };
        }

        private static StructureTemplate BuildMangroveDock()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                blocks.Add(new StructureBlock(dx, 0, 0, BlockType.Mud));
                blocks.Add(new StructureBlock(dx, 1, 0, BlockType.WillowLog));
            }

            blocks.Add(new StructureBlock(-2, 1, 1, BlockType.WillowLog));
            blocks.Add(new StructureBlock(2, 1, 1, BlockType.WillowLog));
            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }
    }
}
