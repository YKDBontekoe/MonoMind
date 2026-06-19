using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.Village
{
    public static class VillageGuidance
    {
        public static string GetNextBestAction(VillageEntity village, VillagerManager villagers, Vector3? playerPos = null) =>
            SettlementGuidance.Compute(village, villagers, playerPos).Detail;

        public static string GetQuickStartSteps(VillageEntity village, VillagerManager villagers, Vector3? playerPos = null)
        {
            VillageSettlementHealth.SyncPopulationRegistry(village, villagers);
            int livePopulation = VillageSettlementHealth.GetLivePopulation(village, villagers);

            if (livePopulation == 0)
            {
                if (playerPos.HasValue && VillageSettlementHealth.IsPlayerNearTownHeart(village, playerPos.Value))
                {
                    return "1) Click SUMMON SETTLERS below  2) Open People tab  3) Assign Lumber or Build";
                }

                return $"1) Walk to Town Heart ({village.AnchorX}, {village.AnchorZ})  2) Open People tab  3) Assign Lumber or Build";
            }

            return "1) People tab — assign jobs  2) Build tab — queue farm or house  3) Shift+click trees to mark lumber";
        }
    }
}
