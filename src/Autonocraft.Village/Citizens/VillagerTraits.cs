using System;

namespace Autonocraft.Village
{
    public static class VillagerTraits
    {
        public const string Strong = "strong";
        public const string GreenThumb = "green_thumb";

        public const float StrongMineSpeedBonus = 1.25f;
        public const float GreenThumbFarmYieldBonus = 1.35f;

        public static bool Matches(string trait, string id) =>
            string.Equals(trait, id, StringComparison.OrdinalIgnoreCase);

        public static float GetMineSpeedMultiplier(string trait) =>
            Matches(trait, Strong) ? StrongMineSpeedBonus : 1f;

        public static float GetFarmYieldMultiplier(string trait) =>
            Matches(trait, GreenThumb) ? GreenThumbFarmYieldBonus : 1f;
    }
}
