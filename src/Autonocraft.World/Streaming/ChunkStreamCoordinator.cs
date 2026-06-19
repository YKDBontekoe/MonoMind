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
    // UpdateChunksAround, load/unload, initial-load fast path.
    public partial class VoxelWorld
    {
        private readonly List<(int cx, int cz)> _unloadedChunkCoordsScratch = new();
        private readonly List<(int x, int z)> _keysToRemoveScratch = new();
        private int _streamAgentCx;
        private int _streamAgentCz;
        private int _streamRenderDistance = 8;
        private readonly List<Chunk> _forceShellScratch = new();
        private int _lastAgentCx = int.MinValue;
        private int _lastAgentCz = int.MinValue;
        private int _prefetchCooldown;
        private List<(int cx, int cz)>? _initialLoadQueue;
        private int _initialLoadIndex;
        private int _initialLoadTotal;
        private int _initialLoadMeshTotal;
        private Vector3 _initialLoadAgentPos;
        private int _initialLoadRenderDistance = 8;
        private bool _initialLoading;
        private int _initialLoadShellsPending;
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

            float speedSq = profile.Velocity.X * profile.Velocity.X + profile.Velocity.Z * profile.Velocity.Z;
            if (speedSq < 4f)
            {
                return;
            }

            float invSpeed = 1f / MathF.Sqrt(speedSq);
            int dirX = MathF.Abs(profile.Velocity.X * invSpeed) > 0.3f
                ? profile.Velocity.X > 0f ? 1 : -1
                : 0;
            int dirZ = MathF.Abs(profile.Velocity.Z * invSpeed) > 0.3f
                ? profile.Velocity.Z > 0f ? 1 : -1
                : 0;
            if (dirX == 0 && dirZ == 0)
            {
                return;
            }

            int agentCx = profile.AgentChunkX;
            int agentCz = profile.AgentChunkZ;
            const int prefetchDepth = 2;
            for (int ahead = 1; ahead <= prefetchDepth; ahead++)
            {
                if (dirX != 0)
                {
                    int leadCx = agentCx + dirX * (renderDistance + ahead);
                    for (int dz = -renderDistance; dz <= renderDistance; dz++)
                    {
                        QueueTerrainLoad(leadCx, agentCz + dz);
                    }
                }

                if (dirZ != 0)
                {
                    int leadCz = agentCz + dirZ * (renderDistance + ahead);
                    for (int dx = -renderDistance; dx <= renderDistance; dx++)
                    {
                        QueueTerrainLoad(agentCx + dx, leadCz);
                    }
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
        public void UpdateChunksAround(GraphicsDevice? context, Vector3 agentPos, int renderDistance)
        {
            UpdateChunksAround(context, agentPos, renderDistance, ChunkStreamingProfile.Stationary(agentPos));
        }
        /// <summary>
        /// Queues terrain generation for a small area without changing the player stream
        /// center or unloading distant chunks. Use for villages / jobs — never call
        /// <see cref="UpdateChunksAround"/> with a tiny render distance for this.
        /// </summary>
        public void EnsureChunksLoaded(Vector3 center, int chunkRadius)
        {
            int centerX = (int)MathF.Round(center.X);
            int centerZ = (int)MathF.Round(center.Z);
            GetChunkCoords(centerX, centerZ, out int centerCx, out int centerCz, out _, out _);

            _lock.EnterWriteLock();
            try
            {
                for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
                {
                    for (int dz = -chunkRadius; dz <= chunkRadius; dz++)
                    {
                        QueueTerrainLoad(centerCx + dx, centerCz + dz);
                    }
                }

                _terrainScheduler.DrainPendingSync((cx, cz) =>
                {
                    if (!_chunks.ContainsKey((cx, cz)))
                    {
                        CreateChunkTerrain(cx, cz);
                    }
                });
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>Marks the streaming agent chunk after initial load so the first gameplay frame does not treat it as a teleport.</summary>
        internal void SyncStreamingAgent(Vector3 agentPos)
        {
            GetChunkCoords(
                (int)MathF.Round(agentPos.X),
                (int)MathF.Round(agentPos.Z),
                out int agentCx,
                out int agentCz,
                out _,
                out _);
            _lastAgentCx = agentCx;
            _lastAgentCz = agentCz;
            _streamAgentCx = agentCx;
            _streamAgentCz = agentCz;
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
                SyncStreamingAgent(_initialLoadAgentPos);
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
                SyncStreamingAgent(agentPos);
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
                }

                if (crossedChunk)
                {
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
    }
}
