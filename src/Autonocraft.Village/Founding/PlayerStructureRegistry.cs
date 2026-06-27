using System;
using System.Collections.Generic;
using Autonocraft.Domain.Village;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Village
{
    public static class PlayerStructureRegistry
    {
        private static readonly BuildingBlueprint[] AllBlueprints = BuildBlueprints();
        private static readonly Dictionary<string, BuildingBlueprint> ById = BuildLookup();

        public static IReadOnlyList<BuildingBlueprint> All => AllBlueprints;

        public static bool TryGet(string id, out BuildingBlueprint blueprint)
            => ById.TryGetValue(id, out blueprint!);

        public static BuildingBlueprint Get(string id) => ById[id];

        private static Dictionary<string, BuildingBlueprint> BuildLookup()
        {
            var map = new Dictionary<string, BuildingBlueprint>();
            foreach (var blueprint in AllBlueprints)
            {
                map[blueprint.Id] = blueprint;
            }

            return map;
        }

        private static BuildingBlueprint[] BuildBlueprints()
        {
            return new[]
            {
                new BuildingBlueprint
                {
                    Id = "town_heart",
                    Kind = BuildingKind.TownHeart,
                    DisplayName = "Town Heart",
                    Template = BuildTownHeart(),
                    Costs = new[]
                    {
                        new BlockCost(BlockType.OakPlank, 8),
                        new BlockCost(BlockType.Cobblestone, 4)
                    },
                    HousingProvided = 4,
                    PopulationCapBonus = 2,
                    StorageSlots = 9
                },
                new BuildingBlueprint
                {
                    Id = "peasant_house",
                    Kind = BuildingKind.House,
                    DisplayName = "Peasant House",
                    Template = BuildPeasantHouse(),
                    Costs = new[]
                    {
                        new BlockCost(BlockType.OakPlank, 24),
                        new BlockCost(BlockType.Cobblestone, 4)
                    },
                    HousingProvided = 2,
                    PopulationCapBonus = 2
                },
                new BuildingBlueprint
                {
                    Id = "storage_crate",
                    Kind = BuildingKind.Storage,
                    DisplayName = "Storage Crate",
                    Template = BuildStorageCrate(),
                    Costs = new[] { new BlockCost(BlockType.OakPlank, 16) },
                    StorageSlots = 18
                },
                new BuildingBlueprint
                {
                    Id = "lumber_camp",
                    Kind = BuildingKind.LumberCamp,
                    DisplayName = "Lumber Camp",
                    Template = BuildLumberCamp(),
                    Costs = new[]
                    {
                        new BlockCost(BlockType.OakPlank, 12),
                        new BlockCost(BlockType.Cobblestone, 4)
                    }
                },
                new BuildingBlueprint
                {
                    Id = "workshop",
                    Kind = BuildingKind.Workshop,
                    DisplayName = "Workshop",
                    Template = BuildWorkshop(),
                    Costs = new[]
                    {
                        new BlockCost(BlockType.OakPlank, 20),
                        new BlockCost(BlockType.Cobblestone, 12),
                        new BlockCost(BlockType.Stone, 8)
                    }
                },
                new BuildingBlueprint
                {
                    Id = "farm_plot",
                    Kind = BuildingKind.FarmPlot,
                    DisplayName = "Farm Plot",
                    Template = BuildFarmPlot(),
                    Costs = new[]
                    {
                        new BlockCost(BlockType.Dirt, 16),
                        new BlockCost(BlockType.OakPlank, 4)
                    }
                },
                new BuildingBlueprint
                {
                    Id = "quarry",
                    Kind = BuildingKind.Quarry,
                    DisplayName = "Quarry",
                    Template = BuildQuarry(),
                    Costs = new[]
                    {
                        new BlockCost(BlockType.Cobblestone, 16),
                        new BlockCost(BlockType.OakPlank, 8)
                    }
                },
                new BuildingBlueprint
                {
                    Id = "kitchen",
                    Kind = BuildingKind.Kitchen,
                    DisplayName = "Kitchen",
                    Template = BuildKitchen(),
                    Costs = new[]
                    {
                        new BlockCost(BlockType.OakPlank, 16),
                        new BlockCost(BlockType.Cobblestone, 8)
                    }
                },
                new BuildingBlueprint
                {
                    Id = "well",
                    Kind = BuildingKind.Well,
                    DisplayName = "Village Well",
                    Template = BuildWell(),
                    Costs = new[] { new BlockCost(BlockType.Cobblestone, 12) }
                },
                new BuildingBlueprint
                {
                    Id = "market",
                    Kind = BuildingKind.Market,
                    DisplayName = "Market Stall",
                    Template = BuildMarket(),
                    Costs = new[]
                    {
                        new BlockCost(BlockType.OakPlank, 20),
                        new BlockCost(BlockType.Cobblestone, 4)
                    },
                    StorageSlots = 9
                }
            };
        }

        private static void AddPost(List<StructureBlock> blocks, int x, int z, int height, BlockType post = BlockType.OakLog, bool lantern = false)
        {
            for (int y = 1; y <= height; y++)
            {
                blocks.Add(new StructureBlock(x, y, z, post));
            }

            if (lantern)
            {
                blocks.Add(new StructureBlock(x, height + 1, z, BlockType.Lantern));
            }
        }

        private static void AddBorder(List<StructureBlock> blocks, int radius, BlockType blockType, int y = 0)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius)
                    {
                        continue;
                    }

                    blocks.Add(new StructureBlock(dx, y, dz, blockType));
                }
            }
        }

        private static StructureTemplate BuildTownHeart()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    bool edge = Math.Abs(dx) == 3 || Math.Abs(dz) == 3;
                    bool cross = dx == 0 || dz == 0;
                    blocks.Add(new StructureBlock(dx, 0, dz, edge ? BlockType.Cobblestone : cross ? BlockType.Gravel : BlockType.OakPlank));
                }
            }

            AddPost(blocks, -3, -3, 2);
            AddPost(blocks, 3, -3, 2);
            AddPost(blocks, -3, 3, 2);
            AddPost(blocks, 3, 3, 2);

            for (int x = -2; x <= 2; x++)
            {
                blocks.Add(new StructureBlock(x, 3, -3, BlockType.OakPlank));
                blocks.Add(new StructureBlock(x, 3, 3, BlockType.OakPlank));
            }

            for (int z = -2; z <= 2; z++)
            {
                blocks.Add(new StructureBlock(-3, 3, z, BlockType.OakPlank));
                blocks.Add(new StructureBlock(3, 3, z, BlockType.OakPlank));
            }

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 4, dz, BlockType.OakPlank));
                }
            }

            blocks.Add(new StructureBlock(0, 3, 0, BlockType.OakLog));
            blocks.Add(new StructureBlock(0, 2, 0, BlockType.Lantern));
            blocks.Add(new StructureBlock(-1, 1, 2, BlockType.StationBench));
            blocks.Add(new StructureBlock(1, 1, 2, BlockType.Chest));
            blocks.Add(new StructureBlock(0, 0, -4, BlockType.Gravel));
            blocks.Add(new StructureBlock(0, 0, 4, BlockType.Gravel));
            blocks.Add(new StructureBlock(-4, 0, 0, BlockType.Gravel));
            blocks.Add(new StructureBlock(4, 0, 0, BlockType.Gravel));

            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildPeasantHouse()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, Math.Abs(dx) == 2 || Math.Abs(dz) == 2 ? BlockType.Cobblestone : BlockType.OakPlank));
                }
            }

            blocks.Add(new StructureBlock(-1, 0, -3, BlockType.Gravel));
            blocks.Add(new StructureBlock(0, 0, -3, BlockType.Gravel));
            blocks.Add(new StructureBlock(1, 0, -3, BlockType.Gravel));
            blocks.Add(new StructureBlock(-1, 1, -3, BlockType.OakLog));
            blocks.Add(new StructureBlock(1, 1, -3, BlockType.OakLog));
            AddPost(blocks, -3, -3, 2, lantern: true);
            AddPost(blocks, 3, -3, 2, lantern: true);
            AddPost(blocks, -3, 3, 2);
            AddPost(blocks, 3, 3, 2);

            for (int y = 1; y <= 3; y++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    BlockType front = dx == 0 && y <= 2 ? BlockType.Air : y == 2 && (dx == -1 || dx == 1) ? BlockType.Glass : BlockType.OakPlank;
                    BlockType back = y == 2 && (dx == -1 || dx == 1) ? BlockType.Glass : BlockType.OakPlank;
                    blocks.Add(new StructureBlock(dx, y, -2, front));
                    blocks.Add(new StructureBlock(dx, y, 2, back));
                }

                for (int dz = -1; dz <= 1; dz++)
                {
                    BlockType leftSide = y == 2 && dz == 0 ? BlockType.Glass : BlockType.OakPlank;
                    BlockType rightSide = dz == -1 && y >= 1
                        ? BlockType.Cobblestone
                        : y == 2 && dz == 0
                            ? BlockType.Glass
                            : BlockType.OakPlank;
                    blocks.Add(new StructureBlock(-2, y, dz, leftSide));
                    blocks.Add(new StructureBlock(2, y, dz, rightSide));
                }
            }

            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    int roofY = 4 + Math.Abs(dx) / 2;
                    blocks.Add(new StructureBlock(dx, roofY, dz, Math.Abs(dx) == 2 ? BlockType.OakPlank : BlockType.OakLog));
                    if (Math.Abs(dx) <= 1)
                    {
                        blocks.Add(new StructureBlock(dx, roofY + 1, dz, BlockType.OakPlank));
                    }
                }
            }

            blocks.Add(new StructureBlock(-1, 1, 1, BlockType.Chest));
            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildStorageCrate()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, Math.Abs(dx) == 2 || Math.Abs(dz) == 2 ? BlockType.Gravel : BlockType.Cobblestone));
                }
            }

            AddPost(blocks, -2, -2, 2, lantern: true);
            AddPost(blocks, 2, -2, 2, lantern: true);
            AddPost(blocks, -2, 2, 2);
            AddPost(blocks, 2, 2, 2);
            blocks.Add(new StructureBlock(0, 0, -3, BlockType.Gravel));
            for (int dx = -1; dx <= 1; dx++)
            {
                blocks.Add(new StructureBlock(dx, 1, -1, BlockType.Chest));
                blocks.Add(new StructureBlock(dx, 1, 1, BlockType.OakLog));
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 3, dz, BlockType.OakPlank));
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildLumberCamp()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, Math.Abs(dx) == 3 || Math.Abs(dz) == 3 ? BlockType.Gravel : BlockType.Dirt));
                }
            }

            AddPost(blocks, -3, -3, 2, lantern: true);
            AddPost(blocks, 3, -3, 2, lantern: true);
            blocks.Add(new StructureBlock(0, 0, -4, BlockType.Gravel));
            blocks.Add(new StructureBlock(0, 1, 1, BlockType.OakLog));
            for (int dx = -2; dx <= 1; dx++)
            {
                blocks.Add(new StructureBlock(dx, 1, -1, BlockType.OakLog));
                blocks.Add(new StructureBlock(dx, 2, -1, BlockType.OakLog));
            }

            blocks.Add(new StructureBlock(2, 1, 1, BlockType.OakLog));
            blocks.Add(new StructureBlock(2, 2, 1, BlockType.OakLog));
            blocks.Add(new StructureBlock(0, 1, 2, BlockType.Chest));
            blocks.Add(new StructureBlock(-2, 1, 2, BlockType.OakLog));
            blocks.Add(new StructureBlock(-2, 2, 2, BlockType.OakLog));
            blocks.Add(new StructureBlock(1, 1, 2, BlockType.OakLog));
            blocks.Add(new StructureBlock(1, 2, 2, BlockType.OakLog));
            for (int dx = -2; dx <= 1; dx++)
            {
                for (int dz = 1; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 3, dz, BlockType.OakPlank));
                }
            }

            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildWorkshop()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, Math.Abs(dx) == 2 || Math.Abs(dz) == 2 ? BlockType.Cobblestone : BlockType.Gravel));
                }
            }

            AddPost(blocks, -2, -2, 3, lantern: true);
            AddPost(blocks, 2, -2, 3, lantern: true);
            AddPost(blocks, -2, 2, 3);
            AddPost(blocks, 2, 2, 3);
            blocks.Add(new StructureBlock(0, 0, -3, BlockType.Gravel));
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 4, dz, BlockType.OakPlank));
                }
            }

            blocks.Add(new StructureBlock(-1, 1, -1, BlockType.StationBench));
            blocks.Add(new StructureBlock(1, 1, -1, BlockType.StationForge));
            blocks.Add(new StructureBlock(-1, 1, 1, BlockType.StationCrucible));
            blocks.Add(new StructureBlock(1, 1, 1, BlockType.Chest));
            blocks.Add(new StructureBlock(2, 1, -2, BlockType.Cobblestone));
            blocks.Add(new StructureBlock(2, 2, -2, BlockType.Cobblestone));
            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildFarmPlot()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    bool isEdge = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                    bool isCenter = dx == 0 && dz == 0;

                    if (isEdge)
                    {
                        blocks.Add(new StructureBlock(dx, 0, dz, BlockType.OakLog));
                    }
                    else if (isCenter)
                    {
                        blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Water));
                    }
                    else
                    {
                        blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Dirt));
                    }
                }
            }

            blocks.Add(new StructureBlock(-3, 0, 0, BlockType.Gravel));
            blocks.Add(new StructureBlock(3, 0, 0, BlockType.Gravel));
            blocks.Add(new StructureBlock(0, 0, -3, BlockType.Gravel));
            blocks.Add(new StructureBlock(0, 0, 3, BlockType.Gravel));
            AddPost(blocks, -2, -2, 2, lantern: true);
            AddPost(blocks, 2, -2, 2, lantern: true);
            blocks.Add(new StructureBlock(-2, 1, 0, BlockType.OakLog));
            blocks.Add(new StructureBlock(2, 1, 0, BlockType.OakLog));

            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildQuarry()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    bool edge = Math.Abs(dx) == 3 || Math.Abs(dz) == 3;
                    blocks.Add(new StructureBlock(dx, 0, dz, edge ? BlockType.Cobblestone : BlockType.Air));
                    
                    if (edge && (Math.Abs(dx) != 3 || Math.Abs(dz) != 3))
                    {
                        blocks.Add(new StructureBlock(dx, 1, dz, BlockType.Cobblestone));
                    }
                }
            }

            blocks.Add(new StructureBlock(0, 0, 1, BlockType.Rope));
            blocks.Add(new StructureBlock(0, -1, 1, BlockType.Rope));
            blocks.Add(new StructureBlock(0, -2, 1, BlockType.Rope));
            AddPost(blocks, -3, -3, 2, lantern: true);
            AddPost(blocks, 3, -3, 2, lantern: true);

            blocks.Add(new StructureBlock(-1, 1, 0, BlockType.OakLog));
            blocks.Add(new StructureBlock(-1, 2, 0, BlockType.OakLog));
            blocks.Add(new StructureBlock(-1, 3, 0, BlockType.OakLog));
            blocks.Add(new StructureBlock(0, 3, 0, BlockType.OakLog));
            blocks.Add(new StructureBlock(0, 2, 0, BlockType.Rope));
            blocks.Add(new StructureBlock(2, 1, -1, BlockType.Chest));
            
            return new StructureTemplate { FootprintRadius = 3, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildKitchen()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, Math.Abs(dx) == 2 || Math.Abs(dz) == 2 ? BlockType.Gravel : BlockType.Cobblestone));
                }
            }

            AddPost(blocks, -2, -2, 2, lantern: true);
            AddPost(blocks, 2, -2, 2, lantern: true);
            AddPost(blocks, -2, 2, 2);
            AddPost(blocks, 2, 2, 2);
            blocks.Add(new StructureBlock(0, 0, -3, BlockType.Gravel));
            blocks.Add(new StructureBlock(0, 1, 1, BlockType.StationSmoker));
            blocks.Add(new StructureBlock(-1, 1, 0, BlockType.OakPlank));
            blocks.Add(new StructureBlock(1, 1, 0, BlockType.OakPlank));
            blocks.Add(new StructureBlock(0, 1, -1, BlockType.Chest));
            blocks.Add(new StructureBlock(2, 1, 1, BlockType.Cobblestone));
            blocks.Add(new StructureBlock(2, 2, 1, BlockType.Cobblestone));
            blocks.Add(new StructureBlock(2, 3, 1, BlockType.Cobblestone));
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 3, dz, BlockType.OakPlank));
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildWell()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    bool ring = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                    if (ring)
                    {
                        blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Gravel));
                        continue;
                    }

                    bool basinEdge = Math.Abs(dx) == 1 || Math.Abs(dz) == 1;
                    blocks.Add(new StructureBlock(dx, 0, dz, basinEdge ? BlockType.Cobblestone : BlockType.Water));
                }
            }

            AddPost(blocks, -1, -1, 3);
            AddPost(blocks, 1, -1, 3);
            AddPost(blocks, -1, 1, 3, lantern: true);
            AddPost(blocks, 1, 1, 3, lantern: true);
            blocks.Add(new StructureBlock(0, 0, -3, BlockType.Gravel));
            blocks.Add(new StructureBlock(0, 0, 3, BlockType.Gravel));
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 4, dz, BlockType.OakPlank));
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildMarket()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, Math.Abs(dx) == 2 || Math.Abs(dz) == 2 ? BlockType.Gravel : BlockType.Cobblestone));
                }
            }

            AddPost(blocks, -2, -2, 2, lantern: true);
            AddPost(blocks, 2, -2, 2, lantern: true);
            AddPost(blocks, -2, 2, 2, lantern: true);
            AddPost(blocks, 2, 2, 2, lantern: true);
            blocks.Add(new StructureBlock(0, 0, -3, BlockType.Gravel));
            blocks.Add(new StructureBlock(-1, 1, 0, BlockType.OakPlank));
            blocks.Add(new StructureBlock(0, 1, 0, BlockType.OakPlank));
            blocks.Add(new StructureBlock(1, 1, 0, BlockType.OakPlank));
            blocks.Add(new StructureBlock(0, 1, 1, BlockType.Chest));
            for (int dx = -1; dx <= 1; dx++)
            {
                BlockType canopy = dx == 0 ? BlockType.RedStainedGlass : BlockType.OakPlank;
                blocks.Add(new StructureBlock(dx, 3, 1, canopy));
                blocks.Add(new StructureBlock(dx, 3, 0, canopy));
                blocks.Add(new StructureBlock(dx, 2, -1, canopy));
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }
    }
}
