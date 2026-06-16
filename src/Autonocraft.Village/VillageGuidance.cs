using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.Village
{
    public static class VillageGuidance
    {
        public static string GetNextBestAction(VillageEntity village, VillagerManager villagers, Vector3? playerPos = null)
        {
            VillageSettlementHealth.SyncPopulationRegistry(village, villagers);
            int livePopulation = VillageSettlementHealth.GetLivePopulation(village, villagers);

            if (livePopulation == 0)
            {
                if (VillageSettlementHealth.HasEstablishedSettlement(village))
                {
                    if (playerPos.HasValue && VillageSettlementHealth.IsPlayerNearTownHeart(village, playerPos.Value))
                    {
                        return "Settlers missing — click SUMMON SETTLERS or wait a moment, then open PEOPLE tab";
                    }

                    return "Settlers missing — walk to Town Heart, then open PEOPLE tab";
                }

                return "Found or claim a settlement to welcome your first settler";
            }

            if (village.FoodStock <= livePopulation * 0.5f)
            {
                return "Food is low — build a farm plot (BUILD tab) or assign FARM jobs";
            }

            int idle = 0;
            foreach (var villager in villagers.All)
            {
                if (villager.VillageId == village.Id && villager.CurrentJob == JobType.Idle)
                {
                    idle++;
                }
            }

            if (idle > 0)
            {
                return $"{idle} villager(s) idle — open PEOPLE tab and assign LUMBER, BUILD, or FARM";
            }

            foreach (var site in village.BuildingSites)
            {
                if (!site.IsComplete)
                {
                    return "Construction queued — assign BUILD on PEOPLE tab if nobody is working";
                }
            }

            if (livePopulation < village.PopulationCap && village.CanRecruit(villagers, false))
            {
                return "Press R or RECRUIT to add another worker (needs 4 oak planks in storage)";
            }

            if (livePopulation >= village.PopulationCap)
            {
                return "At housing cap — queue a Peasant House on BUILD tab, then recruit";
            }

            return "Settlement running — use BUILD and PEOPLE tabs to grow";
        }

        public static string GetQuickStartSteps(VillageEntity village, VillagerManager villagers, Vector3? playerPos = null)
        {
            VillageSettlementHealth.SyncPopulationRegistry(village, villagers);
            int livePopulation = VillageSettlementHealth.GetLivePopulation(village, villagers);

            if (livePopulation == 0)
            {
                if (playerPos.HasValue && VillageSettlementHealth.IsPlayerNearTownHeart(village, playerPos.Value))
                {
                    return "1) Click SUMMON SETTLERS below  2) Open PEOPLE tab  3) Assign LUMBER or BUILD";
                }

                return $"1) Walk to Town Heart ({village.AnchorX}, {village.AnchorZ})  2) Open PEOPLE tab  3) Assign LUMBER or BUILD";
            }

            return "1) PEOPLE tab — assign jobs  2) BUILD tab — queue farm or house  3) Shift+click trees to mark lumber";
        }
    }
}
