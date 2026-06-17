using Autonocraft.Village;

namespace Autonocraft.UI.Village
{
    public static class VillagerActivityText
    {
        public static string Describe(Entities.Villager villager) =>
            Village.VillagerActivityText.Describe(villager);

        public static string DescribeProgress(Entities.Villager villager) =>
            Village.VillagerActivityText.DescribeProgress(villager);

        public static bool NeedsAttention(Entities.Villager villager) =>
            Village.VillagerActivityText.NeedsAttention(villager);
    }
}
