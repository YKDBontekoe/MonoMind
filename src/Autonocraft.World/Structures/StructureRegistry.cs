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
                Def("ForestShelter", StructureTier.Small, ProceduralStructures.ForestShelter, BiomeType.Forest, BiomeType.Plains, BiomeType.Jungle),
                Def("PlainsWell", StructureTier.Small, ProceduralStructures.PlainsWell, BiomeType.Plains),
                Def("DesertCairn", StructureTier.Small, ProceduralStructures.DesertCairn, BiomeType.Desert),
                Def("SwampShrine", StructureTier.Small, ProceduralStructures.SwampShrine, BiomeType.Swamp),
                Def("BeachPost", StructureTier.Small, ProceduralStructures.BeachPost, BiomeType.Beach),
                Def("MountainCairn", StructureTier.Small, ProceduralStructures.MountainCairn, BiomeType.Mountains),
                Def("BadlandsSpire", StructureTier.Small, ProceduralStructures.BadlandsSpire, BiomeType.Badlands),
                Def("MushroomCircle", StructureTier.Small, ProceduralStructures.MushroomCircle, BiomeType.MushroomForest),
                Def("VolcanicVent", StructureTier.Small, ProceduralStructures.VolcanicVent, BiomeType.Volcanic),
                Def("MangroveDock", StructureTier.Small, ProceduralStructures.MangroveDock, BiomeType.Mangrove),
                Def("PlainsCottage", StructureTier.Medium, ProceduralStructures.PlainsCottage, BiomeType.Plains),
                Def("ForestWatchtower", StructureTier.Medium, ProceduralStructures.ForestWatchtower, BiomeType.Forest),
                Def("DesertShrine", StructureTier.Medium, ProceduralStructures.DesertShrine, BiomeType.Desert),
                Def("SwampAltar", StructureTier.Medium, ProceduralStructures.SwampAltar, BiomeType.Swamp),
                Def("SnowyHut", StructureTier.Medium, ProceduralStructures.SnowyHut, BiomeType.SnowyPeaks),
                Def("VillageOutpost", StructureTier.Medium, ProceduralStructures.VillageOutpost, BiomeType.Plains, BiomeType.Forest, BiomeType.Jungle),
                Def("AbandonedCastle", StructureTier.Large, ProceduralStructures.AbandonedCastle, BiomeType.Forest, BiomeType.Plains, BiomeType.Mountains),
                Def("RuinedDungeon", StructureTier.Large, ProceduralStructures.RuinedDungeon, BiomeType.Desert, BiomeType.Badlands, BiomeType.Volcanic, BiomeType.Swamp),
                Def("MegaCastle", StructureTier.Mega, ProceduralStructures.MegaCastle, BiomeType.Forest, BiomeType.Plains, BiomeType.Mountains),
                Def("MegaCitadel", StructureTier.Mega, ProceduralStructures.MegaCitadel, BiomeType.Desert, BiomeType.Badlands, BiomeType.Volcanic, BiomeType.Swamp)
            };
        }

        private static StructureDefinition Def(
            string id,
            StructureTier tier,
            StructureTemplateGenerator generator,
            params BiomeType[] biomes)
        {
            return new StructureDefinition
            {
                Id = id,
                Tier = tier,
                AllowedBiomes = biomes,
                GenerateTemplate = generator,
                Template = new StructureTemplate { FootprintRadius = TierDefaultRadius(tier) }
            };
        }

        private static int TierDefaultRadius(StructureTier tier) => tier switch
        {
            StructureTier.Small => 3,
            StructureTier.Medium => 5,
            StructureTier.Large => 12,
            StructureTier.Mega => 40,
            _ => 3
        };
    }
}
