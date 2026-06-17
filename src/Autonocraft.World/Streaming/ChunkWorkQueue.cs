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
    // Pending terrain/mesh candidate selection (MeshBuildScheduler, TerrainGenScheduler).
    public partial class VoxelWorld
    {
        private readonly TerrainGenScheduler _terrainScheduler = new();
        private readonly MeshBuildScheduler _meshScheduler = new();
        private readonly HashSet<(int cx, int cz)> _pendingMesh = new HashSet<(int cx, int cz)>();
        private readonly List<(int cx, int cz)> _newChunkCoordsScratch = new();
        private readonly List<(int cx, int cz)> _completedCoordsScratch = new();
        private readonly List<(Chunk chunk, MeshBuildContext context, ChunkMeshDetail detail, int chunkDistance)> _meshJobsScratch = new();
        private readonly List<(int cx, int cz)> _pendingMeshSortScratch = new();
        private readonly List<(int cx, int cz)> _requeueScratch = new();
        private readonly List<(int cx, int cz)> _meshRescanScratch = new();
        private int _missingMeshScanIndex;
        private void OnChunkReadyForMeshing(Chunk chunk, ChunkStreamingProfile profile)
        {
            EnqueueMeshForChunk(chunk.ChunkX, chunk.ChunkZ, invalidateExisting: true);

            if (_initialLoading)
            {
                _initialLoadShellsPending++;
                return;
            }

            int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, profile.AgentChunkX, profile.AgentChunkZ);
            if (chunkDistance > NeighborRemeshMaxChunkDistance)
            {
                return;
            }

            if (!profile.FastTravel)
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
        private static int ComputeMeshSortKey(
            (int cx, int cz) coord,
            Chunk chunk,
            int agentCx,
            int agentCz,
            int chunkDistance,
            ChunkStreamingProfile profile)
        {
            int key = chunkDistance * 12;
            if (!chunk.HasAnyMesh())
            {
                key -= 200;
            }
            else
            {
                key += 400;
            }

            if (chunkDistance > 4 && !chunk.HasAnyMesh())
            {
                key += 300;
            }
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
            bool deferFullDetail,
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

                if (!ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod, deferFullDetail))
                {
                    continue;
                }

                var detail = ChunkLod.SelectBuildDetail(chunk, chunkDistance, renderDistance, restrictLod, deferFullDetail);
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

                if (!ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod, deferFullDetail))
                {
                    _pendingMeshSortScratch.Add(coord);
                }
            }

            foreach (var coord in _pendingMeshSortScratch)
            {
                _pendingMesh.Remove(coord);
            }
        }

        private void SyncPendingMeshesForRange(int agentCx, int agentCz, int renderDistance, bool restrictLod, bool deferFullDetail)
        {
            foreach (var chunk in _activeChunkList)
            {
                int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz);
                if (chunkDistance > renderDistance)
                {
                    continue;
                }

                if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod, deferFullDetail))
                {
                    _pendingMesh.Add((chunk.ChunkX, chunk.ChunkZ));
                }
            }
        }

        private void EnqueueMissingDetailMeshes(
            int agentCx,
            int agentCz,
            int renderDistance,
            bool restrictLod = false,
            bool deferFullDetail = false)
        {
            _meshRescanScratch.Clear();
            _lock.EnterReadLock();
            try
            {
                int chunkCount = _activeChunkList.Count;
                if (chunkCount == 0)
                {
                    return;
                }

                int batch = Math.Min(MissingMeshScanChunksPerFrame, chunkCount);
                for (int i = 0; i < batch; i++)
                {
                    int index = (_missingMeshScanIndex + i) % chunkCount;
                    var chunk = _activeChunkList[index];
                    int chunkDistance = GetChunkSortDistance(chunk.ChunkX, chunk.ChunkZ, agentCx, agentCz);
                    if (chunkDistance > renderDistance)
                    {
                        continue;
                    }

                    if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod, deferFullDetail))
                    {
                        _meshRescanScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    }
                }

                _missingMeshScanIndex = (_missingMeshScanIndex + batch) % chunkCount;
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
            bool initialLoading = _initialLoading;
            float gameplayMeshThrottle = Math.Clamp(GameplayMeshThrottle, 0f, 1f);
            bool deferFullDetail = !initialLoading && gameplayMeshThrottle < 1f;
            bool memoryPressure = !initialLoading &&
                                  GC.GetTotalMemory(forceFullCollection: false) > GameplayMeshMemoryPressureBytes;
            var processStopwatch = initialLoading ? null : Stopwatch.StartNew();

            _lock.EnterReadLock();
            int meshPressure;
            try
            {
                meshPressure = _pendingMesh.Count + _meshScheduler.BuildsInFlight + _meshScheduler.CompletedUploadQueueCount;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            bool highMeshPressure = !initialLoading && (meshPressure > MeshPressurePendingThreshold || memoryPressure);
            bool restrictLod = profile.FastTravel || deferFullDetail;
            int maxCompletions = profile.FastTravel && !highMeshPressure
                ? MaxTerrainCompletionsPerFrame
                : highMeshPressure
                    ? MaxTerrainCompletionsUnderMeshPressure
                    : initialLoading
                        ? LoadingTerrainCompletionsPerFrame
                        : MaxTerrainCompletionsPerFrame;
            float meshBudgetMs = profile.FastTravel && !highMeshPressure
                ? MeshBuildBudgetMs
                : restrictLod
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
                : profile.FastTravel && !highMeshPressure
                    ? MeshBuildScheduler.MaxMeshDispatchesPerFrame + 2
                    : Math.Max(1, (int)MathF.Round(MeshBuildScheduler.MaxMeshDispatchesPerFrame * MathF.Max(0.4f, gameplayMeshThrottle)));
            int meshInFlightCap = initialLoading
                ? MeshBuildScheduler.LoadingMaxMeshBuildsInFlight
                : Math.Max(1, (int)MathF.Round(MaxMeshBuildsInFlight * MathF.Max(0.35f, gameplayMeshThrottle)));
            int completedQueueCap = initialLoading
                ? 24
                : Math.Max(4, (int)MathF.Round(MeshBuildScheduler.MaxCompletedUploadsBeforePause * MathF.Max(0.5f, gameplayMeshThrottle)));

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
                    SyncPendingMeshesForRange(agentCx, agentCz, renderDistance, restrictLod, deferFullDetail);
                    if (_pendingMesh.Count > 0)
                    {
                        SelectClosestMeshJobs(agentCx, agentCz, renderDistance, restrictLod, deferFullDetail, profile, maxMeshJobs);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            EnqueueMissingDetailMeshes(agentCx, agentCz, renderDistance, restrictLod, deferFullDetail);

            if (processStopwatch != null &&
                processStopwatch.Elapsed.TotalMilliseconds >= GameplayChunkProcessBudgetMs)
            {
                return meshed;
            }

            // Phase A: upload pre-built mesh data on the main thread within a small GPU budget.
            if (device != null)
            {
                int uploadCap = initialLoading
                    ? MeshBuildScheduler.LoadingMaxMeshUploadsPerFrame
                    : highMeshPressure
                        ? 3
                        : Math.Max(2, (int)MathF.Round(MeshBuildScheduler.MaxMeshUploadsPerFrame * MathF.Max(0.5f, gameplayMeshThrottle)));
                meshed += _meshScheduler.ProcessCompletedUploads(
                    device,
                    uploadCap,
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
            int syncMeshBuildsThisFrame = 0;
            int distantShellDispatchesThisFrame = 0;
            foreach (var (chunk, context, detail, chunkDistance) in _meshJobsScratch)
            {
                if (processStopwatch != null &&
                    processStopwatch.Elapsed.TotalMilliseconds >= GameplayChunkProcessBudgetMs)
                {
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                if (meshStopwatch.Elapsed.TotalMilliseconds >= meshBudgetMs)
                {
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                if (IsMeshBuildInFlight(chunk, detail))
                {
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                bool isDistantShell = chunkDistance > 4 && detail == ChunkMeshDetail.Shell;
                if (isDistantShell && distantShellDispatchesThisFrame >= MaxDistantShellDispatchesPerFrame)
                {
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                bool preferAsync = initialLoading
                    ? chunkDistance > SyncMeshMaxChunkDistance
                    : chunkDistance > SyncMeshMaxChunkDistance || !chunk.HasAnyMesh();

                if (preferAsync &&
                    dispatchesThisFrame < maxMeshDispatches &&
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
                        (c, dist, rd, restrict) => ChunkLod.NeedsHigherDetailBuild(c, dist, rd, restrict, deferFullDetail),
                        initialLoading,
                        meshInFlightCap,
                        completedQueueCap))
                {
                    dispatchesThisFrame++;
                    if (isDistantShell)
                    {
                        distantShellDispatchesThisFrame++;
                    }

                    continue;
                }

                if (device == null)
                {
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                if (!initialLoading &&
                    (syncMeshBuildsThisFrame >= MaxSyncMeshBuildsPerFrame ||
                     chunkDistance > SyncMeshMaxChunkDistance))
                {
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                var buildStopwatch = Stopwatch.StartNew();
                bool buildFlora = !restrictLod && ChunkLod.ShouldBuildFlora(chunkDistance, renderDistance);
                try
                {
                    chunk.EnsureMesh(device, context, detail, buildFlora);
                }
                catch (Exception ex)
                {
                    WorldDebugTrace.LogChunkEvent?.Invoke($"mesh failed ({chunk.ChunkX},{chunk.ChunkZ}) detail={detail}: {ex.Message}");
                    Console.WriteLine($"[Streaming] Mesh build failed for chunk ({chunk.ChunkX},{chunk.ChunkZ}) detail={detail}: {ex.Message}");
                    _requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
                    continue;
                }

                buildStopwatch.Stop();
                PerfCounters.RecordMeshBuild((float)buildStopwatch.Elapsed.TotalMilliseconds);
                meshed++;
                syncMeshBuildsThisFrame++;
                if (initialLoading && detail == ChunkMeshDetail.Shell)
                {
                    NotifyInitialLoadShellComplete(chunk);
                }

                if (ChunkLod.NeedsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod, deferFullDetail))
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

            PerfCounters.PendingMeshCount = PendingMeshCount;
            return meshed;
        }
    }
}
