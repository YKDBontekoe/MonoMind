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
        public const int DefaultTerrainChunksPerFrame = 4;
        public const int DefaultMeshChunksPerFrame = 4;
        public const int LoadingMeshChunksPerFrame = 10;

        public static int GetLoadingMeshChunksPerFrame(int renderDistance) =>
            renderDistance >= 10 ? 16 : renderDistance >= 8 ? 12 : LoadingMeshChunksPerFrame;
        public const int LoadingTerrainCompletionsPerFrame = 4;
        public const int MaxTerrainCompletionsPerFrame = 2;
        public const int MaxMeshCandidatesPerFrame = 12;
        public const float MeshBuildBudgetMs = 10f;
        public const float FastTravelMeshBuildBudgetMs = 7f;
        public const int MissingMeshScanIntervalFrames = 120;
        public const int MaxMeshBuildsInFlight = 4;
        public const int MaxMeshDispatchesPerFrame = 3;
        public const int MaxMeshUploadsPerFrame = 4;
        private const float MeshUploadBudgetMs = 6f;
        public const int FastTravelPrefetchIntervalFrames = 6;
        public const int NeighborRemeshDistanceFastTravel = 2;

        private readonly Dictionary<(int x, int z), Chunk> _chunks = new Dictionary<(int x, int z), Chunk>();
        private readonly Dictionary<(int x, int y, int z), byte> _modifications = new Dictionary<(int x, int y, int z), byte>();
        private readonly Dictionary<(int cx, int cz), List<(int x, int y, int z)>> _modificationsByChunk = new();
        private readonly List<Chunk> _activeChunkList = new();
        private readonly FluidSystem _fluids = new();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly WorldGenerator _generator;
        private readonly SemaphoreSlim _terrainGenSemaphore = new(Math.Max(1, Environment.ProcessorCount));

        private readonly Queue<(int cx, int cz)> _pendingTerrain = new Queue<(int cx, int cz)>();
        private readonly HashSet<(int cx, int cz)> _pendingTerrainLookup = new HashSet<(int cx, int cz)>();
        private readonly HashSet<(int cx, int cz)> _pendingMesh = new HashSet<(int cx, int cz)>();
        private sealed class InFlightChunkJob
        {
            public int GenerationToken { get; init; }
            public Task<Chunk?> Task { get; init; } = null!;
        }

        private readonly Dictionary<(int cx, int cz), InFlightChunkJob> _inFlightGeneration = new Dictionary<(int cx, int cz), InFlightChunkJob>();
        private int _generationToken;
        private int _streamAgentCx;
        private int _streamAgentCz;
        private int _streamRenderDistance = 8;

        private readonly List<(int cx, int cz)> _newChunkCoordsScratch = new();
        private readonly List<(int cx, int cz)> _completedCoordsScratch = new();
        private readonly List<(Chunk chunk, MeshBuildContext context, ChunkMeshDetail detail, int chunkDistance)> _meshJobsScratch = new();
        private readonly List<(int cx, int cz)> _pendingMeshSortScratch = new();
        private readonly List<(int cx, int cz)> _requeueScratch = new();
        private readonly List<(int cx, int cz)> _meshRescanScratch = new();

        private sealed class CompletedMeshUpload
        {
            public readonly Chunk Chunk;
            public readonly Chunk.PrebuiltMeshData Data;

            public CompletedMeshUpload(Chunk chunk, Chunk.PrebuiltMeshData data)
            {
                Chunk = chunk;
                Data = data;
            }
        }

        private readonly ConcurrentQueue<CompletedMeshUpload> _completedMeshUploads = new();
        private readonly ConcurrentQueue<(int cx, int cz)> _failedMeshRequeues = new();
        private int _meshBuildsInFlight;

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

        public int Seed { get; }
        public WorldGenParams GenerationParams { get; }
        public FluidSystem Fluids => _fluids;
        public int ActiveChunkCount => _activeChunkList.Count;
        public IReadOnlyList<Chunk> ActiveChunks => _activeChunkList;
        public int PendingMeshCount
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _pendingMesh.Count
                        + Volatile.Read(ref _meshBuildsInFlight)
                        + _completedMeshUploads.Count;
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
                    return _inFlightGeneration.Count;
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
                    && _pendingTerrain.Count == 0
                    && _pendingMesh.Count == 0
                    && _inFlightGeneration.Count == 0;
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
                _inFlightGeneration[(cx, cz)] = new InFlightChunkJob
                {
                    GenerationToken = _generationToken,
                    Task = Task.FromException<Chunk?>(new InvalidOperationException("Injected test fault"))
                };
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
            cx = x >= 0 ? x / Chunk.Width : (x - Chunk.Width + 1) / Chunk.Width;
            cz = z >= 0 ? z / Chunk.Depth : (z - Chunk.Depth + 1) / Chunk.Depth;

            lx = x - cx * Chunk.Width;
            lz = z - cz * Chunk.Depth;
        }

        public BlockType GetBlock(int x, int y, int z)
        {
            if (y < 0 || y >= Chunk.Height) return BlockType.Air;

            Autonocraft.Core.PerfCounters.GetBlockCalls++;
            _lock.EnterReadLock();
            try
            {
                GetChunkCoords(x, z, out int cx, out int cz, out int lx, out int lz);
                if (_chunks.TryGetValue((cx, cz), out var chunk))
                {
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
            if (_chunks.ContainsKey(coord) || _inFlightGeneration.ContainsKey(coord))
            {
                return;
            }

            var generator = _generator;
            int token = _generationToken;
            _inFlightGeneration[coord] = new InFlightChunkJob
            {
                GenerationToken = token,
                Task = Task.Run(async () =>
                {
                    await _terrainGenSemaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        if (token != _generationToken)
                        {
                            return null;
                        }

                        var chunk = new Chunk(cx, cz);
                        generator.GenerateChunkTerrain(chunk, null);
                        return chunk;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[World] Chunk generation failed at ({cx},{cz}): {ex.Message}");
                        return null;
                    }
                    finally
                    {
                        _terrainGenSemaphore.Release();
                    }
                })
            };
        }

        private bool TryCompleteChunkGeneration(int cx, int cz, out Chunk? chunk)
        {
            chunk = null;
            var coord = (cx, cz);
            if (!_inFlightGeneration.TryGetValue(coord, out var job))
            {
                return false;
            }

            if (job.GenerationToken != _generationToken)
            {
                _inFlightGeneration.Remove(coord);
                return false;
            }

            if (!job.Task.IsCompleted)
            {
                return false;
            }

            _inFlightGeneration.Remove(coord);

            if (job.Task.IsFaulted)
            {
                Console.WriteLine($"[World] Chunk generation faulted at ({cx},{cz}): {job.Task.Exception?.GetBaseException().Message}");
                return false;
            }

            if (job.Task.IsCanceled)
            {
                return false;
            }

            chunk = job.Task.GetAwaiter().GetResult();
            if (chunk == null)
            {
                return false;
            }

            if (!ShouldAcceptChunkAt(cx, cz))
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
                chunk.InvalidateMeshDetail(detail);
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
                ChunkLod.SelectRenderTarget(chunkDistance, renderDistance, restrictLod: false),
                out _);

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

        private void SelectClosestMeshJobs(
            int agentCx,
            int agentCz,
            int renderDistance,
            bool restrictLod,
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
                int sortKey = (chunk.HasAnyMesh() ? 1 : 0) * 1000 + chunkDistance;
                int insertAt = _meshJobsScratch.Count;
                for (int i = 0; i < _meshJobsScratch.Count; i++)
                {
                    int existingKey = (_meshJobsScratch[i].chunk.HasAnyMesh() ? 1 : 0) * 1000 + _meshJobsScratch[i].chunkDistance;
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
            if (_chunks.ContainsKey(coord) || _pendingTerrainLookup.Contains(coord))
            {
                return;
            }

            _pendingTerrain.Enqueue(coord);
            _pendingTerrainLookup.Add(coord);
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
            int maxCompletions = restrictLod ? 1 : MaxTerrainCompletionsPerFrame;
            float meshBudgetMs = restrictLod ? FastTravelMeshBuildBudgetMs : MeshBuildBudgetMs;

            _newChunkCoordsScratch.Clear();
            _meshJobsScratch.Clear();
            _requeueScratch.Clear();
            int meshed = 0;
            int maxMeshJobs = maxMeshPerFrame > 0
                ? Math.Min(maxMeshPerFrame, MaxMeshCandidatesPerFrame)
                : 0;

            _lock.EnterWriteLock();
            try
            {
                while (_failedMeshRequeues.TryDequeue(out var failedCoord))
                {
                    _pendingMesh.Add(failedCoord);
                }

                int terrainProcessed = 0;
                while (_pendingTerrain.Count > 0 && terrainProcessed < maxTerrainPerFrame)
                {
                    var coord = _pendingTerrain.Dequeue();
                    _pendingTerrainLookup.Remove(coord);

                    if (_chunks.ContainsKey(coord) || _inFlightGeneration.ContainsKey(coord))
                    {
                        continue;
                    }

                    StartChunkGeneration(coord.cx, coord.cz);
                    terrainProcessed++;
                }

                _completedCoordsScratch.Clear();
                _completedCoordsScratch.AddRange(_inFlightGeneration.Keys);
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
                        SelectClosestMeshJobs(agentCx, agentCz, renderDistance, restrictLod, maxMeshJobs);
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
                var uploadStopwatch = Stopwatch.StartNew();
                int uploadsThisFrame = 0;
                while (_completedMeshUploads.TryDequeue(out var pending) &&
                       uploadsThisFrame < MaxMeshUploadsPerFrame &&
                       uploadStopwatch.Elapsed.TotalMilliseconds < MeshUploadBudgetMs)
                {
                    bool stillLoaded;
                    _lock.EnterReadLock();
                    try
                    {
                        stillLoaded = _chunks.ContainsKey((pending.Chunk.ChunkX, pending.Chunk.ChunkZ));
                    }
                    finally
                    {
                        _lock.ExitReadLock();
                    }

                    if (!stillLoaded)
                    {
                        ClearMeshBuildInFlight(pending.Chunk, pending.Data.Detail);
                        continue;
                    }

                    var uploadChunkStopwatch = Stopwatch.StartNew();
                    try
                    {
                        pending.Chunk.ApplyPrebuiltMesh(device, pending.Data);
                    }
                    catch (Exception ex)
                    {
                        ClearMeshBuildInFlight(pending.Chunk, pending.Data.Detail);
                        _requeueScratch.Add((pending.Chunk.ChunkX, pending.Chunk.ChunkZ));
                        InputDebugTrace.LogChunkEvent($"mesh upload failed ({pending.Chunk.ChunkX},{pending.Chunk.ChunkZ}): {ex.Message}");
                    }

                    uploadChunkStopwatch.Stop();
                    if (!pending.Chunk.HasMesh(pending.Data.Detail))
                    {
                        _requeueScratch.Add((pending.Chunk.ChunkX, pending.Chunk.ChunkZ));
                        continue;
                    }

                    PerfCounters.RecordMeshBuild((float)uploadChunkStopwatch.Elapsed.TotalMilliseconds);
                    meshed++;
                    uploadsThisFrame++;

                    int chunkDist = GetChunkSortDistance(pending.Chunk.ChunkX, pending.Chunk.ChunkZ, agentCx, agentCz);
                    if (ChunkLod.NeedsHigherDetailBuild(pending.Chunk, chunkDist, renderDistance, restrictLod))
                    {
                        _requeueScratch.Add((pending.Chunk.ChunkX, pending.Chunk.ChunkZ));
                    }
                }
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

                    if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
                    {
                        _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    }

                    continue;
                }

                if (dispatchesThisFrame < MaxMeshDispatchesPerFrame &&
                    Volatile.Read(ref _meshBuildsInFlight) < MaxMeshBuildsInFlight)
                {
                    bool buildFlora = !restrictLod && ChunkLod.ShouldBuildFlora(chunkDistance, renderDistance);
                    SetMeshBuildInFlight(chunk, detail, true);
                    Interlocked.Increment(ref _meshBuildsInFlight);
                    dispatchesThisFrame++;

                    var capturedChunk = chunk;
                    var capturedContext = context;
                    var capturedDetail = detail;
                    var capturedFlora = buildFlora;
                    Task.Run(() =>
                    {
                        try
                        {
                            var data = capturedChunk.BuildMeshCpuOnly(capturedContext, capturedDetail, capturedFlora);
                            _completedMeshUploads.Enqueue(new CompletedMeshUpload(capturedChunk, data));
                        }
                        catch (Exception ex)
                        {
                            ClearMeshBuildInFlight(capturedChunk, capturedDetail);
                            InputDebugTrace.LogChunkEvent($"async mesh build failed ({capturedChunk.ChunkX},{capturedChunk.ChunkZ}): {ex.Message}");
                            _failedMeshRequeues.Enqueue((capturedChunk.ChunkX, capturedChunk.ChunkZ));
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _meshBuildsInFlight);
                        }
                    });

                    if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
                    {
                        _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    }

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

            _initialLoadQueue = chunksToLoad;
            _initialLoadIndex = 0;
            _initialLoadTotal = chunksToLoad.Count;
            _initialLoadMeshTotal = 0;
            _initialLoadAgentPos = agentPos;
            _initialLoadRenderDistance = renderDistance;
            _streamAgentCx = agentCx;
            _streamAgentCz = agentCz;
            _streamRenderDistance = renderDistance;
            _pendingTerrain.Clear();
            _pendingTerrainLookup.Clear();
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
            if (_initialLoadQueue == null && _pendingTerrain.Count == 0 && _pendingMesh.Count == 0 && _inFlightGeneration.Count == 0)
            {
                progress = 1f;
                status = "READY";
                return true;
            }

            Vector3 agentPos = _initialLoadAgentPos;
            var profile = ChunkStreamingProfile.Stationary(agentPos);

            int terrainProcessed = 0;
            _lock.EnterWriteLock();
            try
            {
                while (_initialLoadQueue != null && _initialLoadIndex < _initialLoadQueue.Count && terrainProcessed < chunksPerFrame)
                {
                    var coord = _initialLoadQueue[_initialLoadIndex];
                    if (!_chunks.ContainsKey(coord) && !_inFlightGeneration.ContainsKey(coord))
                    {
                        StartChunkGeneration(coord.cx, coord.cz);
                    }

                    _initialLoadIndex++;
                    terrainProcessed++;
                }

                _completedCoordsScratch.Clear();
                _completedCoordsScratch.AddRange(_inFlightGeneration.Keys);
                int completionsThisFrame = 0;
                foreach (var coord in _completedCoordsScratch)
                {
                    if (completionsThisFrame >= LoadingTerrainCompletionsPerFrame)
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
                    if (_initialLoadMeshTotal == 0)
                    {
                        _initialLoadMeshTotal = Math.Max(1, _initialLoadTotal);
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
            bool terrainDone = _initialLoadQueue == null && _inFlightGeneration.Count == 0;
            int chunksNeedingPlayable = CountChunksNeedingPlayableMeshInRange(agentCx, agentCz, _initialLoadRenderDistance);
            if (terrainDone && chunksNeedingPlayable > 0 && chunksNeedingPlayable <= 8)
            {
                ForceCompletePlayableMeshes(device, agentCx, agentCz, _initialLoadRenderDistance);
                chunksNeedingPlayable = CountChunksNeedingPlayableMeshInRange(agentCx, agentCz, _initialLoadRenderDistance);
            }

            bool meshesDone = terrainDone && chunksNeedingPlayable == 0;

            if (terrainDone && meshesDone)
            {
                progress = 1f;
                status = "READY";
                return true;
            }

            if (!terrainDone)
            {
                progress = _initialLoadTotal == 0 ? 0f : (float)_initialLoadIndex / _initialLoadTotal * 0.65f;
                status = $"BUILDING CHUNKS {_initialLoadIndex}/{_initialLoadTotal}";
            }
            else
            {
                int completed = Math.Max(0, _initialLoadMeshTotal - chunksNeedingPlayable);
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

            var unloadedChunkCoords = new List<(int cx, int cz)>();
            bool crossedChunk = agentCx != _lastAgentCx || agentCz != _lastAgentCz;

            _lock.EnterWriteLock();
            try
            {
                if (crossedChunk)
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

                    var keysToRemove = new List<(int x, int z)>();
                    foreach (var coord in _chunks.Keys)
                    {
                        int distanceX = Math.Abs(coord.x - agentCx);
                        int distanceZ = Math.Abs(coord.z - agentCz);

                        if (distanceX > renderDistance + 1 || distanceZ > renderDistance + 1)
                        {
                            keysToRemove.Add(coord);
                        }
                    }

                    foreach (var key in keysToRemove)
                    {
                        if (_chunks.TryGetValue(key, out var chunk))
                        {
                            UnregisterChunk(chunk);
                            chunk.Dispose();
                            _chunks.Remove(key);
                            unloadedChunkCoords.Add(key);
                        }

                        _pendingTerrainLookup.Remove(key);
                        _pendingMesh.Remove(key);
                        _inFlightGeneration.Remove(key);
                    }
                }

                QueueTerrainAhead(profile, renderDistance);

                if (context == null)
                {
                    while (_pendingTerrain.Count > 0)
                    {
                        var coord = _pendingTerrain.Dequeue();
                        _pendingTerrainLookup.Remove(coord);
                        if (!_chunks.ContainsKey(coord))
                        {
                            CreateChunkTerrain(coord.cx, coord.cz);
                        }
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            if (unloadedChunkCoords.Count > 0)
            {
                ChunksUnloaded?.Invoke(unloadedChunkCoords);
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

            return -1;
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
            _generationToken++;

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
                _inFlightGeneration.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            _terrainGenSemaphore.Dispose();
            _lock.Dispose();
        }

        public void ResetForNewWorld()
        {
            _generationToken++;

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
                _pendingTerrain.Clear();
                _pendingTerrainLookup.Clear();
                _pendingMesh.Clear();
                _inFlightGeneration.Clear();
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
