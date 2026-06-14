using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.Village
{
    public static class VillageGuidance
    {
        public static string GetNextBestAction(VillageEntity village, VillagerManager villagers)
        {
            if (village.Population == 0)
            {
                return "Recruit your first villager (R)";
            }

            if (village.FoodStock <= village.Population * 0.5f)
            {
                return "Food is low — build a farm plot or assign farmers";
            }

            int idle = 0;
            foreach (int id in village.VillagerIds)
            {
                if (villagers.TryGet(id, out var v) && v.CurrentJob == JobType.Idle)
                {
                    idle++;
                }
            }

            if (idle > 0)
            {
                return $"{idle} villager(s) idle — assign jobs on PEOPLE tab";
            }

            foreach (var site in village.BuildingSites)
            {
                if (!site.IsComplete)
                {
                    return "Builders are working — queue more on BUILD tab";
                }
            }

            if (village.Population < village.PopulationCap && village.CanRecruit(false))
            {
                return "Recruit more workers to grow the settlement";
            }

            return "Settlement running smoothly — set goals on GOALS tab";
        }
    }
}
