using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.World
{
    // Block get/set, modifications, and public API shell.
    public partial class VoxelWorld : IDisposable
    {
        public const int DefaultTerrainChunksPerFrame = 6;
        public const int DefaultMeshChunksPerFrame = 6;
        public const int LoadingMeshChunksPerFrame = 10;

        public static int GetLoadingMeshChunksPerFrame(int renderDistance) =>
            renderDistance >= 40 ? 48 :
            renderDistance >= 32 ? 40 :
            renderDistance >= 24 ? 32 :
            renderDistance >= 18 ? 28 :
            renderDistance >= 16 ? 24 :
            renderDistance >= 12 ? 20 :
            renderDistance >= 8 ? 16 :
            LoadingMeshChunksPerFrame;

        public static int GetRuntimeTerrainChunksPerFrame(int renderDistance) =>
            renderDistance >= 40 ? 10 :
            renderDistance >= 32 ? 9 :
            renderDistance >= 24 ? 7 :
            renderDistance >= 18 ? 6 :
            renderDistance >= 12 ? 5 :
            DefaultTerrainChunksPerFrame;

        public static int GetRuntimeMeshChunksPerFrame(int renderDistance) =>
            renderDistance >= 40 ? 10 :
            renderDistance >= 32 ? 9 :
            renderDistance >= 24 ? 7 :
            renderDistance >= 18 ? 6 :
            renderDistance >= 12 ? 5 :
            DefaultMeshChunksPerFrame;
        public const int LoadingTerrainCompletionsPerFrame = 12;
        public const int MaxTerrainCompletionsPerFrame = 4;
        public const int MaxMeshCandidatesPerFrame = 24;
        public const int LoadingMaxMeshCandidatesPerFrame = 48;
        public const float MeshBuildBudgetMs = 14f;
        public const float LoadingMeshBuildBudgetMs = 22f;
        public const float FastTravelMeshBuildBudgetMs = 10f;
        public const int MissingMeshScanChunksPerFrame = 16;
        public const int MaxMeshBuildsInFlight = 6;
        public const int MaxMeshDispatchesPerFrame = 4;
        public const int MaxMeshUploadsPerFrame = 6;
        private const float MeshUploadBudgetMs = 9f;
        public const int FastTravelPrefetchIntervalFrames = 4;
        public const int NeighborRemeshDistanceFastTravel = 2;
        public const int NeighborRemeshMaxChunkDistance = 5;
        public const int MaxSyncMeshBuildsPerFrame = 1;
        public const int SyncMeshMaxChunkDistance = 1;
        public const int MeshPressurePendingThreshold = 36;
        public const int MaxTerrainCompletionsUnderMeshPressure = 2;
        public const int MaxDistantShellDispatchesPerFrame = 2;
        public const float GameplayChunkProcessBudgetMs = 18f;
        private const long GameplayMeshMemoryPressureBytes = 1_800_000_000L;

        /// <summary>0..1 mesh promotion rate after spawn (1 = normal). Lower values defer full-detail builds.</summary>
        public float GameplayMeshThrottle { get; set; } = 1f;

        private readonly Dictionary<(int x, int z), Chunk> _chunks = new Dictionary<(int x, int z), Chunk>();
        private readonly Dictionary<(int x, int y, int z), byte> _modifications = new Dictionary<(int x, int y, int z), byte>();
        private readonly Dictionary<(int cx, int cz), List<(int x, int y, int z)>> _modificationsByChunk = new();
        private readonly List<Chunk> _activeChunkList = new();
        private readonly FluidSystem _fluids = new();
        private readonly StructureContainerSystem _containers = new();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly WorldGenerator _generator;

        [ThreadStatic]
        private static Chunk? _lastChunk;
        [ThreadStatic]
        private static int _lastCx;
        [ThreadStatic]
        private static int _lastCz;
        [ThreadStatic]
        private static VoxelWorld? _lastWorld;

        public bool InitialLoading => _initialLoading;

        public int Seed { get; }
        public WorldGenParams GenerationParams { get; }
        public FluidSystem Fluids => _fluids;
        public StructureContainerSystem Containers => _containers;
        public int ActiveChunkCount => _activeChunkList.Count;
        public int StreamRenderDistance => _streamRenderDistance;
        public IReadOnlyList<Chunk> ActiveChunks => _activeChunkList;
        public int PendingMeshCount
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _meshScheduler.PendingMeshCount(_pendingMesh);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        internal int InFlightGenerationCount
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _terrainScheduler.InFlightCount;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        internal bool IsInitialLoadComplete()
        {
            _lock.EnterReadLock();
            try
            {
                return _initialLoadQueue == null
                    && _terrainScheduler.PendingTerrainCount == 0
                    && _pendingMesh.Count == 0
                    && _terrainScheduler.InFlightCount == 0;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        internal void InjectFaultedChunkJobForTests(int cx, int cz)
        {
            _lock.EnterWriteLock();
            try
            {
                _terrainScheduler.InjectFaultedJobForTests(cx, cz);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public event Action<IReadOnlyList<(int cx, int cz)>>? ChunksLoaded;
        public event Action<IReadOnlyList<(int cx, int cz)>>? ChunksUnloaded;

        public VoxelWorld(int seed = WorldConstants.DefaultSeed, WorldGenParams? parameters = null)
        {
            Seed = seed;
            GenerationParams = parameters ?? WorldGenParams.ForType(WorldType.Default);
            _generator = new WorldGenerator(seed, GenerationParams);
        }

        public bool IsInBounds(int x, int y, int z)
        {
            return y >= 0 && y < Chunk.Height;
        }

        public BiomeSample SampleBiome(int wx, int wz) => _generator.SampleBiome(wx, wz);

        public static void GetChunkCoords(int x, int z, out int cx, out int cz, out int lx, out int lz)
        {
            cx = x >> 4;
            cz = z >> 4;
            lx = x & 15;
            lz = z & 15;
        }

        public BlockType GetBlock(int x, int y, int z)
        {
            if (y < 0 || y >= Chunk.Height) return BlockType.Air;

            PerfCounters.GetBlockCalls++;
            GetChunkCoords(x, z, out int cx, out int cz, out int lx, out int lz);

            var lastChunk = _lastChunk;
            if (lastChunk != null && _lastCx == cx && _lastCz == cz && _lastWorld == this && !lastChunk.IsUnloaded)
            {
                return lastChunk.GetBlock(lx, y, lz);
            }

            _lock.EnterReadLock();
            try
            {
                if (_chunks.TryGetValue((cx, cz), out var chunk))
                {
                    _lastChunk = chunk;
                    _lastCx = cx;
                    _lastCz = cz;
                    _lastWorld = this;
                    return chunk.GetBlock(lx, y, lz);
                }
                return BlockType.Air;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        internal void SetBlockDuringGeneration(int x, int y, int z, BlockType type)
        {
            if (y < 0 || y >= Chunk.Height) return;

            GetChunkCoords(x, z, out int cx, out int cz, out int lx, out int lz);
            if (_chunks.TryGetValue((cx, cz), out var chunk))
            {
                chunk.SetBlock(lx, y, lz, type);
            }
        }

        public void SetBlock(int x, int y, int z, BlockType type, GraphicsDevice? context = null)
        {
            SetBlockInternal(x, y, z, type, context, notifyFluids: true);
        }

        internal void SetBlockInternal(int x, int y, int z, BlockType type, GraphicsDevice? context, bool notifyFluids)
        {
            if (y < 0 || y >= Chunk.Height) return;

            _lock.EnterWriteLock();
            try
            {
                BlockType previous = BlockType.Air;
                GetChunkCoords(x, z, out int cx, out int cz, out int lx, out int lz);
                if (_chunks.TryGetValue((cx, cz), out var existingChunk))
                {
                    previous = existingChunk.GetBlock(lx, y, lz);
                }

                _modifications[(x, y, z)] = (byte)type;
                TrackModificationByChunk(x, y, z);

                if (_chunks.TryGetValue((cx, cz), out var chunk))
                {
                    chunk.SetBlock(lx, y, lz, type);
                    EnqueueMeshForBlockEdit(cx, cz, lx, lz);
                }

                if (notifyFluids)
                {
                    _fluids.NotifyBlockChanged(x, y, z, previous, type);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void ApplySaveData(WorldSaveData data)
        {
            _lock.EnterWriteLock();
            try
            {
                _modifications.Clear();
                _modificationsByChunk.Clear();
                foreach (var mod in data.Modifications)
                {
                    _modifications[(mod.X, mod.Y, mod.Z)] = mod.Block;
                    TrackModificationByChunk(mod.X, mod.Y, mod.Z);
                }

                _fluids.ApplySaveData(data.FluidModifications ?? new List<FluidModification>());
                _containers.ApplySaveData(data.ContainerModifications ?? new List<ContainerModification>());
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public List<BlockModification> ExportModifications()
        {
            _lock.EnterReadLock();
            try
            {
                var result = new List<BlockModification>(_modifications.Count);
                foreach (var entry in _modifications)
                {
                    result.Add(new BlockModification
                    {
                        X = entry.Key.x,
                        Y = entry.Key.y,
                        Z = entry.Key.z,
                        Block = entry.Value
                    });
                }

                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void ApplyModificationsToChunk(Chunk chunk)
        {
            if (!_modificationsByChunk.TryGetValue((chunk.ChunkX, chunk.ChunkZ), out var mods))
            {
                return;
            }

            foreach (var (wx, wy, wz) in mods)
            {
                if (wy < 0 || wy >= Chunk.Height) continue;

                GetChunkCoords(wx, wz, out int cx, out int cz, out int lx, out int lz);
                if (cx != chunk.ChunkX || cz != chunk.ChunkZ) continue;

                if (_modifications.TryGetValue((wx, wy, wz), out byte blockValue))
                {
                    chunk.SetBlock(lx, wy, lz, (BlockType)blockValue);
                }
            }
        }

        private void TrackModificationByChunk(int x, int y, int z)
        {
            GetChunkCoords(x, z, out int cx, out int cz, out _, out _);
            var key = (cx, cz);
            if (!_modificationsByChunk.TryGetValue(key, out var list))
            {
                list = new List<(int x, int y, int z)>();
                _modificationsByChunk[key] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].x == x && list[i].y == y && list[i].z == z)
                {
                    return;
                }
            }

            list.Add((x, y, z));
        }

        private void RegisterChunk(Chunk chunk)
        {
            _activeChunkList.Add(chunk);
        }

        private void UnregisterChunk(Chunk chunk)
        {
            _activeChunkList.Remove(chunk);
            if (_lastChunk == chunk)
            {
                _lastChunk = null;
                _lastWorld = null;
            }
        }

        private bool ShouldAcceptChunkAt(int cx, int cz)
        {
            int distanceX = Math.Abs(cx - _streamAgentCx);
            int distanceZ = Math.Abs(cz - _streamAgentCz);
            return distanceX <= _streamRenderDistance + 1 && distanceZ <= _streamRenderDistance + 1;
        }
        private void FlushChunkChests(Chunk chunk)
        {
            if (chunk.PendingChests.Count == 0)
            {
                return;
            }

            int baseX = chunk.ChunkX * Chunk.Width;
            int baseZ = chunk.ChunkZ * Chunk.Depth;
            foreach (var pending in chunk.PendingChests)
            {
                if (chunk.GetBlock(pending.LocalX, pending.LocalY, pending.LocalZ) != BlockType.Chest)
                {
                    continue;
                }

                Containers.RegisterChest(
                    baseX + pending.LocalX,
                    pending.LocalY,
                    baseZ + pending.LocalZ,
                    pending.LootTableId,
                    pending.RollSeed);
            }

            chunk.PendingChests.Clear();
        }

        private void StartChunkGeneration(int cx, int cz)
        {
            var coord = (cx, cz);
            if (_chunks.ContainsKey(coord) || _terrainScheduler.ContainsInFlight(coord))
            {
                return;
            }

            _terrainScheduler.StartChunkGeneration(cx, cz, _generator);
        }

        private bool TryCompleteChunkGeneration(int cx, int cz, out Chunk? chunk)
        {
            chunk = null;
            if (!_terrainScheduler.TryCompleteChunkGeneration(cx, cz, out chunk) || chunk == null)
            {
                return false;
            }

            if (!ShouldAcceptChunkAt(cx, cz))
            {
                chunk.Dispose();
                chunk = null;
                return false;
            }

            var coord = (cx, cz);
            if (_chunks.ContainsKey(coord))
            {
                chunk.Dispose();
                chunk = null;
                return false;
            }

            _chunks.Add(coord, chunk);
            RegisterChunk(chunk);
            ApplyModificationsToChunk(chunk);
            FlushChunkChests(chunk);
            chunk.RebuildColumnHeights();
            return true;
        }

        private Chunk CreateChunkTerrain(int cx, int cz)
        {
            var chunk = new Chunk(cx, cz);
            _chunks.Add((cx, cz), chunk);
            RegisterChunk(chunk);
            _generator.GenerateChunkTerrain(chunk, this);
            ApplyModificationsToChunk(chunk);
            FlushChunkChests(chunk);
            chunk.RebuildColumnHeights();
            return chunk;
        }

        private void EnqueueMeshForChunk(int cx, int cz, bool invalidateExisting = false, bool markStale = false)
        {
            if (_chunks.TryGetValue((cx, cz), out var chunk))
            {
                if (markStale && chunk.HasAnyMesh())
                {
                    chunk.MarkMeshesStale();
                }
                else if (invalidateExisting && chunk.HasAnyMesh())
                {
                    chunk.InvalidateMeshes();
                }

                _pendingMesh.Add((cx, cz));
            }
        }

        private void EnqueueMeshForBlockEdit(int cx, int cz, int lx, int lz)
        {
            EnqueueMeshForBlockEditChunk(cx, cz, invalidateFlora: true);

            if (lx == 0)
            {
                EnqueueMeshForBlockEditChunk(cx - 1, cz, invalidateFlora: false);
            }

            if (lx == Chunk.Width - 1)
            {
                EnqueueMeshForBlockEditChunk(cx + 1, cz, invalidateFlora: false);
            }

            if (lz == 0)
            {
                EnqueueMeshForBlockEditChunk(cx, cz - 1, invalidateFlora: false);
            }

            if (lz == Chunk.Depth - 1)
            {
                EnqueueMeshForBlockEditChunk(cx, cz + 1, invalidateFlora: false);
            }
        }

        private void EnqueueMeshForBlockEditChunk(int cx, int cz, bool invalidateFlora)
        {
            if (!_chunks.TryGetValue((cx, cz), out var chunk))
            {
                return;
            }

            int chunkDistance = GetChunkSortDistance(cx, cz, _streamAgentCx, _streamAgentCz);
            var detail = ChunkLod.SelectDetail(chunkDistance, _streamRenderDistance);
            if (chunk.HasMesh(detail))
            {
                chunk.MarkMeshesStale();
            }

            if (invalidateFlora && chunk.HasFloraMesh())
            {
                chunk.InvalidateFloraMesh();
            }

            _pendingMesh.Add((cx, cz));
        }
        public List<Chunk> GetActiveChunks()
        {
            _lock.EnterReadLock();
            try
            {
                return new List<Chunk>(_activeChunkList);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public string GetSubGrid(int cx, int cy, int cz, int radius)
        {
            var sb = new System.Text.StringBuilder();

            for (int y = cy - radius; y <= cy + radius; y++)
            {
                sb.AppendLine($"--- Layer Y = {y} ---");
                for (int z = cz - radius; z <= cz + radius; z++)
                {
                    for (int x = cx - radius; x <= cx + radius; x++)
                    {
                        if (x == cx && y == cy && z == cz)
                        {
                            sb.Append("@ ");
                        }
                        else
                        {
                            BlockType block = GetBlock(x, y, z);
                            char symbol = block switch
                            {
                                BlockType.Air => '.',
                                BlockType.Grass => 'G',
                                BlockType.OakLog => 'W',
                                BlockType.Stone => 'S',
                                BlockType.Dirt => 'D',
                                BlockType.Water => '~',
                                BlockType.Sand => 'a',
                                BlockType.Snow => '*',
                                BlockType.Gravel => 'r',
                                BlockType.CoalOre => 'c',
                                BlockType.IronOre => 'i',
                                BlockType.GoldOre => 'o',
                                BlockType.TallGrass => 't',
                                BlockType.Flower => 'f',
                                BlockType.Cactus => 'x',
                                BlockType.Fern => 'F',
                                BlockType.MushroomRed => 'm',
                                BlockType.MushroomBrown => 'B',
                                BlockType.DeadBush => 'd',
                                BlockType.LilyPad => 'v',
                                BlockType.Vine => 'V',
                                BlockType.BerryBush => 'y',
                                BlockType.Seagrass => 'g',
                                BlockType.OakLeaves => 'L',
                                BlockType.BirchLog => 'b',
                                BlockType.BirchLeaves => 'l',
                                BlockType.PineLog => 'p',
                                BlockType.PineLeaves => 'n',
                                _ => '?'
                            };
                            sb.Append(symbol + " ");
                        }
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public int GetHighestSolidY(int x, int z)
        {
            GetChunkCoords(x, z, out int cx, out int cz, out int lx, out int lz);
            _lock.EnterReadLock();
            try
            {
                if (_chunks.TryGetValue((cx, cz), out var chunk))
                {
                    return chunk.GetCachedHighestSolidY(lx, lz);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return _generator.PreviewColumn(x, z).SurfaceHeight;
        }

        public void GenerateHeightmap(int minX, int minZ, byte[] heightData)
        {
            _lock.EnterReadLock();
            try
            {
                // Cache chunks overlapping the 256x256 heightmap bounds
                GetChunkCoords(minX, minZ, out int minCx, out int minCz, out _, out _);
                GetChunkCoords(minX + 255, minZ + 255, out int maxCx, out int maxCz, out _, out _);

                int chunksX = maxCx - minCx + 1;
                int chunksZ = maxCz - minCz + 1;
                Chunk?[,] localChunks = new Chunk?[chunksX, chunksZ];

                for (int cz = 0; cz < chunksZ; cz++)
                {
                    for (int cx = 0; cx < chunksX; cx++)
                    {
                        _chunks.TryGetValue((minCx + cx, minCz + cz), out localChunks[cx, cz]);
                    }
                }

                for (int dz = 0; dz < 256; dz++)
                {
                    int worldZ = minZ + dz;
                    GetChunkCoords(minX, worldZ, out _, out int cz, out _, out int lz);
                    int czIdx = cz - minCz;

                    for (int dx = 0; dx < 256; dx++)
                    {
                        int worldX = minX + dx;
                        GetChunkCoords(worldX, worldZ, out int cx, out _, out int lx, out _);
                        int cxIdx = cx - minCx;

                        Chunk? chunk = null;
                        if (cxIdx >= 0 && cxIdx < chunksX && czIdx >= 0 && czIdx < chunksZ)
                        {
                            chunk = localChunks[cxIdx, czIdx];
                        }

                        int highestY = 0;
                        if (chunk != null)
                        {
                            for (int y = Chunk.Height - 1; y >= 0; y--)
                            {
                                if (chunk.GetBlock(lx, y, lz) != BlockType.Air)
                                {
                                    highestY = y;
                                    break;
                                }
                            }
                        }
                        heightData[dz * 256 + dx] = (byte)highestY;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            _terrainScheduler.InvalidateGeneration();

            _lock.EnterWriteLock();
            try
            {
                foreach (var chunk in _chunks.Values)
                {
                    chunk.Dispose();
                }
                _chunks.Clear();
                _activeChunkList.Clear();
                _modifications.Clear();
                _modificationsByChunk.Clear();
                _containers.Clear();
                StructurePlacementKeys.ClearCache();
                _terrainScheduler.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            _terrainScheduler.Dispose();
            _lock.Dispose();
        }

        public void ResetForNewWorld()
        {
            _terrainScheduler.InvalidateGeneration();

            _lock.EnterWriteLock();
            try
            {
                foreach (var chunk in _chunks.Values)
                {
                    chunk.Dispose();
                }
                _chunks.Clear();
                _activeChunkList.Clear();
                _modifications.Clear();
                _modificationsByChunk.Clear();
                _containers.Clear();
                StructurePlacementKeys.ClearCache();
                _initialLoadQueue = null;
                _initialLoadIndex = 0;
                _initialLoadTotal = 0;
                _initialLoadMeshTotal = 0;
                _terrainScheduler.Clear();
                _pendingMesh.Clear();
                _lastAgentCx = int.MinValue;
                _lastAgentCz = int.MinValue;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
