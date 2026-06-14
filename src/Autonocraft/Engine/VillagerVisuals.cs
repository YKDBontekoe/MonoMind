using Autonocraft.Domain.Village;
using Microsoft.Xna.Framework;

namespace Autonocraft.Engine
{
    public static class VillagerVisuals
    {
        public static Color GetRoleColor(VillagerRole role) => role switch
        {
            VillagerRole.Lumberjack => new Color(0.55f, 0.38f, 0.22f),
            VillagerRole.Builder => new Color(0.95f, 0.55f, 0.18f),
            VillagerRole.Farmer => new Color(0.35f, 0.72f, 0.38f),
            VillagerRole.Smith => new Color(0.45f, 0.48f, 0.52f),
            VillagerRole.Miner => new Color(0.58f, 0.58f, 0.62f),
            VillagerRole.Hauler => new Color(0.68f, 0.52f, 0.34f),
            _ => new Color(0.72f, 0.68f, 0.58f)
        };

        public static Color GetJobIndicatorColor(JobType job) => job switch
        {
            JobType.Gather or JobType.Lumber => new Color(0.35f, 0.78f, 0.32f, 0.92f),
            JobType.Mine => new Color(0.5f, 0.52f, 0.58f, 0.92f),
            JobType.Farm => new Color(0.55f, 0.82f, 0.28f, 0.92f),
            JobType.Build => new Color(0.28f, 0.62f, 0.95f, 0.92f),
            JobType.Haul => new Color(0.62f, 0.42f, 0.24f, 0.92f),
            JobType.Craft => new Color(0.92f, 0.78f, 0.22f, 0.92f),
            JobType.Sleep => new Color(0.35f, 0.45f, 0.72f, 0.75f),
            _ => Color.Transparent
        };

        public static bool ShouldDrawJobIndicator(JobType job) =>
            job is JobType.Gather or JobType.Lumber or JobType.Mine or JobType.Farm
                or JobType.Build or JobType.Haul or JobType.Craft or JobType.Sleep;
    }
}
