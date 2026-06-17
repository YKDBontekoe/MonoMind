using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal static class VillagerCarryHelper
    {
        public static void TryOffloadCarryToOutputChest(Villager villager, VillageContext context)
        {
            if (context.Village == null)
            {
                return;
            }

            BuildingKind? kind = villager.CurrentJob switch
            {
                JobType.Lumber => BuildingKind.LumberCamp,
                JobType.Mine => BuildingKind.Quarry,
                JobType.Farm => BuildingKind.FarmPlot,
                _ => null
            };

            if (!kind.HasValue)
            {
                return;
            }

            OutputChest? chest = null;
            if (villager.AssignedBuildingId.HasValue &&
                context.Village.TryGetOutputChestForBuilding(villager.AssignedBuildingId.Value, out var buildingChest))
            {
                chest = buildingChest;
            }
            else
            {
                chest = context.Village.GetNearestOutputChest(kind.Value, villager.Position);
            }

            if (chest != null)
            {
                HaulLogistics.OffloadInventoryToChest(villager, chest);
            }
        }
    }
}
