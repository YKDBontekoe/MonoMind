using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.World
{
    public sealed class FluidModification
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public byte Level { get; set; }
        public bool IsSource { get; set; }
    }

    public sealed class FluidSystem
    {
        public const byte MaxLevel = 7;
        public const byte SourceFlag = 0x80;
        private const float TickInterval = 0.12f;

        private readonly Dictionary<(int x, int y, int z), byte> _cells = new();
        private readonly Queue<(int x, int y, int z)> _updateQueue = new();
        private readonly HashSet<(int x, int y, int z)> _queued = new();
        private float _tickAccumulator;

        public void Clear()
        {
            _cells.Clear();
            _updateQueue.Clear();
            _queued.Clear();
            _tickAccumulator = 0f;
        }

        public void ApplySaveData(IEnumerable<FluidModification> mods)
        {
            Clear();
            foreach (var mod in mods)
            {
                byte packed = mod.Level;
                if (mod.IsSource)
                {
                    packed |= SourceFlag;
                }

                _cells[(mod.X, mod.Y, mod.Z)] = packed;
            }
        }

        public List<FluidModification> ExportModifications()
        {
            var result = new List<FluidModification>(_cells.Count);
            foreach (var entry in _cells)
            {
                result.Add(new FluidModification
                {
                    X = entry.Key.x,
                    Y = entry.Key.y,
                    Z = entry.Key.z,
                    Level = (byte)(entry.Value & 0x7),
                    IsSource = (entry.Value & SourceFlag) != 0
                });
            }

            return result;
        }

        public bool HasFluid(int x, int y, int z) => _cells.ContainsKey((x, y, z));

        public bool IsSource(int x, int y, int z)
        {
            return _cells.TryGetValue((x, y, z), out byte packed) && (packed & SourceFlag) != 0;
        }

        /// <summary>
        /// World-gen water has no metadata and acts as an infinite spread source.
        /// Bucket-placed water is explicitly marked as a source.
        /// </summary>
        private bool ActsAsSource(int x, int y, int z) => IsSource(x, y, z) || !HasFluid(x, y, z);

        public void RegisterSource(int x, int y, int z)
        {
            _cells[(x, y, z)] = (byte)(MaxLevel | SourceFlag);
            Enqueue(x, y, z);
        }

        public void RemoveFluid(int x, int y, int z)
        {
            _cells.Remove((x, y, z));
            _queued.Remove((x, y, z));
        }

        public void PlaceSource(VoxelWorld world, int x, int y, int z, GraphicsDevice? context)
        {
            RegisterSource(x, y, z);
            world.SetBlockInternal(x, y, z, BlockType.Water, context, notifyFluids: false);
            EnqueueNeighbors(x, y, z);
        }

        public bool TryPickup(VoxelWorld world, int x, int y, int z, GraphicsDevice? context)
        {
            if (!world.GetBlock(x, y, z).IsWater())
            {
                return false;
            }

            RemoveFluid(x, y, z);
            world.SetBlockInternal(x, y, z, BlockType.Air, context, notifyFluids: false);
            EnqueueNeighbors(x, y, z);
            return true;
        }

        /// <summary>
        /// Wake adjacent water when terrain changes so static oceans/rivers fill dug gaps.
        /// </summary>
        public void NotifyBlockChanged(int x, int y, int z, BlockType previous, BlockType current)
        {
            if (previous.IsWater() || current.IsWater() || previous == BlockType.Air || current == BlockType.Air)
            {
                EnqueueNeighbors(x, y, z);
            }
        }

        public void Update(VoxelWorld world, float deltaTime, GraphicsDevice? context, int maxUpdatesPerTick = 96)
        {
            _tickAccumulator += deltaTime;
            if (_tickAccumulator < TickInterval)
            {
                return;
            }

            _tickAccumulator = 0f;
            int processed = 0;
            while (_updateQueue.Count > 0 && processed < maxUpdatesPerTick)
            {
                var (x, y, z) = _updateQueue.Dequeue();
                _queued.Remove((x, y, z));
                processed++;
                ProcessCell(world, x, y, z, context);
            }
        }

        private void ProcessCell(VoxelWorld world, int x, int y, int z, GraphicsDevice? context)
        {
            if (!world.GetBlock(x, y, z).IsWater())
            {
                RemoveFluid(x, y, z);
                return;
            }

            bool source = ActsAsSource(x, y, z);
            bool placedFlow = HasFluid(x, y, z) && !IsSource(x, y, z);

            if (TryFlowDown(world, x, y, z, context, source, placedFlow))
            {
                return;
            }

            if (source || GetLevel(x, y, z) > 1)
            {
                TryFlowHorizontal(world, x, y, z, context);
                TryFlowDownDiagonal(world, x, y, z, context);
            }

            if (placedFlow)
            {
                TryRemoveUnsupported(world, x, y, z, context);
            }
        }

        private bool TryFlowDown(
            VoxelWorld world,
            int x,
            int y,
            int z,
            GraphicsDevice? context,
            bool source,
            bool placedFlow)
        {
            int belowY = y - 1;
            if (belowY < 0)
            {
                return false;
            }

            if (!CanReceive(world, x, belowY, z))
            {
                return false;
            }

            SetFlowingCell(world, x, belowY, z, MaxLevel, false, context);
            Enqueue(x, belowY, z);

            if (placedFlow)
            {
                world.SetBlockInternal(x, y, z, BlockType.Air, context, notifyFluids: false);
                RemoveFluid(x, y, z);
                EnqueueNeighbors(x, y, z);
            }

            return true;
        }

        private void TryFlowDownDiagonal(VoxelWorld world, int x, int y, int z, GraphicsDevice? context)
        {
            ReadOnlySpan<(int dx, int dz)> dirs = stackalloc (int, int)[]
            {
                (1, 0), (-1, 0), (0, 1), (0, -1)
            };

            foreach (var (dx, dz) in dirs)
            {
                int nx = x + dx;
                int nz = z + dz;
                int ny = y - 1;
                if (ny < 0 || !CanReceive(world, nx, ny, nz))
                {
                    continue;
                }

                if (!CanReceive(world, nx, y, nz))
                {
                    continue;
                }

                SetFlowingCell(world, nx, ny, nz, MaxLevel, false, context);
                Enqueue(nx, ny, nz);
            }
        }

        private void TryFlowHorizontal(VoxelWorld world, int x, int y, int z, GraphicsDevice? context)
        {
            ReadOnlySpan<(int dx, int dz)> dirs = stackalloc (int, int)[]
            {
                (1, 0), (-1, 0), (0, 1), (0, -1)
            };

            byte nextLevel = (byte)Math.Max(1, GetLevel(x, y, z) - 1);
            foreach (var (dx, dz) in dirs)
            {
                int nx = x + dx;
                int nz = z + dz;
                if (!CanReceive(world, nx, y, nz))
                {
                    continue;
                }

                if (!HasSupport(world, nx, y, nz))
                {
                    continue;
                }

                SetFlowingCell(world, nx, y, nz, nextLevel, false, context);
                Enqueue(nx, y, nz);
            }
        }

        private void TryRemoveUnsupported(VoxelWorld world, int x, int y, int z, GraphicsDevice? context)
        {
            if (HasSupport(world, x, y, z))
            {
                return;
            }

            world.SetBlockInternal(x, y, z, BlockType.Air, context, notifyFluids: false);
            RemoveFluid(x, y, z);
            EnqueueNeighbors(x, y, z);
        }

        private static bool HasSupport(VoxelWorld world, int x, int y, int z)
        {
            if (y <= 0)
            {
                return true;
            }

            var below = world.GetBlock(x, y - 1, z);
            return below.IsCollidable() || below.IsWater();
        }

        private static bool CanReceive(VoxelWorld world, int x, int y, int z)
        {
            if (!world.IsInBounds(x, y, z))
            {
                return false;
            }

            return world.GetBlock(x, y, z) == BlockType.Air;
        }

        private void SetFlowingCell(
            VoxelWorld world,
            int x,
            int y,
            int z,
            byte level,
            bool source,
            GraphicsDevice? context)
        {
            byte packed = (byte)(Math.Clamp((int)level, 1, (int)MaxLevel) | (source ? SourceFlag : 0));
            _cells[(x, y, z)] = packed;
            world.SetBlockInternal(x, y, z, BlockType.Water, context, notifyFluids: false);
        }

        private byte GetLevel(int x, int y, int z)
        {
            if (!_cells.TryGetValue((x, y, z), out byte packed))
            {
                return MaxLevel;
            }

            return (byte)(packed & 0x7);
        }

        private void Enqueue(int x, int y, int z)
        {
            var key = (x, y, z);
            if (_queued.Add(key))
            {
                _updateQueue.Enqueue(key);
            }
        }

        private void EnqueueNeighbors(int x, int y, int z)
        {
            Enqueue(x, y, z);
            Enqueue(x, y + 1, z);
            Enqueue(x, y - 1, z);
            Enqueue(x + 1, y, z);
            Enqueue(x - 1, y, z);
            Enqueue(x, y, z + 1);
            Enqueue(x, y, z - 1);
        }
    }
}
