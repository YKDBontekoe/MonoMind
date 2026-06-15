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
                        new BlockCost(BlockType.Cobblestone, 32),
                        new BlockCost(BlockType.OakPlank, 16)
                    },
                    HousingProvided = 2,
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
                        new BlockCost(BlockType.Cobblestone, 8)
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

        private static StructureTemplate BuildTownHeart()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Cobblestone));
                }
            }

            blocks.Add(new StructureBlock(0, 1, 0, BlockType.OakPlank));
            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildPeasantHouse()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Cobblestone));
                }
            }

            for (int y = 1; y <= 3; y++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    blocks.Add(new StructureBlock(dx, y, -2, BlockType.OakPlank));
                    blocks.Add(new StructureBlock(dx, y, 2, BlockType.OakPlank));
                }

                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(-2, y, dz, BlockType.OakPlank));
                    blocks.Add(new StructureBlock(2, y, dz, BlockType.OakPlank));
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildStorageCrate()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.OakPlank));
                }
            }

            blocks.Add(new StructureBlock(0, 1, 0, BlockType.OakLog));
            return new StructureTemplate { FootprintRadius = 1, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildLumberCamp()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -1; dx <= 1; dx++)
            {
                blocks.Add(new StructureBlock(dx, 0, 0, BlockType.OakPlank));
                blocks.Add(new StructureBlock(dx, 1, 0, BlockType.OakLog));
            }

            blocks.Add(new StructureBlock(0, 0, 1, BlockType.OakLog));
            return new StructureTemplate { FootprintRadius = 1, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildWorkshop()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Cobblestone));
                }
            }

            blocks.Add(new StructureBlock(0, 1, 0, BlockType.StationBench));
            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildFarmPlot()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.Dirt));
                }
            }

            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildQuarry()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    bool edge = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                    blocks.Add(new StructureBlock(dx, 0, dz, edge ? BlockType.Cobblestone : BlockType.Air));
                }
            }

            blocks.Add(new StructureBlock(0, 1, 0, BlockType.OakLog));
            return new StructureTemplate { FootprintRadius = 2, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildKitchen()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    blocks.Add(new StructureBlock(dx, 0, dz, BlockType.OakPlank));
                }
            }

            blocks.Add(new StructureBlock(0, 1, 0, BlockType.StationBench));
            return new StructureTemplate { FootprintRadius = 1, Blocks = blocks.ToArray() };
        }

        private static StructureTemplate BuildWell()
        {
            return new StructureTemplate
            {
                FootprintRadius = 1,
                Blocks = new[]
                {
                    new StructureBlock(0, 0, 0, BlockType.Cobblestone),
                    new StructureBlock(0, 1, 0, BlockType.Cobblestone)
                }
            };
        }

        private static StructureTemplate BuildMarket()
        {
            var blocks = new List<StructureBlock>();
            for (int dx = -1; dx <= 1; dx++)
            {
                blocks.Add(new StructureBlock(dx, 0, 0, BlockType.OakPlank));
            }

            blocks.Add(new StructureBlock(0, 1, 0, BlockType.OakPlank));
            return new StructureTemplate { FootprintRadius = 1, Blocks = blocks.ToArray() };
        }
    }
}
