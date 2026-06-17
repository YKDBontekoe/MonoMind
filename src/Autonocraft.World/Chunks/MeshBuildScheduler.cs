using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.World
{
    /// <summary>
    /// Pending mesh queue, async CPU mesh builds, and main-thread GPU upload dispatch.
    /// </summary>
    internal sealed class MeshBuildScheduler
    {
        public const int MaxMeshBuildsInFlight = 6;
        public const int MaxMeshDispatchesPerFrame = 4;
        public const int MaxMeshUploadsPerFrame = 6;
        private const float MeshUploadBudgetMs = 9f;

        public const int LoadingMaxMeshBuildsInFlight = 12;
        public const int LoadingMaxMeshDispatchesPerFrame = 8;
        public const int LoadingMaxMeshUploadsPerFrame = 14;
        private const float LoadingMeshUploadBudgetMs = 18f;

        public sealed class CompletedMeshUpload
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

        public int BuildsInFlight => Volatile.Read(ref _meshBuildsInFlight);
        public int CompletedUploadQueueCount => _completedMeshUploads.Count;

        public int PendingMeshCount(HashSet<(int cx, int cz)> pendingMesh) =>
            pendingMesh.Count + BuildsInFlight + CompletedUploadQueueCount;

        public void DrainFailedRequeues(HashSet<(int cx, int cz)> pendingMesh)
        {
            while (_failedMeshRequeues.TryDequeue(out var failedCoord))
            {
                pendingMesh.Add(failedCoord);
            }
        }

        public int ProcessCompletedUploads(
            GraphicsDevice device,
            int maxUploadsPerFrame,
            Func<int, int, bool> isChunkLoaded,
            Action<Chunk, ChunkMeshDetail> clearBuildInFlight,
            List<(int cx, int cz)> requeueScratch,
            int agentCx,
            int agentCz,
            int renderDistance,
            bool restrictLod,
            bool initialLoading = false,
            Action<Chunk, ChunkMeshDetail>? onMeshUploaded = null)
        {
            int maxUploads = initialLoading ? LoadingMaxMeshUploadsPerFrame : maxUploadsPerFrame;
            float uploadBudgetMs = initialLoading ? LoadingMeshUploadBudgetMs : MeshUploadBudgetMs;

            int meshed = 0;
            var uploadStopwatch = Stopwatch.StartNew();
            int uploadsThisFrame = 0;
            while (uploadsThisFrame < maxUploads &&
                   uploadStopwatch.Elapsed.TotalMilliseconds < uploadBudgetMs &&
                   _completedMeshUploads.TryDequeue(out var pending))
            {
                if (!isChunkLoaded(pending.Chunk.ChunkX, pending.Chunk.ChunkZ))
                {
                    pending.Data.ReturnToPools();
                    clearBuildInFlight(pending.Chunk, pending.Data.Detail);
                    continue;
                }

                var uploadChunkStopwatch = Stopwatch.StartNew();
                try
                {
                    pending.Chunk.ApplyPrebuiltMesh(device, pending.Data);
                }
                catch (Exception ex)
                {
                    clearBuildInFlight(pending.Chunk, pending.Data.Detail);
                    requeueScratch.Add((pending.Chunk.ChunkX, pending.Chunk.ChunkZ));
                    WorldDebugTrace.LogChunkEvent?.Invoke($"mesh upload failed ({pending.Chunk.ChunkX},{pending.Chunk.ChunkZ}): {ex.Message}");
                }
                finally
                {
                    pending.Data.ReturnToPools();
                }

                uploadChunkStopwatch.Stop();
                if (!pending.Chunk.HasMesh(pending.Data.Detail))
                {
                    requeueScratch.Add((pending.Chunk.ChunkX, pending.Chunk.ChunkZ));
                    continue;
                }

                PerfCounters.RecordMeshBuild((float)uploadChunkStopwatch.Elapsed.TotalMilliseconds);
                meshed++;
                uploadsThisFrame++;
                onMeshUploaded?.Invoke(pending.Chunk, pending.Data.Detail);

                int chunkDist = ChunkLod.GetChunkDistance(pending.Chunk.ChunkX, pending.Chunk.ChunkZ, agentCx, agentCz);
                if (ChunkLod.NeedsHigherDetailBuild(pending.Chunk, chunkDist, renderDistance, restrictLod))
                {
                    requeueScratch.Add((pending.Chunk.ChunkX, pending.Chunk.ChunkZ));
                }
            }

            return meshed;
        }

        public bool TryDispatchAsyncBuild(
            Chunk chunk,
            MeshBuildContext context,
            ChunkMeshDetail detail,
            int chunkDistance,
            int renderDistance,
            bool restrictLod,
            Action<Chunk, ChunkMeshDetail, bool> setBuildInFlight,
            Action<Chunk, ChunkMeshDetail> clearBuildInFlight,
            List<(int cx, int cz)> requeueScratch,
            Func<Chunk, int, int, bool, bool> needsHigherDetailBuild,
            bool initialLoading = false)
        {
            int maxInFlight = initialLoading ? LoadingMaxMeshBuildsInFlight : MaxMeshBuildsInFlight;
            if (Volatile.Read(ref _meshBuildsInFlight) >= maxInFlight)
            {
                return false;
            }

            bool buildFlora = !restrictLod && ChunkLod.ShouldBuildFlora(chunkDistance, renderDistance);
            setBuildInFlight(chunk, detail, true);
            Interlocked.Increment(ref _meshBuildsInFlight);

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
                    clearBuildInFlight(capturedChunk, capturedDetail);
                    WorldDebugTrace.LogChunkEvent?.Invoke($"async mesh build failed ({capturedChunk.ChunkX},{capturedChunk.ChunkZ}): {ex.Message}");
                    _failedMeshRequeues.Enqueue((capturedChunk.ChunkX, capturedChunk.ChunkZ));
                }
                finally
                {
                    Interlocked.Decrement(ref _meshBuildsInFlight);
                }
            });

            if (needsHigherDetailBuild(chunk, chunkDistance, renderDistance, restrictLod))
            {
                requeueScratch.Add((chunk.ChunkX, chunk.ChunkZ));
            }

            return true;
        }
    }
}
