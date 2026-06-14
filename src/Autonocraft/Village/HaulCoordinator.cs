using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;

namespace Autonocraft.Village
{
    public sealed class HaulCoordinator
    {
        private readonly VillagerManager _villagers;

        public HaulCoordinator(VillagerManager villagers)
        {
            _villagers = villagers;
        }

        public void TryAssignHaulers(Village village)
        {
            foreach (var villagerId in village.VillagerIds)
            {
                if (!_villagers.TryGet(villagerId, out var hauler) || hauler.CurrentJob != JobType.Idle)
                {
                    continue;
                }

                if (hauler.Role != VillagerRole.Hauler && hauler.Role != VillagerRole.Peasant)
                {
                    continue;
                }

                if (!TryFindHaulWork(village, out var chest, out var sourceVillager, out var pickupPos, hauler.Id))
                {
                    continue;
                }

                AssignHaulJob(hauler, pickupPos, chest, sourceVillager);
            }
        }

        public bool TryAssignHaulWork(Village village, Villager hauler)
        {
            if (!TryFindHaulWork(village, out var chest, out var sourceVillager, out var pickupPos, hauler.Id))
            {
                return false;
            }

            AssignHaulJob(hauler, pickupPos, chest, sourceVillager);
            return true;
        }

        private static void AssignHaulJob(Villager hauler, Vector3 pickupPos, OutputChest? chest, Villager? sourceVillager)
        {
            if (chest != null)
            {
                hauler.AssignHaulJob(pickupPos, chest.Id, null);
            }
            else if (sourceVillager != null)
            {
                hauler.AssignHaulJob(pickupPos, null, sourceVillager.Id);
            }
        }

        private bool TryFindHaulWork(
            Village village,
            out OutputChest? chest,
            out Villager? sourceVillager,
            out Vector3 pickupPos,
            int? excludeVillagerId = null)
        {
            chest = village.FindFullestOutputChest();
            if (chest != null)
            {
                sourceVillager = null;
                pickupPos = chest.Position;
                return true;
            }

            foreach (var villagerId in village.VillagerIds)
            {
                if (!_villagers.TryGet(villagerId, out var worker))
                {
                    continue;
                }

                if (excludeVillagerId.HasValue && worker.Id == excludeVillagerId.Value)
                {
                    continue;
                }

                if (worker.CurrentJob is not (JobType.Lumber or JobType.Mine or JobType.Farm))
                {
                    continue;
                }

                if (!HaulLogistics.IsCarryFull(worker.Inventory))
                {
                    continue;
                }

                if (IsVillagerReservedForHaul(worker.Id))
                {
                    continue;
                }

                sourceVillager = worker;
                pickupPos = worker.Position;
                chest = null;
                return true;
            }

            sourceVillager = null;
            pickupPos = default;
            chest = null;
            return false;
        }

        private bool IsVillagerReservedForHaul(int villagerId)
        {
            foreach (var villager in _villagers.All)
            {
                if (villager.CurrentJob == JobType.Haul && villager.HaulSourceVillagerId == villagerId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
