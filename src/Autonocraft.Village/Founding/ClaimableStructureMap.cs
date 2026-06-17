using Autonocraft.World.Structures;

namespace Autonocraft.Village
{
    public static class ClaimableStructureMap
    {
        public static bool IsClaimable(string structureId)
            => structureId is "PlainsCottage" or "VillageOutpost" or "ForestShelter";

        public static string GetBlueprintId(string structureId)
            => structureId switch
            {
                "PlainsCottage" => "peasant_house",
                "VillageOutpost" => "peasant_house",
                "ForestShelter" => "lumber_camp",
                _ => "peasant_house"
            };

        public static string GetDefaultVillageName(string structureId)
            => structureId switch
            {
                "PlainsCottage" => "Cottage Outpost",
                "VillageOutpost" => "Village Outpost",
                "ForestShelter" => "Forest Camp",
                _ => $"{structureId} Outpost"
            };
    }
}
