namespace Autonocraft.Core
{
    public static class SurvivalConstants
    {
        public const float MaxHunger = 20f;
        public const float HungerDrainPerSecond = 0.012f;
        public const float StarvationDamageInterval = 4f;
        public const float StarvationDamage = 1f;
        public const float LowHungerFraction = 0.25f;
        public const float LowHungerSpeedMultiplier = 0.7f;
        public const float HungerWarningFraction = 0.30f;
        public const float RationHungerThresholdFraction = 0.80f;
        public const float RationFoodCost = 1f;
        public const float RespawnHungerFraction = 0.60f;

        public const int CookedMeatRestore = 6;
        public const int RawMeatRestore = 2;
        public const int BreadRestore = 5;

        public const int MaxNightWolves = 2;
        public const float NightWolfSpawnRadius = 32f;
        public const float SpawnWarmupSeconds = 15f;
    }
}
