using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Autonocraft.Core;
using Autonocraft.Engine;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.World
{
    public class VoxelWorld : IDisposable
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
        public const int MissingMeshScanIntervalFrames = 120;
        public const int MaxMeshBuildsInFlight = 6;
        public const int MaxMeshDispatchesPerFrame = 4;
        public const int MaxMeshUploadsPerFrame = 6;
        private const float MeshUploadBudgetMs = 9f;
        public const int FastTravelPrefetchIntervalFrames = 4;
        public const int NeighborRemeshDistanceFastTravel = 2;

        private readonly Dictionary<(int x, int z), Chunk> _chunks = new Dictionary<(int x, int z), Chunk>();
        private readonly Dictionary<(int x, int y, int z), byte> _modifications = new Dictionary<(int x, int y, int z), byte>();
        private readonly Dictionary<(int cx, int cz), List<(int x, int y, int z)>> _modificationsByChunk = new();
        private readonly List<Chunk> _activeChunkList = new();
        private readonly FluidSystem _fluids = new();
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

        private readonly List<(int cx, int cz)> _unloadedChunkCoordsScratch = new();
        private readonly List<(int x, int z)> _keysToRemoveScratch = new();
        private readonly TerrainGenScheduler _terrainScheduler = new();
        private readonly MeshBuildScheduler _meshScheduler = new();
        private readonly HashSet<(int cx, int cz)> _pendingMesh = new HashSet<(int cx, int cz)>();
        private int _streamAgentCx;
        private int _streamAgentCz;
        private int _streamRenderDistance = 8;

        private readonly List<(int cx, int cz)> _newChunkCoordsScratch = new();
        private readonly List<(int cx, int cz)> _completedCoordsScratch = new();
        private readonly List<(Chunk chunk, MeshBuildContext context, ChunkMeshDetail detail, int chunkDistance)> _meshJobsScratch = new();
        private readonly List<(int cx, int cz)> _pendingMeshSortScratch = new();
        private readonly List<(int cx, int cz)> _requeueScratch = new();
        private readonly List<(int cx, int cz)> _meshRescanScratch = new();
        private readonly List<Chunk> _forceShellScratch = new();

        private int _lastAgentCx = int.MinValue;
        private int _lastAgentCz = int.MinValue;
        private int _missingMeshScanCooldown;
        private int _prefetchCooldown;

        private List<(int cx, int cz)>? _initialLoadQueue;
        private int _initialLoadIndex;
        private int _initialLoadTotal;
        private int _initialLoadMeshTotal;
        private Vector3 _initialLoadAgentPos;
        private int _initialLoadRenderDistance = 8;
        private bool _initialLoading;
        private int _initialLoadShellsPending;

        public bool InitialLoading => _initialLoading;

        public int Seed { get; }
        public WorldGenParams GenerationParams { get; }
        public FluidSystem Fluids => _fluids;
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

            Autonocraft.Core.PerfCounters.GetBlockCalls++;
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

        private void OnChunkReadyForMeshing(Chunk chunk, ChunkStreamingProfile profile)
        {
            EnqueueMeshForChunk(chunk.ChunkX, chunk.ChunkZ, invalidateExisting: true);

            if (_initialLoading)
            {
                _initialLoadShellsPending++;
                return;
            }

            if (profile.FastTravel)
            {
                TryEnqueueNeighborRemesh(chunk.ChunkX - 1, chunk.ChunkZ, profile);
                TryEnqueueNeighborRemesh(chunk.ChunkX + 1, chunk.ChunkZ, profile);
                TryEnqueueNeighborRemesh(chunk.ChunkX, chunk.ChunkZ - 1, profile);
                TryEnqueueNeighborRemesh(chunk.ChunkX, chunk.ChunkZ + 1, profile);
            }
            else
            {
                TryEnqueueNeighborRemeshInRange(chunk.ChunkX - 1, chunk.ChunkZ, profile);
                TryEnqueueNeighborRemeshInRange(chunk.ChunkX + 1, chunk.ChunkZ, profile);
                TryEnqueueNeighborRemeshInRange(chunk.ChunkX, chunk.ChunkZ - 1, profile);
                TryEnqueueNeighborRemeshInRange(chunk.ChunkX, chunk.ChunkZ + 1, profile);
            }
        }

        private void TryEnqueueNeighborRemeshInRange(int cx, int cz, ChunkStreamingProfile profile)
        {
            int dist = GetChunkSortDistance(cx, cz, profile.AgentChunkX, profile.AgentChunkZ);
            if (dist > _streamRenderDistance)
            {
                return;
            }

            EnqueueMeshForChunk(cx, cz, markStale: true);
        }

        private void TryEnqueueNeighborRemesh(int cx, int cz, ChunkStreamingProfile profile)
        {
            if (GetChunkSortDistance(cx, cz, profile.AgentChunkX, profile.AgentChunkZ) > NeighborRemeshDistanceFastTravel)
            {
                return;
            }

            EnqueueMeshForChunk(cx, cz, markStale: true);
        }

        private void QueueTerrainAhead(ChunkStreamingProfile profile, int renderDistance)
        {
            if (!profile.FastTravel)
            {
                return;
            }

            if (--_prefetchCooldown > 0)
            {
                return;
            }

            _prefetchCooldown = FastTravelPrefetchIntervalFrames;

            int leadCx = profile.AgentChunkX;
            int leadCz = profile.AgentChunkZ;
            if (MathF.Abs(profile.Velocity.X) > 2f)
            {
                leadCx += profile.Velocity.X > 0f ? 1 : -1;
            }

            if (MathF.Abs(profile.Velocity.Z) > 2f)
            {
                leadCz += profile.Velocity.Z > 0f ? 1 : -1;
            }

            if (leadCx == profile.AgentChunkX && leadCz == profile.AgentChunkZ)
            {
                return;
            }

            for (int dx = -renderDistance; dx <= renderDistance; dx++)
            {
                for (int dz = -renderDistance; dz <= renderDistance; dz++)
                {
                    QueueTerrainLoad(leadCx + dx, leadCz + dz);
                }
            }
        }

        private static bool ChunkHasPlayableMesh(Chunk chunk, int chunkDistance, int renderDistance) =>
            ChunkLod.TryGetRenderableDetail(
                chunk,
                ChunkLod.SelectRenderTarget(chunk, chunkDistance, renderDistance, restrictLod: false),
                out _);

        private static bool ChunkHasInitialLoadMesh(Chunk chunk) =>
            chunk.HasMesh(ChunkMeshDetail.Shell);

        private void NotifyInitialLoadShellComplete(Chunk chunk)
        {
            if (!_initialLoading || chunk.InitialLoadShellReported || !chunk.HasMesh(ChunkMeshDetail.Shell))
            {
                return;
            }

            chunk.InitialLoadShellReported = true;
            _initialLoadShellsPending = Math.Max(0, _initialLoadShellsPending - 1);
        }

        private int CountChunksNeedingInitialLoadMeshInRange(int agentCx, int agentCz, int renderDistance)
        {
            int count = 0;
            _lock.EnterReadLock();
            try
            {
                foreach (var chunk in _activeChunkList)
                {
                    int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz);
                    if (chunkDistance > renderDistance)
                    {
                        continue;
                    }

                    if (!ChunkHasInitialLoadMesh(chunk))
                    {
                        count++;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return count;
        }

        private int CountActiveChunksInRange(int agentCx, int agentCz, int renderDistance)
        {
            int count = 0;
            _lock.EnterReadLock();
            try
            {
                foreach (var chunk in _activeChunkList)
                {
                    if (GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz) <= renderDistance)
                    {
                        count++;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return count;
        }

        private void ForceCompleteShellMeshes(GraphicsDevice? device, int agentCx, int agentCz, int renderDistance, int maxPerFrame)
        {
            if (device == null || maxPerFrame <= 0)
            {
                return;
            }

            _forceShellScratch.Clear();
            foreach (var chunk in _activeChunkList)
            {
                int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz);
                if (chunkDistance > renderDistance || ChunkHasInitialLoadMesh(chunk))
                {
                    continue;
                }

                if (!_chunks.TryGetValue((chunk.ChunkX, chunk.ChunkZ), out var canonical) || !ReferenceEquals(canonical, chunk))
                {
                    continue;
                }

                _forceShellScratch.Add(chunk);
            }

            _forceShellScratch.Sort((a, b) =>
            {
                int da = GetChunkSortDistance(a.ChunkX, a.ChunkZ, agentCx, agentCz);
                int db = GetChunkSortDistance(b.ChunkX, b.ChunkZ, agentCx, agentCz);
                return da != db ? da.CompareTo(db) : a.ChunkZ != b.ChunkZ ? a.ChunkZ.CompareTo(b.ChunkZ) : a.ChunkX.CompareTo(b.ChunkX);
            });

            int built = 0;
            foreach (var chunk in _forceShellScratch)
            {
                if (built >= maxPerFrame)
                {
                    break;
                }

                if (!TryCreateMeshBuildContext(chunk, out var context))
                {
                    continue;
                }

                ClearMeshBuildInFlight(chunk, ChunkMeshDetail.Shell);
                try
                {
                    chunk.EnsureMesh(device, context, ChunkMeshDetail.Shell, buildFlora: false);
                    NotifyInitialLoadShellComplete(chunk);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[Load] Force shell mesh failed ({chunk.ChunkX},{chunk.ChunkZ}): {ex.Message}");
                    chunk.ForceMarkMeshDetailComplete(ChunkMeshDetail.Shell);
                    chunk.MeshStale = false;
                }

                built++;
            }
        }

        private int CountChunksNeedingPlayableMeshInRange(int agentCx, int agentCz, int renderDistance)
        {
            int count = 0;
            _lock.EnterReadLock();
            try
            {
                foreach (var chunk in _activeChunkList)
                {
                    int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz);
                    if (chunkDistance > renderDistance)
                    {
                        continue;
                    }

                    if (!ChunkHasPlayableMesh(chunk, chunkDistance, renderDistance))
                    {
                        count++;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return count;
        }

        private void ForceCompletePlayableMeshes(GraphicsDevice? device, int agentCx, int agentCz, int renderDistance)
        {
            if (device == null)
            {
                return;
            }

            foreach (var chunk in _activeChunkList.ToArray())
            {
                int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz);
                if (chunkDistance > renderDistance || ChunkHasPlayableMesh(chunk, chunkDistance, renderDistance))
                {
                    continue;
                }

                if (!_chunks.TryGetValue((chunk.ChunkX, chunk.ChunkZ), out var canonical) || !ReferenceEquals(canonical, chunk))
                {
                    continue;
                }

                for (int attempt = 0; attempt < 4 && !ChunkHasPlayableMesh(canonical, chunkDistance, renderDistance); attempt++)
                {
                    if (!TryCreateMeshBuildContext(canonical, out var context))
                    {
                        break;
                    }

                    var detail = ChunkLod.SelectBuildDetail(canonical, chunkDistance, renderDistance, restrictLod: false);
                    ClearMeshBuildInFlight(canonical, detail);
                    bool buildFlora = ChunkLod.ShouldBuildFlora(chunkDistance, renderDistance);
                    try
                    {
                        canonical.EnsureMesh(device, context, detail, buildFlora);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[Load] Force playable mesh failed ({canonical.ChunkX},{canonical.ChunkZ}) detail={detail}: {ex.Message}");
                        canonical.ForceMarkMeshDetailComplete(detail);
                        canonical.MeshStale = false;
                        break;
                    }
                }
            }
        }

        private int CountChunksNeedingMeshInRange(int agentCx, int agentCz, int renderDistance, bool restrictLod)
        {
            int count = 0;
            _lock.EnterReadLock();
            try
            {
                foreach (var chunk in _activeChunkList)
                {
                    int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz);
                    if (chunkDistance > renderDistance)
                    {
                        continue;
                    }

                    if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
                    {
                        count++;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return count;
        }

        private static int ComputeMeshSortKey(
            (int cx, int cz) coord,
            Chunk chunk,
            int agentCx,
            int agentCz,
            int chunkDistance,
            ChunkStreamingProfile profile)
        {
            int key = (chunk.HasAnyMesh() ? 1 : 0) * 1000 + chunkDistance * 12;
            if (chunk.HasAlphaCutoutBlocks)
            {
                key -= 60;
            }
            float speedSq = profile.Velocity.X * profile.Velocity.X + profile.Velocity.Z * profile.Velocity.Z;
            if (speedSq > 1f)
            {
                float dx = coord.cx - agentCx;
                float dz = coord.cz - agentCz;
                float invSpeed = 1f / MathF.Sqrt(speedSq);
                float ahead = dx * profile.Velocity.X * invSpeed + dz * profile.Velocity.Z * invSpeed;
                if (ahead > 0f)
                {
                    key -= Math.Min(8, (int)MathF.Round(ahead * 2f));
                }
            }

            return key;
        }

        private void SelectClosestMeshJobs(
            int agentCx,
            int agentCz,
            int renderDistance,
            bool restrictLod,
            ChunkStreamingProfile profile,
            int maxJobs)
        {
            foreach (var coord in _pendingMesh)
            {
                if (!_chunks.TryGetValue(coord, out var chunk) ||
                    !TryCreateMeshBuildContextNoLock(chunk, out var context))
                {
                    continue;
                }

                int chunkDistance = GetChunkSortDistance(coord.cx, coord.cz, agentCx, agentCz);
                if (chunkDistance > renderDistance)
                {
                    continue;
                }

                if (!ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
                {
                    continue;
                }

                var detail = ChunkLod.SelectBuildDetail(chunk, chunkDistance, renderDistance, restrictLod);
                int sortKey = ComputeMeshSortKey(coord, chunk, agentCx, agentCz, chunkDistance, profile);
                int insertAt = _meshJobsScratch.Count;
                for (int i = 0; i < _meshJobsScratch.Count; i++)
                {
                    var existing = _meshJobsScratch[i];
                    int existingKey = ComputeMeshSortKey(
                        (existing.chunk.ChunkX, existing.chunk.ChunkZ),
                        existing.chunk,
                        agentCx,
                        agentCz,
                        existing.chunkDistance,
                        profile);
                    if (sortKey < existingKey)
                    {
                        insertAt = i;
                        break;
                    }
                }

                var job = (chunk, context, detail, chunkDistance);
                if (_meshJobsScratch.Count < maxJobs)
                {
                    _meshJobsScratch.Insert(insertAt, job);
                }
                else if (insertAt < maxJobs)
                {
                    _meshJobsScratch.Insert(insertAt, job);
                    _meshJobsScratch.RemoveAt(maxJobs);
                }
            }

            for (int i = _meshJobsScratch.Count - 1; i >= 0; i--)
            {
                var chunk = _meshJobsScratch[i].chunk;
                _pendingMesh.Remove((chunk.ChunkX, chunk.ChunkZ));
            }

            _pendingMeshSortScratch.Clear();
            foreach (var coord in _pendingMesh)
            {
                if (!_chunks.TryGetValue(coord, out var chunk))
                {
                    _pendingMeshSortScratch.Add(coord);
                    continue;
                }

                int chunkDistance = GetChunkSortDistance(coord.cx, coord.cz, agentCx, agentCz);
                if (chunkDistance > renderDistance)
                {
                    _pendingMeshSortScratch.Add(coord);
                    continue;
                }

                if (!TryCreateMeshBuildContextNoLock(chunk, out _))
                {
                    _pendingMeshSortScratch.Add(coord);
                    continue;
                }

                if (!ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
                {
                    _pendingMeshSortScratch.Add(coord);
                }
            }

            foreach (var coord in _pendingMeshSortScratch)
            {
                _pendingMesh.Remove(coord);
            }
        }

        private void SyncPendingMeshesForRange(int agentCx, int agentCz, int renderDistance, bool restrictLod)
        {
            foreach (var chunk in _activeChunkList)
            {
                int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz);
                if (chunkDistance > renderDistance)
                {
                    continue;
                }

                if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
                {
                    _pendingMesh.Add((chunk.ChunkX, chunk.ChunkZ));
                }
            }
        }

        private void EnqueueMissingDetailMeshes(int agentCx, int agentCz, int renderDistance, bool restrictLod = false)
        {
            _meshRescanScratch.Clear();
            _lock.EnterReadLock();
            try
            {
                foreach (var chunk in _activeChunkList)
                {
                    int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz);
                    if (chunkDistance > renderDistance)
                    {
                        continue;
                    }

                    if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
                    {
                        _meshRescanScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            if (_meshRescanScratch.Count == 0)
            {
                return;
            }

            _lock.EnterWriteLock();
            try
            {
                foreach (var coord in _meshRescanScratch)
                {
                    _pendingMesh.Add(coord);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void UpdateChunksAround(GraphicsDevice? context, Vector3 agentPos, int renderDistance)
        {
            UpdateChunksAround(context, agentPos, renderDistance, ChunkStreamingProfile.Stationary(agentPos));
        }

        public int ProcessPendingWork(
            GraphicsDevice? device,
            Vector3 agentPos,
            int renderDistance,
            int maxTerrainPerFrame = DefaultTerrainChunksPerFrame,
            int maxMeshPerFrame = DefaultMeshChunksPerFrame)
        {
            return ProcessPendingWork(
                device,
                agentPos,
                renderDistance,
                ChunkStreamingProfile.Stationary(agentPos),
                maxTerrainPerFrame,
                maxMeshPerFrame);
        }

        private static int GetChunkSortDistance(int cx, int cz, int agentCx, int agentCz)
        {
            return Math.Max(Math.Abs(cx - agentCx), Math.Abs(cz - agentCz));
        }

        private static bool IsMeshBuildInFlight(Chunk chunk, ChunkMeshDetail detail) =>
            detail switch
            {
                ChunkMeshDetail.Surface => chunk.SurfaceMeshBuildInFlight,
                ChunkMeshDetail.Shell => chunk.ShellMeshBuildInFlight,
                _ => chunk.FullMeshBuildInFlight
            };

        private static void SetMeshBuildInFlight(Chunk chunk, ChunkMeshDetail detail, bool inFlight)
        {
            switch (detail)
            {
                case ChunkMeshDetail.Surface:
                    chunk.SurfaceMeshBuildInFlight = inFlight;
                    break;
                case ChunkMeshDetail.Shell:
                    chunk.ShellMeshBuildInFlight = inFlight;
                    break;
                default:
                    chunk.FullMeshBuildInFlight = inFlight;
                    break;
            }
        }

        private static void ClearMeshBuildInFlight(Chunk chunk, ChunkMeshDetail detail) =>
            SetMeshBuildInFlight(chunk, detail, false);

        private void QueueTerrainLoad(int cx, int cz)
        {
            var coord = (cx, cz);
            if (_chunks.ContainsKey(coord) || _terrainScheduler.IsTerrainQueued(coord))
            {
                return;
            }

            _terrainScheduler.EnqueueTerrain(cx, cz);
        }

        internal bool TryCreateMeshBuildContext(Chunk center, out MeshBuildContext context)
        {
            _lock.EnterReadLock();
            try
            {
                return TryCreateMeshBuildContextNoLock(center, out context);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private bool TryCreateMeshBuildContextNoLock(Chunk center, out MeshBuildContext context)
        {
            context = null!;
            if (!_chunks.TryGetValue((center.ChunkX, center.ChunkZ), out var verified) || !ReferenceEquals(verified, center))
            {
                return false;
            }

            _chunks.TryGetValue((center.ChunkX - 1, center.ChunkZ), out var negX);
            _chunks.TryGetValue((center.ChunkX + 1, center.ChunkZ), out var posX);
            _chunks.TryGetValue((center.ChunkX, center.ChunkZ - 1), out var negZ);
            _chunks.TryGetValue((center.ChunkX, center.ChunkZ + 1), out var posZ);
            context = new MeshBuildContext(center, negX, posX, negZ, posZ, Seed, _generator.BiomeMap);
            return true;
        }

        public int ProcessPendingWork(
            GraphicsDevice? device,
            Vector3 agentPos,
            int renderDistance,
            ChunkStreamingProfile profile,
            int maxTerrainPerFrame = DefaultTerrainChunksPerFrame,
            int maxMeshPerFrame = DefaultMeshChunksPerFrame)
        {
            GetChunkCoords((int)MathF.Round(agentPos.X), (int)MathF.Round(agentPos.Z), out int agentCx, out int agentCz, out _, out _);
            bool restrictLod = profile.FastTravel;
            bool initialLoading = _initialLoading;
            int maxCompletions = restrictLod ? 1 : (initialLoading ? LoadingTerrainCompletionsPerFrame : MaxTerrainCompletionsPerFrame);
            float meshBudgetMs = restrictLod
                ? FastTravelMeshBuildBudgetMs
                : initialLoading ? LoadingMeshBuildBudgetMs : MeshBuildBudgetMs;

            _newChunkCoordsScratch.Clear();
            _meshJobsScratch.Clear();
            _requeueScratch.Clear();
            int meshed = 0;
            int meshCandidateCap = initialLoading ? LoadingMaxMeshCandidatesPerFrame : MaxMeshCandidatesPerFrame;
            int maxMeshJobs = maxMeshPerFrame > 0
                ? Math.Min(maxMeshPerFrame, meshCandidateCap)
                : 0;
            int maxMeshDispatches = initialLoading
                ? MeshBuildScheduler.LoadingMaxMeshDispatchesPerFrame
                : MeshBuildScheduler.MaxMeshDispatchesPerFrame;

            _lock.EnterWriteLock();
            try
            {
                _meshScheduler.DrainFailedRequeues(_pendingMesh);

                _terrainScheduler.DispatchPending(
                    maxTerrainPerFrame,
                    coord => _chunks.ContainsKey(coord) || _terrainScheduler.ContainsInFlight(coord),
                    StartChunkGeneration);

                _completedCoordsScratch.Clear();
                _terrainScheduler.SnapshotInFlightKeys(_completedCoordsScratch);
                int completionsThisFrame = 0;
                foreach (var coord in _completedCoordsScratch)
                {
                    if (completionsThisFrame >= maxCompletions)
                    {
                        break;
                    }

                    if (TryCompleteChunkGeneration(coord.cx, coord.cz, out var chunk) && chunk != null)
                    {
                        OnChunkReadyForMeshing(chunk, profile);
                        _newChunkCoordsScratch.Add(coord);
                        completionsThisFrame++;
                    }
                }

                if (device != null && maxMeshJobs > 0)
                {
                    SyncPendingMeshesForRange(agentCx, agentCz, renderDistance, restrictLod);
                    if (_pendingMesh.Count > 0)
                    {
                        SelectClosestMeshJobs(agentCx, agentCz, renderDistance, restrictLod, profile, maxMeshJobs);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            if (--_missingMeshScanCooldown <= 0)
            {
                _missingMeshScanCooldown = MissingMeshScanIntervalFrames;
                EnqueueMissingDetailMeshes(agentCx, agentCz, renderDistance, restrictLod);
            }

            // Phase A: upload pre-built mesh data on the main thread within a small GPU budget.
            if (device != null)
            {
                meshed += _meshScheduler.ProcessCompletedUploads(
                    device,
                    (cx, cz) =>
                    {
                        _lock.EnterReadLock();
                        try
                        {
                            return _chunks.ContainsKey((cx, cz));
                        }
                        finally
                        {
                            _lock.ExitReadLock();
                        }
                    },
                    ClearMeshBuildInFlight,
                    _requeueScratch,
                    agentCx,
                    agentCz,
                    renderDistance,
                    restrictLod,
                    initialLoading,
                    initialLoading
                        ? (Action<Chunk, ChunkMeshDetail>?)((chunk, detail) =>
                        {
                            if (detail == ChunkMeshDetail.Shell)
                            {
                                NotifyInitialLoadShellComplete(chunk);
                            }
                        })
                        : null);
            }

            // Phase B: dispatch background CPU mesh builds, then fall back to synchronous builds if needed.
            var meshStopwatch = Stopwatch.StartNew();
            int dispatchesThisFrame = 0;
            foreach (var (chunk, context, detail, chunkDistance) in _meshJobsScratch)
            {
                if (meshed > 0 && meshStopwatch.Elapsed.TotalMilliseconds >= meshBudgetMs)
                {
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                if (IsMeshBuildInFlight(chunk, detail))
                {
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                if (!chunk.HasAnyMesh())
                {
                    if (device == null)
                    {
                        _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                        continue;
                    }

                    var firstMeshStopwatch = Stopwatch.StartNew();
                    bool firstFlora = !restrictLod && ChunkLod.ShouldBuildFlora(chunkDistance, renderDistance);
                    try
                    {
                        chunk.EnsureMesh(device, context, detail, firstFlora);
                    }
                    catch (Exception ex)
                    {
                        InputDebugTrace.LogChunkEvent($"mesh failed ({chunk.ChunkX},{chunk.ChunkZ}) detail={detail}: {ex.Message}");
                        Console.WriteLine($"[Streaming] Mesh build failed for chunk ({chunk.ChunkX},{chunk.ChunkZ}) detail={detail}: {ex.Message}");
                        _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                        continue;
                    }

                    firstMeshStopwatch.Stop();
                    PerfCounters.RecordMeshBuild((float)firstMeshStopwatch.Elapsed.TotalMilliseconds);
                    meshed++;
                    if (initialLoading && detail == ChunkMeshDetail.Shell)
                    {
                        NotifyInitialLoadShellComplete(chunk);
                    }

                    if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
                    {
                        _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    }

                    continue;
                }

                if (dispatchesThisFrame < maxMeshDispatches &&
                    _meshScheduler.TryDispatchAsyncBuild(
                        chunk,
                        context,
                        detail,
                        chunkDistance,
                        renderDistance,
                        restrictLod,
                        SetMeshBuildInFlight,
                        ClearMeshBuildInFlight,
                        _requeueScratch,
                        ChunkLod.NeedsHigherDetailBuild,
                        initialLoading))
                {
                    dispatchesThisFrame++;
                    continue;
                }

                if (device == null)
                {
                    continue;
                }

                var buildStopwatch = Stopwatch.StartNew();
                bool syncFlora = !restrictLod && ChunkLod.ShouldBuildFlora(chunkDistance, renderDistance);
                try
                {
                    chunk.EnsureMesh(device, context, detail, syncFlora);
                }
                catch (Exception ex)
                {
                    InputDebugTrace.LogChunkEvent($"mesh failed ({chunk.ChunkX},{chunk.ChunkZ}) detail={detail}: {ex.Message}");
                    Console.WriteLine($"[Streaming] Mesh build failed for chunk ({chunk.ChunkX},{chunk.ChunkZ}) detail={detail}: {ex.Message}");
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                buildStopwatch.Stop();
                PerfCounters.RecordMeshBuild((float)buildStopwatch.Elapsed.TotalMilliseconds);
                meshed++;
                if (initialLoading && detail == ChunkMeshDetail.Shell)
                {
                    NotifyInitialLoadShellComplete(chunk);
                }

                if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
                {
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                }
            }

            if (_requeueScratch.Count > 0)
            {
                _lock.EnterWriteLock();
                try
                {
                    foreach (var coord in _requeueScratch)
                    {
                        _pendingMesh.Add(coord);
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            if (_newChunkCoordsScratch.Count > 0)
            {
                ChunksLoaded?.Invoke(_newChunkCoordsScratch);
            }

            Autonocraft.Core.PerfCounters.PendingMeshCount = PendingMeshCount;
            return meshed;
        }

        public void BeginInitialLoad(Vector3 agentPos, int renderDistance)
        {
            int agentX = (int)MathF.Round(agentPos.X);
            int agentZ = (int)MathF.Round(agentPos.Z);

            GetChunkCoords(agentX, agentZ, out int agentCx, out int agentCz, out _, out _);

            var chunksToLoad = new List<(int cx, int cz)>();
            for (int dx = -renderDistance; dx <= renderDistance; dx++)
            {
                for (int dz = -renderDistance; dz <= renderDistance; dz++)
                {
                    chunksToLoad.Add((agentCx + dx, agentCz + dz));
                }
            }

            chunksToLoad.Sort((a, b) =>
            {
                int da = GetChunkSortDistance(a.cx, a.cz, agentCx, agentCz);
                int db = GetChunkSortDistance(b.cx, b.cz, agentCx, agentCz);
                int cmp = da.CompareTo(db);
                return cmp != 0 ? cmp : a.cz.CompareTo(b.cz) != 0 ? a.cz.CompareTo(b.cz) : a.cx.CompareTo(b.cx);
            });

            _initialLoadQueue = chunksToLoad;
            _initialLoadIndex = 0;
            _initialLoadTotal = chunksToLoad.Count;
            _initialLoadMeshTotal = 0;
            _initialLoadAgentPos = agentPos;
            _initialLoadRenderDistance = renderDistance;
            _streamAgentCx = agentCx;
            _streamAgentCz = agentCz;
            _streamRenderDistance = renderDistance;
            _initialLoading = true;
            _initialLoadShellsPending = 0;
            _terrainScheduler.ClearPending();
            _pendingMesh.Clear();
            _lastAgentCx = int.MinValue;
            _lastAgentCz = int.MinValue;
        }

        public bool AdvanceInitialLoad(
            GraphicsDevice device,
            int chunksPerFrame,
            int meshesPerFrame,
            int renderDistance,
            out float progress,
            out string status)
        {
            if (_initialLoadQueue == null && _terrainScheduler.PendingTerrainCount == 0 && _pendingMesh.Count == 0 && _terrainScheduler.InFlightCount == 0)
            {
                _initialLoading = false;
                progress = 1f;
                status = "READY";
                return true;
            }

            Vector3 agentPos = _initialLoadAgentPos;
            var profile = ChunkStreamingProfile.Stationary(agentPos);

            int terrainDispatched = 0;
            int terrainDispatchBudget = Math.Max(chunksPerFrame, Environment.ProcessorCount);
            _lock.EnterWriteLock();
            try
            {
                while (_initialLoadQueue != null &&
                       _initialLoadIndex < _initialLoadQueue.Count &&
                       terrainDispatched < terrainDispatchBudget)
                {
                    var coord = _initialLoadQueue[_initialLoadIndex];
                    if (!_chunks.ContainsKey(coord) && !_terrainScheduler.ContainsInFlight(coord))
                    {
                        StartChunkGeneration(coord.cx, coord.cz);
                        terrainDispatched++;
                    }

                    _initialLoadIndex++;
                }

                _completedCoordsScratch.Clear();
                _terrainScheduler.SnapshotInFlightKeys(_completedCoordsScratch);
                int maxTerrainCompletions = renderDistance >= 32 ? 28 :
                    renderDistance >= 24 ? 22 :
                    renderDistance >= 16 ? 18 :
                    LoadingTerrainCompletionsPerFrame;
                int completionsThisFrame = 0;
                foreach (var coord in _completedCoordsScratch)
                {
                    if (completionsThisFrame >= maxTerrainCompletions)
                    {
                        break;
                    }

                    if (TryCompleteChunkGeneration(coord.cx, coord.cz, out var chunk) && chunk != null)
                    {
                        OnChunkReadyForMeshing(chunk, profile);
                        completionsThisFrame++;
                    }
                }

                if (_initialLoadQueue != null && _initialLoadIndex >= _initialLoadQueue.Count)
                {
                    GetChunkCoords((int)MathF.Round(agentPos.X), (int)MathF.Round(agentPos.Z), out int queueAgentCx, out int queueAgentCz, out _, out _);
                    if (_initialLoadMeshTotal == 0)
                    {
                        _initialLoadMeshTotal = Math.Max(1, CountActiveChunksInRange(queueAgentCx, queueAgentCz, _initialLoadRenderDistance));
                    }

                    _initialLoadQueue = null;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            ProcessPendingWork(device, agentPos, _initialLoadRenderDistance, profile, maxTerrainPerFrame: chunksPerFrame, maxMeshPerFrame: meshesPerFrame);

            GetChunkCoords((int)MathF.Round(agentPos.X), (int)MathF.Round(agentPos.Z), out int agentCx, out int agentCz, out _, out _);
            bool terrainDone = _initialLoadQueue == null && _terrainScheduler.InFlightCount == 0;
            if (terrainDone && _initialLoadShellsPending > 0)
            {
                ForceCompleteShellMeshes(device, agentCx, agentCz, _initialLoadRenderDistance, maxPerFrame: meshesPerFrame * 2);
            }

            bool meshesDone = terrainDone && _initialLoadShellsPending == 0;

            if (terrainDone && meshesDone)
            {
                _initialLoading = false;
                progress = 1f;
                status = "READY";
                return true;
            }

            if (!terrainDone)
            {
                int loadedChunks = CountActiveChunksInRange(agentCx, agentCz, _initialLoadRenderDistance);
                progress = _initialLoadTotal == 0 ? 0f : (float)loadedChunks / _initialLoadTotal * 0.65f;
                status = $"BUILDING CHUNKS {loadedChunks}/{_initialLoadTotal}";
            }
            else
            {
                int completed = Math.Max(0, _initialLoadMeshTotal - _initialLoadShellsPending);
                progress = 0.65f + completed / (float)_initialLoadMeshTotal * 0.35f;
                status = $"MESHING CHUNKS {completed}/{_initialLoadMeshTotal}";
            }

            return false;
        }

        public void UpdateChunksAround(GraphicsDevice? context, Vector3 agentPos, int renderDistance, ChunkStreamingProfile profile)
        {
            int agentX = (int)MathF.Round(agentPos.X);
            int agentZ = (int)MathF.Round(agentPos.Z);

            GetChunkCoords(agentX, agentZ, out int agentCx, out int agentCz, out _, out _);
            _streamAgentCx = agentCx;
            _streamAgentCz = agentCz;
            _streamRenderDistance = renderDistance;

            _unloadedChunkCoordsScratch.Clear();
            bool crossedChunk = agentCx != _lastAgentCx || agentCz != _lastAgentCz;

            _lock.EnterWriteLock();
            try
            {
                int expectedChunkCount = (renderDistance * 2 + 1) * (renderDistance * 2 + 1);
                bool needsRadiusFill = _activeChunkList.Count < expectedChunkCount;
                if (crossedChunk || needsRadiusFill)
                {
                    for (int dx = -renderDistance; dx <= renderDistance; dx++)
                    {
                        for (int dz = -renderDistance; dz <= renderDistance; dz++)
                        {
                            QueueTerrainLoad(agentCx + dx, agentCz + dz);
                        }
                    }

                    _lastAgentCx = agentCx;
                    _lastAgentCz = agentCz;

                    _keysToRemoveScratch.Clear();
                    foreach (var coord in _chunks.Keys)
                    {
                        int distanceX = Math.Abs(coord.x - agentCx);
                        int distanceZ = Math.Abs(coord.z - agentCz);

                        if (distanceX > renderDistance + 1 || distanceZ > renderDistance + 1)
                        {
                            _keysToRemoveScratch.Add(coord);
                        }
                    }

                    foreach (var key in _keysToRemoveScratch)
                    {
                        if (_chunks.TryGetValue(key, out var chunk))
                        {
                            UnregisterChunk(chunk);
                            chunk.Dispose();
                            _chunks.Remove(key);
                            _unloadedChunkCoordsScratch.Add(key);
                        }

                        _terrainScheduler.RemoveCoord(key);
                        _pendingMesh.Remove(key);
                    }
                }

                QueueTerrainAhead(profile, renderDistance);

                if (context == null)
                {
                    _terrainScheduler.DrainPendingSync((cx, cz) =>
                    {
                        if (!_chunks.ContainsKey((cx, cz)))
                        {
                            CreateChunkTerrain(cx, cz);
                        }
                    });
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            if (_unloadedChunkCoordsScratch.Count > 0)
            {
                ChunksUnloaded?.Invoke(_unloadedChunkCoordsScratch);
            }
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
