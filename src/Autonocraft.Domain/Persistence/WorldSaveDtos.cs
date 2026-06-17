using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Autonocraft.Domain.Core;

namespace Autonocraft.Domain.Persistence
{
    public sealed class FluidModification
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public byte Level { get; set; }
        public bool IsSource { get; set; }
    }

    public sealed class WorldSaveData
    {
        public int Version { get; set; } = 7;
        public string SlotId { get; set; } = string.Empty;
        public string SlotName { get; set; } = string.Empty;
        public int Seed { get; set; } = 1337;
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
        public SpawnSaveData Spawn { get; set; } = new();
        public PlayerSaveData Player { get; set; } = new();
        public TimeSaveData Time { get; set; } = new();
        public List<BlockModification> Modifications { get; set; } = new();
        public List<FluidModification> FluidModifications { get; set; } = new();
        public List<string> UnlockedCraftingIds { get; set; } = new();
        public List<VillageSaveData> Villages { get; set; } = new();
        public List<VillagerSaveData> Villagers { get; set; } = new();
        public List<ClaimedAnchorSaveData> ClaimedAnchors { get; set; } = new();
        public bool VillageOnboardingComplete { get; set; }
    }

    public sealed class SpawnSaveData
    {
        public int X { get; set; } = 16;
        public int Z { get; set; } = 16;
    }

    public sealed class PlayerSaveData
    {
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float VelX { get; set; }
        public float VelY { get; set; }
        public float VelZ { get; set; }
        public float Yaw { get; set; } = -90f;
        public float Pitch { get; set; }
        public float Health { get; set; } = 20f;
        public float MaxHealth { get; set; } = 20f;
        public float Hunger { get; set; } = GameDefaults.MaxHunger;
        public float MaxHunger { get; set; } = GameDefaults.MaxHunger;
        public bool CreativeMode { get; set; }

        [JsonPropertyName("flyingMode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? FlyingModeLegacy { get; set; }
        public int SelectedSlot { get; set; }
        public List<InventorySlotSaveData> Hotbar { get; set; } = new();
        public int MiningLevel { get; set; } = 1;
        public float MiningXp { get; set; }
        public int WoodcuttingLevel { get; set; } = 1;
        public float WoodcuttingXp { get; set; }
        public int CombatLevel { get; set; } = 1;
        public float CombatXp { get; set; }
        public PlayerStatisticsSaveData Statistics { get; set; } = new();
    }

    public sealed class PlayerStatisticsSaveData
    {
        public double TotalPlayTimeSeconds { get; set; }
        public int SessionCount { get; set; }
        public float DistanceWalked { get; set; }
        public int StepsWalked { get; set; }
        public float MaxAltitude { get; set; }
        public float DistanceFlown { get; set; }
        public int AnimalsKilled { get; set; }
        public int SheepKilled { get; set; }
        public int PigKilled { get; set; }
        public int ChickenKilled { get; set; }
        public int CowKilled { get; set; }
        public int BearKilled { get; set; }
        public int FoxKilled { get; set; }
        public int DeerKilled { get; set; }
        public int WolfKilled { get; set; }
        public float DamageDealt { get; set; }
        public float DamageTaken { get; set; }
        public int PlayerDeaths { get; set; }
        public int BlocksBroken { get; set; }
        public int BlocksPlaced { get; set; }
        public int ToolsBroken { get; set; }
        public int FallDamageEvents { get; set; }
        public int TimesDrowned { get; set; }
        public int ItemsCrafted { get; set; }
        public int VillageTutorialStage { get; set; }
        public int EarlyGuideStage { get; set; }
    }

    public sealed class InventorySlotSaveData
    {
        public byte Kind { get; set; }
        public byte Block { get; set; }
        public ushort ToolId { get; set; }
        public int Count { get; set; }
        public int Durability { get; set; }
        public int MaxDurability { get; set; }
    }

    public sealed class TimeSaveData
    {
        public float TimeOfDay { get; set; } = 0.3f;
        public float TimeScale { get; set; } = 0.01f;
        public bool TimePaused { get; set; }
    }

    public sealed class BlockModification
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public byte Block { get; set; }
    }

    public sealed class SaveSlotInfo
    {
        public string SlotId { get; set; } = string.Empty;
        public string SlotName { get; set; } = string.Empty;
        public int Seed { get; set; }
        public DateTime SavedAt { get; set; }
        public bool IsCorrupt { get; set; }
    }

    public sealed class VillageSaveData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int AnchorX { get; set; }
        public int AnchorY { get; set; }
        public int AnchorZ { get; set; }
        public int Tier { get; set; }
        public float FoodStock { get; set; }
        public int ConsecutiveDaysWithoutFood { get; set; }
        public float Happiness { get; set; } = 1f;
        public float Radius { get; set; } = 32f;
        public int StorageSlots { get; set; } = 9;
        public int PopulationCap { get; set; } = 2;
        public int HousingCapacity { get; set; }
        public List<InventorySlotSaveData> Storage { get; set; } = new();
        public List<int> VillagerIds { get; set; } = new();
        public List<BuildingSaveData> Buildings { get; set; } = new();
        public List<BuildingSiteSaveData> BuildingSites { get; set; } = new();
        public List<WorkQueueBlockSaveData> WorkQueue { get; set; } = new();
        public List<VillageGoalSaveData> Goals { get; set; } = new();
        public List<OutputChestSaveData> OutputChests { get; set; } = new();
    }

    public sealed class OutputChestSaveData
    {
        public int Id { get; set; }
        public int BuildingId { get; set; }
        public int Kind { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public List<InventorySlotSaveData> Buffer { get; set; } = new();
    }

    public sealed class VillageGoalSaveData
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool Completed { get; set; }
        public int Kind { get; set; }
        public int? StockBlock { get; set; }
        public int TargetCount { get; set; }
        public string? BlueprintId { get; set; }
        public bool BuildQueued { get; set; }
    }

    public sealed class WorkQueueBlockSaveData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }

    public sealed class BuildingSaveData
    {
        public int Id { get; set; }
        public string BlueprintId { get; set; } = string.Empty;
        public int Kind { get; set; }
        public int AnchorX { get; set; }
        public int AnchorY { get; set; }
        public int AnchorZ { get; set; }
        public bool IsComplete { get; set; } = true;
    }

    public sealed class BuildingSiteSaveData
    {
        public int Id { get; set; }
        public int VillageId { get; set; }
        public string BlueprintId { get; set; } = string.Empty;
        public int AnchorX { get; set; }
        public int AnchorY { get; set; }
        public int AnchorZ { get; set; }
        public bool IsComplete { get; set; }
    }

    public sealed class ClaimedAnchorSaveData
    {
        public int X { get; set; }
        public int Z { get; set; }
    }

    public sealed class VillagerSaveData
    {
        public int Id { get; set; }
        public int VillageId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Role { get; set; }
        public int Job { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float Happiness { get; set; } = 1f;
        public float NeedFood { get; set; } = 1f;
        public float NeedRest { get; set; } = 1f;
        public float NeedSocial { get; set; } = 1f;
        public string Trait { get; set; } = string.Empty;
        public int MiningLevel { get; set; } = 1;
        public float MiningXp { get; set; }
        public int WoodcuttingLevel { get; set; } = 1;
        public float WoodcuttingXp { get; set; }
        public int FarmingLevel { get; set; } = 1;
        public float FarmingXp { get; set; }
        public int? BuildingSiteId { get; set; }
        public int? AssignedBuildingId { get; set; }
        public int? HaulSourceChestId { get; set; }
        public int? HaulSourceVillagerId { get; set; }
        public bool HaulIsDelivering { get; set; }
        public float? MarkedResourceX { get; set; }
        public float? MarkedResourceY { get; set; }
        public float? MarkedResourceZ { get; set; }
        public int? HomeBuildingId { get; set; }
        public float Yaw { get; set; }
        public int AiPhase { get; set; }
        public InventorySlotSaveData? EquippedTool { get; set; }
        public List<InventorySlotSaveData> Inventory { get; set; } = new();
    }
}
