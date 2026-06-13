using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Autonocraft.Engine;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.World
{
    public class VoxelWorld : IDisposable
    {
        public const int DefaultTerrainChunksPerFrame = 3;
        public const int DefaultMeshChunksPerFrame = 2;
        public const int LoadingMeshChunksPerFrame = 4;

        private readonly Dictionary<(int x, int z), Chunk> _chunks = new Dictionary<(int x, int z), Chunk>();
        private readonly Dictionary<(int x, int y, int z), byte> _modifications = new Dictionary<(int x, int y, int z), byte>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly WorldGenerator _generator;

        private readonly Queue<(int cx, int cz)> _pendingTerrain = new Queue<(int cx, int cz)>();
        private readonly HashSet<(int cx, int cz)> _pendingTerrainLookup = new HashSet<(int cx, int cz)>();
        private readonly HashSet<(int cx, int cz)> _pendingMesh = new HashSet<(int cx, int cz)>();
        private readonly Dictionary<(int cx, int cz), Task<Chunk>> _inFlightGeneration = new Dictionary<(int cx, int cz), Task<Chunk>>();

        private List<(int cx, int cz)>? _initialLoadQueue;
        private int _initialLoadIndex;
        private int _initialLoadTotal;
        private int _initialLoadMeshTotal;
        private Vector3 _initialLoadAgentPos;
        private int _initialLoadRenderDistance = 8;

        public int Seed { get; }
        public WorldGenParams GenerationParams { get; }
        public int PendingMeshCount
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _pendingMesh.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
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
            if (y < 0 || y >= Chunk.Height) return;

            _lock.EnterWriteLock();
            try
            {
                _modifications[(x, y, z)] = (byte)type;

                GetChunkCoords(x, z, out int cx, out int cz, out int lx, out int lz);
                if (_chunks.TryGetValue((cx, cz), out var chunk))
                {
                    chunk.SetBlock(lx, y, lz, type);
                    RemeshChunkAtBoundary(chunk, cx, cz, lx, lz, context);
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
                foreach (var mod in data.Modifications)
                {
                    _modifications[(mod.X, mod.Y, mod.Z)] = mod.Block;
                }
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
            foreach (var entry in _modifications)
            {
                int wx = entry.Key.x;
                int wy = entry.Key.y;
                int wz = entry.Key.z;

                if (wy < 0 || wy >= Chunk.Height) continue;

                GetChunkCoords(wx, wz, out int cx, out int cz, out int lx, out int lz);
                if (cx != chunk.ChunkX || cz != chunk.ChunkZ) continue;

                chunk.SetBlock(lx, wy, lz, (BlockType)entry.Value);
            }
        }

        private void StartChunkGeneration(int cx, int cz)
        {
            var coord = (cx, cz);
            if (_chunks.ContainsKey(coord) || _inFlightGeneration.ContainsKey(coord))
            {
                return;
            }

            var generator = _generator;
            _inFlightGeneration[coord] = Task.Run(() =>
            {
                var chunk = new Chunk(cx, cz);
                generator.GenerateChunkTerrain(chunk, null);
                return chunk;
            });
        }

        private bool TryCompleteChunkGeneration(int cx, int cz, out Chunk? chunk)
        {
            chunk = null;
            var coord = (cx, cz);
            if (!_inFlightGeneration.TryGetValue(coord, out var task))
            {
                return false;
            }

            if (!task.IsCompleted)
            {
                return false;
            }

            _inFlightGeneration.Remove(coord);
            chunk = task.GetAwaiter().GetResult();
            _chunks.Add(coord, chunk);
            ApplyModificationsToChunk(chunk);
            return true;
        }

        private Chunk CreateChunkTerrain(int cx, int cz)
        {
            var chunk = new Chunk(cx, cz);
            _chunks.Add((cx, cz), chunk);
            _generator.GenerateChunkTerrain(chunk, this);
            ApplyModificationsToChunk(chunk);
            return chunk;
        }

        private void EnqueueMeshForChunk(int cx, int cz)
        {
            if (_chunks.ContainsKey((cx, cz)))
            {
                _pendingMesh.Add((cx, cz));
            }
        }

        private void EnqueueMeshForChunkAndNeighbors(Chunk chunk)
        {
            EnqueueMeshForChunk(chunk.ChunkX, chunk.ChunkZ);
            EnqueueMeshForChunk(chunk.ChunkX - 1, chunk.ChunkZ);
            EnqueueMeshForChunk(chunk.ChunkX + 1, chunk.ChunkZ);
            EnqueueMeshForChunk(chunk.ChunkX, chunk.ChunkZ - 1);
            EnqueueMeshForChunk(chunk.ChunkX, chunk.ChunkZ + 1);
        }

        private static int GetChunkSortDistance(int cx, int cz, int agentCx, int agentCz)
        {
            return Math.Max(Math.Abs(cx - agentCx), Math.Abs(cz - agentCz));
        }

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

        public int ProcessPendingWork(
            GraphicsDevice? device,
            Vector3 agentPos,
            int renderDistance,
            int maxTerrainPerFrame = DefaultTerrainChunksPerFrame,
            int maxMeshPerFrame = DefaultMeshChunksPerFrame)
        {
            GetChunkCoords((int)MathF.Round(agentPos.X), (int)MathF.Round(agentPos.Z), out int agentCx, out int agentCz, out _, out _);

            var newChunkCoords = new List<(int cx, int cz)>();
            int meshed = 0;

            _lock.EnterWriteLock();
            try
            {
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

                var completedCoords = _inFlightGeneration.Keys.ToList();
                foreach (var coord in completedCoords)
                {
                    if (TryCompleteChunkGeneration(coord.cx, coord.cz, out var chunk) && chunk != null)
                    {
                        EnqueueMeshForChunkAndNeighbors(chunk);
                        newChunkCoords.Add(coord);
                    }
                }

                if (device != null && maxMeshPerFrame > 0 && _pendingMesh.Count > 0)
                {
                    var meshCoords = _pendingMesh
                        .OrderBy(coord => GetChunkSortDistance(coord.cx, coord.cz, agentCx, agentCz))
                        .ToList();

                    foreach (var coord in meshCoords)
                    {
                        if (meshed >= maxMeshPerFrame)
                        {
                            break;
                        }

                        if (_chunks.TryGetValue(coord, out var chunk))
                        {
                            int chunkDistance = GetChunkSortDistance(coord.cx, coord.cz, agentCx, agentCz);
                            var detail = ChunkLod.SelectDetail(chunkDistance, renderDistance);
                            chunk.EnsureMesh(device, this, detail);
                        }

                        _pendingMesh.Remove(coord);
                        meshed++;
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            if (newChunkCoords.Count > 0)
            {
                ChunksLoaded?.Invoke(newChunkCoords);
            }

            return meshed;
        }

        private void RemeshChunkAtBoundary(Chunk chunk, int cx, int cz, int lx, int lz, GraphicsDevice? context)
        {
            if (context == null) return;

            chunk.InvalidateMeshes();
            chunk.GenerateAllMeshes(context, this);

            if (lx == 0 && _chunks.TryGetValue((cx - 1, cz), out var cLeft))
            {
                cLeft.InvalidateMeshes();
                cLeft.GenerateAllMeshes(context, this);
            }
            if (lx == Chunk.Width - 1 && _chunks.TryGetValue((cx + 1, cz), out var cRight))
            {
                cRight.InvalidateMeshes();
                cRight.GenerateAllMeshes(context, this);
            }
            if (lz == 0 && _chunks.TryGetValue((cx, cz - 1), out var cBack))
            {
                cBack.InvalidateMeshes();
                cBack.GenerateAllMeshes(context, this);
            }
            if (lz == Chunk.Depth - 1 && _chunks.TryGetValue((cx, cz + 1), out var cFront))
            {
                cFront.InvalidateMeshes();
                cFront.GenerateAllMeshes(context, this);
            }
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
            _pendingTerrain.Clear();
            _pendingTerrainLookup.Clear();
            _pendingMesh.Clear();
        }

        public bool AdvanceInitialLoad(
            GraphicsDevice device,
            int chunksPerFrame,
            int meshesPerFrame,
            int renderDistance,
            out float progress,
            out string status)
        {
            if (_initialLoadQueue == null && _pendingTerrain.Count == 0 && _pendingMesh.Count == 0)
            {
                progress = 1f;
                status = "READY";
                return true;
            }

            Vector3 agentPos = _initialLoadAgentPos;

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

                var completedCoords = _inFlightGeneration.Keys.ToList();
                foreach (var coord in completedCoords)
                {
                    if (TryCompleteChunkGeneration(coord.cx, coord.cz, out var chunk) && chunk != null)
                    {
                        EnqueueMeshForChunkAndNeighbors(chunk);
                    }
                }

                if (_initialLoadQueue != null && _initialLoadIndex >= _initialLoadQueue.Count)
                {
                    if (_initialLoadMeshTotal == 0)
                    {
                        _initialLoadMeshTotal = Math.Max(1, _pendingMesh.Count);
                    }

                    _initialLoadQueue = null;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            ProcessPendingWork(device, agentPos, _initialLoadRenderDistance, maxTerrainPerFrame: chunksPerFrame, maxMeshPerFrame: meshesPerFrame);

            bool terrainDone = _initialLoadQueue == null && _inFlightGeneration.Count == 0;
            bool meshesDone = _pendingMesh.Count == 0;

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
                int remaining = _pendingMesh.Count;
                int completed = Math.Max(0, _initialLoadMeshTotal - remaining);
                progress = 0.65f + completed / (float)_initialLoadMeshTotal * 0.35f;
                status = $"MESHING CHUNKS {completed}/{_initialLoadMeshTotal}";
            }

            return false;
        }

        public void UpdateChunksAround(GraphicsDevice? context, Vector3 agentPos, int renderDistance)
        {
            int agentX = (int)MathF.Round(agentPos.X);
            int agentZ = (int)MathF.Round(agentPos.Z);

            GetChunkCoords(agentX, agentZ, out int agentCx, out int agentCz, out _, out _);

            var unloadedChunkCoords = new List<(int cx, int cz)>();

            _lock.EnterWriteLock();
            try
            {
                for (int dx = -renderDistance; dx <= renderDistance; dx++)
                {
                    for (int dz = -renderDistance; dz <= renderDistance; dz++)
                    {
                        QueueTerrainLoad(agentCx + dx, agentCz + dz);
                    }
                }

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
                        chunk.Dispose();
                        _chunks.Remove(key);
                        unloadedChunkCoords.Add(key);
                    }

                    _pendingTerrainLookup.Remove(key);
                    _pendingMesh.Remove(key);
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
                return new List<Chunk>(_chunks.Values);
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
            for (int y = Chunk.Height - 1; y >= 0; y--)
            {
                if (GetBlock(x, y, z).IsSolidForSpawn())
                {
                    return y;
                }
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
            _lock.EnterWriteLock();
            try
            {
                foreach (var chunk in _chunks.Values)
                {
                    chunk.Dispose();
                }
                _chunks.Clear();
                _modifications.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void ResetForNewWorld()
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var chunk in _chunks.Values)
                {
                    chunk.Dispose();
                }
                _chunks.Clear();
                _modifications.Clear();
                _initialLoadQueue = null;
                _initialLoadIndex = 0;
                _initialLoadTotal = 0;
                _initialLoadMeshTotal = 0;
                _pendingTerrain.Clear();
                _pendingTerrainLookup.Clear();
                _pendingMesh.Clear();
                _inFlightGeneration.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
