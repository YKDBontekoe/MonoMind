using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Autonocraft.World
{
    /// <summary>
    /// Background terrain generation queue and in-flight chunk jobs.
    /// </summary>
    internal sealed class TerrainGenScheduler : IDisposable
    {
        private sealed class InFlightChunkJob
        {
            public int GenerationToken { get; init; }
            public Task<Chunk?> Task { get; init; } = null!;
        }

        private readonly SemaphoreSlim _terrainGenSemaphore = new(Math.Max(1, Environment.ProcessorCount));
        private readonly Queue<(int cx, int cz)> _pendingTerrain = new();
        private readonly HashSet<(int cx, int cz)> _pendingTerrainLookup = new();
        private readonly Dictionary<(int cx, int cz), InFlightChunkJob> _inFlightGeneration = new();
        private int _generationToken;
        private bool _disposed;

        public int GenerationToken => _generationToken;
        public int PendingTerrainCount => _pendingTerrain.Count;
        public int InFlightCount => _inFlightGeneration.Count;

        public void InvalidateGeneration() => _generationToken++;

        public void Clear()
        {
            InvalidateGeneration();
            WaitForInFlight(TimeSpan.FromSeconds(2));
            _pendingTerrain.Clear();
            _pendingTerrainLookup.Clear();
            _inFlightGeneration.Clear();
        }

        public bool ContainsInFlight((int cx, int cz) coord) => _inFlightGeneration.ContainsKey(coord);

        public void EnqueueTerrain(int cx, int cz)
        {
            var coord = (cx, cz);
            if (_pendingTerrainLookup.Add(coord))
            {
                _pendingTerrain.Enqueue(coord);
            }
        }

        public int DispatchPending(int maxPerFrame, Func<(int cx, int cz), bool> shouldSkip, Action<int, int> startGeneration)
        {
            int terrainProcessed = 0;
            while (_pendingTerrain.Count > 0 && terrainProcessed < maxPerFrame)
            {
                var coord = _pendingTerrain.Dequeue();
                _pendingTerrainLookup.Remove(coord);

                if (shouldSkip(coord))
                {
                    continue;
                }

                startGeneration(coord.cx, coord.cz);
                terrainProcessed++;
            }

            return terrainProcessed;
        }

        public void SnapshotInFlightKeys(List<(int cx, int cz)> destination) =>
            destination.AddRange(_inFlightGeneration.Keys);

        public void StartChunkGeneration(int cx, int cz, WorldGenerator generator)
        {
            var coord = (cx, cz);
            if (_inFlightGeneration.ContainsKey(coord))
            {
                return;
            }

            int token = _generationToken;
            _inFlightGeneration[coord] = new InFlightChunkJob
            {
                GenerationToken = token,
                Task = Task.Run(async () =>
                {
                    try
                    {
                        await _terrainGenSemaphore.WaitAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        return null;
                    }

                    try
                    {
                        if (_disposed || token != _generationToken)
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
                        try
                        {
                            _terrainGenSemaphore.Release();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    }
                })
            };
        }

        public bool TryCompleteChunkGeneration(int cx, int cz, out Chunk? chunk)
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
            return chunk != null;
        }

        public void InjectFaultedJobForTests(int cx, int cz)
        {
            _inFlightGeneration[(cx, cz)] = new InFlightChunkJob
            {
                GenerationToken = _generationToken,
                Task = Task.FromException<Chunk?>(new InvalidOperationException("Injected test fault"))
            };
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            InvalidateGeneration();
            WaitForInFlight(TimeSpan.FromSeconds(5));
            _pendingTerrain.Clear();
            _pendingTerrainLookup.Clear();
            _inFlightGeneration.Clear();
            _terrainGenSemaphore.Dispose();
        }

        private void WaitForInFlight(TimeSpan timeout)
        {
            if (_inFlightGeneration.Count == 0)
            {
                return;
            }

            var tasks = new Task[_inFlightGeneration.Count];
            int index = 0;
            foreach (var job in _inFlightGeneration.Values)
            {
                tasks[index++] = job.Task;
            }

            try
            {
                Task.WaitAll(tasks, timeout);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[World] Waiting for terrain generation to finish: {ex.Message}");
            }
        }
        public bool IsTerrainQueued((int cx, int cz) coord) => _pendingTerrainLookup.Contains(coord);

        public void RemoveCoord((int cx, int cz) coord)
        {
            _pendingTerrainLookup.Remove(coord);
            _inFlightGeneration.Remove(coord);
        }

        public void ClearPending()
        {
            _pendingTerrain.Clear();
            _pendingTerrainLookup.Clear();
        }

        public void DrainPendingSync(Action<int, int> createIfMissing)
        {
            while (_pendingTerrain.Count > 0)
            {
                var coord = _pendingTerrain.Dequeue();
                _pendingTerrainLookup.Remove(coord);
                createIfMissing(coord.cx, coord.cz);
            }
        }
    }
}
