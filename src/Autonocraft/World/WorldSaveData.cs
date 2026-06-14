using System;
using System.Collections.Generic;
using System.Numerics;

namespace Autonocraft.World
{
    public sealed class WorldSaveData
    {
        public int Version { get; set; } = 6;
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
        public bool FlyingMode { get; set; }
        public int SelectedSlot { get; set; }
        public List<InventorySlotSaveData> Hotbar { get; set; } = new();
        public int MiningLevel { get; set; } = 1;
        public float MiningXp { get; set; }
        public int WoodcuttingLevel { get; set; } = 1;
        public float WoodcuttingXp { get; set; }
        public int CombatLevel { get; set; } = 1;
        public float CombatXp { get; set; }
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
        public float Happiness { get; set; } = 1f;
        public int StorageSlots { get; set; } = 9;
        public int PopulationCap { get; set; } = 2;
        public int HousingCapacity { get; set; }
        public List<InventorySlotSaveData> Storage { get; set; } = new();
        public List<int> VillagerIds { get; set; } = new();
        public List<BuildingSaveData> Buildings { get; set; } = new();
        public List<BuildingSiteSaveData> BuildingSites { get; set; } = new();
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
        public string Trait { get; set; } = string.Empty;
        public int? BuildingSiteId { get; set; }
        public List<InventorySlotSaveData> Inventory { get; set; } = new();
    }
}
