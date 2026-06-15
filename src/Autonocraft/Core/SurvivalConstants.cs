namespace Autonocraft.Core
{
    public static class SurvivalConstants
    {
        public const float MaxHunger = 20f;
        public const float LeanStartHunger = 14f;
        public const float RespawnHunger = 8f;
        public const float IdleHungerDepletionPerSecond = 1f / 90f;
        public const float ActiveHungerMultiplier = 2f;
        public const float StarvationIntervalSeconds = 4f;
        public const float StarvationDamage = 1f;
        public const float BerryDropChance = 0.08f;
        public const int DeathLostSlotCount = 3;
        public const float DeathToolDurabilityLossFraction = 0.25f;

        public const float WolfSpawnIntervalSeconds = 45f;
        public const int MaxHostileMobsGlobal = 6;
        public const float WolfSpawnMinDistance = 16f;
        public const float WolfSpawnMaxDistance = 32f;
        public const float SafeZoneBenchRadius = 8f;
        public const float WolfChaseRange = 24f;
        public const float WolfMeleeRange = 1.4f;
        public const float WolfMeleeDamage = 3f;
        public const float WolfAttackCooldownSeconds = 1f;
    }
}
