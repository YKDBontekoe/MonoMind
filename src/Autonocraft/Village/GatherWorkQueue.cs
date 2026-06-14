using System;
using System.Collections.Generic;
using Autonocraft.Domain.Village;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public sealed class GatherWorkQueue
    {
        public const int MaxZoneExtent = 8;

        private readonly List<(int X, int Y, int Z)> _pending = new();

        public int Count => _pending.Count;
        public IReadOnlyList<(int X, int Y, int Z)> Pending => _pending;

        public bool Enqueue(int x, int y, int z)
        {
            foreach (var block in _pending)
            {
                if (block.X == x && block.Y == y && block.Z == z)
                {
                    return false;
                }
            }

            _pending.Add((x, y, z));
            return true;
        }

        public int EnqueueZone(VoxelWorld world, int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        {
            int added = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        var block = world.GetBlock(x, y, z);
                        if (!GatherBlockClassifier.IsGatherable(block))
                        {
                            continue;
                        }

                        if (Enqueue(x, y, z))
                        {
                            added++;
                        }
                    }
                }
            }

            return added;
        }

        public bool TryGetNextForRole(VillagerRole role, VoxelWorld world, out int x, out int y, out int z)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var block = _pending[i];
                var blockType = world.GetBlock(block.X, block.Y, block.Z);
                if (blockType == BlockType.Air || !blockType.IsCollidable())
                {
                    continue;
                }

                if (!GatherBlockClassifier.CanGather(role, blockType))
                {
                    continue;
                }

                x = block.X;
                y = block.Y;
                z = block.Z;
                return true;
            }

            x = y = z = 0;
            return false;
        }

        public bool TryGetNextAny(VoxelWorld world, out int x, out int y, out int z)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var block = _pending[i];
                var blockType = world.GetBlock(block.X, block.Y, block.Z);
                if (blockType == BlockType.Air || !GatherBlockClassifier.IsGatherable(blockType))
                {
                    continue;
                }

                x = block.X;
                y = block.Y;
                z = block.Z;
                return true;
            }

            x = y = z = 0;
            return false;
        }

        public void Complete(int x, int y, int z)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var block = _pending[i];
                if (block.X == x && block.Y == y && block.Z == z)
                {
                    _pending.RemoveAt(i);
                    return;
                }
            }
        }

        public void SyncWithWorld(VoxelWorld world)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var block = _pending[i];
                var blockType = world.GetBlock(block.X, block.Y, block.Z);
                if (blockType == BlockType.Air || !GatherBlockClassifier.IsGatherable(blockType))
                {
                    _pending.RemoveAt(i);
                }
            }
        }

        public void Restore(IEnumerable<WorkQueueBlockSaveData>? entries)
        {
            _pending.Clear();
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                Enqueue(entry.X, entry.Y, entry.Z);
            }
        }

        public List<WorkQueueBlockSaveData> Export()
        {
            var result = new List<WorkQueueBlockSaveData>(_pending.Count);
            foreach (var block in _pending)
            {
                result.Add(new WorkQueueBlockSaveData
                {
                    X = block.X,
                    Y = block.Y,
                    Z = block.Z
                });
            }

            return result;
        }

        public static (int minX, int minY, int minZ, int maxX, int maxY, int maxZ) NormalizeBounds(
            int ax,
            int ay,
            int az,
            int bx,
            int by,
            int bz,
            int maxExtent = MaxZoneExtent)
        {
            int minX = Math.Min(ax, bx);
            int minY = Math.Min(ay, by);
            int minZ = Math.Min(az, bz);
            int maxX = Math.Max(ax, bx);
            int maxY = Math.Max(ay, by);
            int maxZ = Math.Max(az, bz);

            if (maxX - minX + 1 > maxExtent)
            {
                maxX = minX + maxExtent - 1;
            }

            if (maxY - minY + 1 > maxExtent)
            {
                maxY = minY + maxExtent - 1;
            }

            if (maxZ - minZ + 1 > maxExtent)
            {
                maxZ = minZ + maxExtent - 1;
            }

            return (minX, minY, minZ, maxX, maxY, maxZ);
        }
    }
}
