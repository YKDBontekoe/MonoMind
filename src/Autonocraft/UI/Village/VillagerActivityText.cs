using Autonocraft.Domain.Village;
using Autonocraft.Entities;

namespace Autonocraft.UI.Village
{
    public static class VillagerActivityText
    {
        public static string Describe(Villager villager)
        {
            if (villager.CurrentJob == JobType.Idle)
            {
                return "Idle — needs a job";
            }

            if (villager.CurrentJob == JobType.Sleep)
            {
                return "Sleeping";
            }

            return villager.CurrentJob switch
            {
                JobType.Lumber or JobType.Gather => "Chopping wood",
                JobType.Mine => "Mining stone",
                JobType.Farm => "Working the farm",
                JobType.Build => "Building",
                JobType.Haul => villager.HaulIsDelivering ? "Delivering goods" : "Picking up goods",
                JobType.Craft => villager.Role == VillagerRole.Smith ? "Smithing" : "Crafting",
                JobType.Hunt => "Hunting",
                JobType.Mason => "Cutting stone",
                JobType.Cook => "Cooking meals",
                _ => villager.CurrentJob.ToString().ToUpperInvariant()
            };
        }

        public static string DescribeProgress(Villager villager)
        {
            if (villager.CurrentJob is JobType.Lumber or JobType.Mine or JobType.Gather or JobType.Mason)
            {
                if (villager.BreakProgress > 0f)
                {
                    return $"{(int)(villager.BreakProgress * 100f)}%";
                }
            }

            if (villager.CurrentJob == JobType.Build && villager.WorkTimer > 0f)
            {
                return "Placing blocks";
            }

            return string.Empty;
        }

        public static bool NeedsAttention(Villager villager) =>
            villager.CurrentJob == JobType.Idle || villager.Happiness < 0.4f;
    }
}
