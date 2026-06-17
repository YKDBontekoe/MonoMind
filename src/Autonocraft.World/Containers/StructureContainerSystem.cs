using Autonocraft.Domain.Persistence;
using Autonocraft.Items;
using Autonocraft.World.Loot;
using Autonocraft.World.Structures;

namespace Autonocraft.World.Containers
{
    public sealed class WorldChest
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int Z { get; init; }
        public Inventory Inventory { get; } = new(StructureContainerSystem.SlotCount);
        public string LootTableId { get; init; } = string.Empty;
        public bool Opened { get; set; }
    }

    public sealed class StructureContainerSystem
    {
        public const int SlotCount = 18;

        private readonly Dictionary<(int x, int y, int z), WorldChest> _chests = new();
        private readonly object _lock = new();

        public void Clear()
        {
            lock (_lock)
            {
                _chests.Clear();
            }
        }

        public bool TryGet(int x, int y, int z, out WorldChest? chest)
        {
            lock (_lock)
            {
                return _chests.TryGetValue((x, y, z), out chest);
            }
        }

        public void RegisterChest(int x, int y, int z, string lootTableId, int rollSeed)
        {
            lock (_lock)
            {
                if (_chests.ContainsKey((x, y, z)))
                {
                    return;
                }

                var chest = new WorldChest
                {
                    X = x,
                    Y = y,
                    Z = z,
                    LootTableId = lootTableId
                };

                foreach (var stack in LootRoller.Roll(lootTableId, rollSeed))
                {
                    chest.Inventory.AddItem(stack);
                }

                _chests[(x, y, z)] = chest;
            }
        }

        public void ApplySaveData(IEnumerable<ContainerModification> mods)
        {
            lock (_lock)
            {
                foreach (var mod in mods)
                {
                    var chest = new WorldChest
                    {
                        X = mod.X,
                        Y = mod.Y,
                        Z = mod.Z,
                        Opened = true
                    };

                    for (int i = 0; i < Math.Min(mod.Slots.Count, SlotCount); i++)
                    {
                        chest.Inventory.SetSlot(i, ItemStackSaveCodec.Deserialize(mod.Slots[i]));
                    }

                    _chests[(mod.X, mod.Y, mod.Z)] = chest;
                }
            }
        }

        public List<ContainerModification> ExportModifications()
        {
            lock (_lock)
            {
                var result = new List<ContainerModification>(_chests.Count);
                foreach (var chest in _chests.Values)
                {
                    var slots = new List<InventorySlotSaveData>(SlotCount);
                    for (int i = 0; i < SlotCount; i++)
                    {
                        slots.Add(ItemStackSaveCodec.Serialize(chest.Inventory.GetSlot(i)));
                    }

                    result.Add(new ContainerModification
                    {
                        X = chest.X,
                        Y = chest.Y,
                        Z = chest.Z,
                        Slots = slots
                    });
                }

                return result;
            }
        }

        public static int RollSeed(int worldSeed, int x, int y, int z, string lootTableId)
        {
            int salt = unchecked(y * 92821 + lootTableId.GetHashCode(StringComparison.Ordinal));
            return StructurePlacementKeys.MixSeed(worldSeed, x, z, salt);
        }
    }
}
