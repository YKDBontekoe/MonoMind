using System;
using System.Collections.Generic;
using System.Numerics;

namespace Autonocraft.World
{
    public sealed class WorldSaveData
    {
        public int Version { get; set; } = 1;
        public string SlotId { get; set; } = string.Empty;
        public string SlotName { get; set; } = string.Empty;
        public int Seed { get; set; } = 1337;
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
        public SpawnSaveData Spawn { get; set; } = new();
        public PlayerSaveData Player { get; set; } = new();
        public TimeSaveData Time { get; set; } = new();
        public List<BlockModification> Modifications { get; set; } = new();
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
    }

    public sealed class InventorySlotSaveData
    {
        public byte Block { get; set; }
        public int Count { get; set; }
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
        public DateTime SavedAt { get; set; }
    }
}
