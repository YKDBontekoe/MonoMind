using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Village
{
    public sealed class BuildingSite
    {
        public int Id { get; }
        public int VillageId { get; }
        public string BlueprintId { get; }
        public int AnchorX { get; }
        public int AnchorY { get; }
        public int AnchorZ { get; }
        public bool IsComplete { get; set; }

        private readonly StructureBlock[] _blocks;
        private readonly List<StructureBlock> _pending = new();

        private static int _nextId = 1;

        public BuildingSite(int villageId, BuildingBlueprint blueprint, int anchorX, int anchorY, int anchorZ, int? explicitId = null)
        {
            Id = explicitId ?? _nextId++;
            if (explicitId.HasValue && explicitId.Value >= _nextId)
            {
                _nextId = explicitId.Value + 1;
            }

            VillageId = villageId;
            BlueprintId = blueprint.Id;
            AnchorX = anchorX;
            AnchorY = anchorY;
            AnchorZ = anchorZ;
            _blocks = blueprint.Template.Blocks;
            RebuildPending(null);
        }

        public static void ResetIdCounter(int nextId) => _nextId = Math.Max(1, nextId);

        public static BuildingSite Restore(BuildingSiteSaveData entry, BuildingBlueprint blueprint)
        {
            var site = new BuildingSite(entry.VillageId, blueprint, entry.AnchorX, entry.AnchorY, entry.AnchorZ, entry.Id);
            site.IsComplete = entry.IsComplete;
            return site;
        }

        public float CompletionRatio
        {
            get
            {
                if (_blocks.Length == 0)
                {
                    return 1f;
                }

                return 1f - (_pending.Count / (float)_blocks.Length);
            }
        }

        public IReadOnlyList<StructureBlock> PendingBlocks => _pending;
        public int RemainingCount => _pending.Count;

        public bool TryGetNextBlock(out StructureBlock block)
        {
            if (_pending.Count == 0)
            {
                block = default;
                IsComplete = true;
                return false;
            }

            block = _pending[0];
            return true;
        }

        public void SyncWithWorld(VoxelWorld world)
        {
            RebuildPending(world);
            IsComplete = _pending.Count == 0;
        }

        public bool TryPlaceNextBlock(
            VoxelWorld world,
            VillageStorage storage,
            float entityWidth,
            float entityHeight,
            Vector3 builderPos,
            bool creative = false,
            bool checkBuilderCollision = true)
        {
            if (!TryGetNextBlock(out var next))
            {
                return false;
            }

            int wx = AnchorX + next.Dx;
            int wy = AnchorY + next.Dy;
            int wz = AnchorZ + next.Dz;

            if (world.GetBlock(wx, wy, wz) == next.Type)
            {
                _pending.RemoveAt(0);
                IsComplete = _pending.Count == 0;
                return true;
            }

            if (creative && !checkBuilderCollision)
            {
                world.SetBlock(wx, wy, wz, next.Type);
                _pending.RemoveAt(0);
                IsComplete = _pending.Count == 0;
                return true;
            }

            if (!creative && !storage.TryConsumeBlock(next.Type, 1))
            {
                return false;
            }

            if (!BlockActionService.TryPlaceBlock(
                    world,
                    wx,
                    wy,
                    wz,
                    next.Type,
                    checkBuilderCollision ? entityWidth : 0f,
                    checkBuilderCollision ? entityHeight : 0f,
                    builderPos,
                    inventory: null,
                    consumeFromInventory: false))
            {
                if (!creative)
                {
                    storage.AddItem(Items.ItemStack.CreateBlock(next.Type, 1));
                }

                return false;
            }

            _pending.RemoveAt(0);
            IsComplete = _pending.Count == 0;
            return true;
        }

        private void RebuildPending(VoxelWorld? world)
        {
            _pending.Clear();
            foreach (var block in _blocks)
            {
                if (world != null)
                {
                    int wx = AnchorX + block.Dx;
                    int wy = AnchorY + block.Dy;
                    int wz = AnchorZ + block.Dz;
                    var current = world.GetBlock(wx, wy, wz);
                    if (current == block.Type)
                    {
                        continue;
                    }
                }

                _pending.Add(block);
            }
        }
    }
}
