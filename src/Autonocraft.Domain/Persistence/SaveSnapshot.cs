using Autonocraft.Domain.Core;

namespace Autonocraft.Domain.Persistence
{
    /// <summary>
    /// Serializable snapshot passed to WorldSaveManager — decouples saves from the game shell.
    /// </summary>
    public sealed class SaveSnapshot
    {
        public string SlotId { get; init; } = string.Empty;
        public string SlotName { get; init; } = string.Empty;
        public int Seed { get; init; }
        public int SpawnX { get; init; } = GameDefaults.DefaultSpawnX;
        public int SpawnZ { get; init; } = GameDefaults.DefaultSpawnZ;
        public PlayerSaveData Player { get; init; } = new();
        public TimeSaveData Time { get; init; } = new();
        public List<BlockModification> Modifications { get; init; } = new();
        public List<FluidModification> FluidModifications { get; init; } = new();
        public List<string> UnlockedCraftingIds { get; init; } = new();
        public List<VillageSaveData> Villages { get; init; } = new();
        public List<VillagerSaveData> Villagers { get; init; } = new();
        public List<ClaimedAnchorSaveData> ClaimedAnchors { get; init; } = new();
    }
}
