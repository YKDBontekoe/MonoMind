using System;
using System.Numerics;
using Autonocraft.Domain.Village;

namespace Autonocraft.Village
{
    public static class BuildingEffects
    {
        public const int BaseGatherScanRadius = 24;
        public const int LumberCampScanBonus = 16;
        public const float LumberCampWorkSpeedBonus = 1.35f;
        public const int QuarryScanRadius = 12;
        public const int QuarryDepthLimit = 24;
        public const float WorkshopWorkRadius = 4f;
        public const float FarmPlotWorkRadius = 4f;

        public static int GetGatherScanRadius(BuildingKind? originKind)
            => originKind == BuildingKind.LumberCamp
                ? BaseGatherScanRadius + LumberCampScanBonus
                : BaseGatherScanRadius;

        public static bool IsWithinWorkRadius(VillageBuilding building, Vector3 position, float radius)
        {
            var center = GetWorkPosition(building);
            var offset = position - center;
            offset.Y = 0f;
            return offset.LengthSquared() <= radius * radius;
        }

        public static Vector3 GetWorkPosition(VillageBuilding building)
            => new Vector3(building.AnchorX + 0.5f, building.AnchorY, building.AnchorZ + 0.5f);
    }
}
