using System;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Items;

namespace Autonocraft.Village
{
    public sealed class OutputChest
    {
        private static int _nextId = 1;

        public int Id { get; }
        public int BuildingId { get; }
        public BuildingKind BuildingKind { get; }
        public Vector3 Position { get; }
        public Inventory Buffer { get; } = new Inventory(9);

        public OutputChest(int buildingId, BuildingKind kind, Vector3 position, int? explicitId = null)
        {
            Id = explicitId ?? _nextId++;
            if (explicitId.HasValue && explicitId.Value >= _nextId)
            {
                _nextId = explicitId.Value + 1;
            }

            BuildingId = buildingId;
            BuildingKind = kind;
            Position = position;
        }

        public static void ResetIdCounter(int nextId) => _nextId = Math.Max(1, nextId);

        public bool HasItems
        {
            get
            {
                for (int i = 0; i < Buffer.SlotCount; i++)
                {
                    if (!Buffer.GetSlot(i).IsEmpty)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int ItemCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Buffer.SlotCount; i++)
                {
                    total += Buffer.GetSlot(i).Count;
                }

                return total;
            }
        }
    }
}
