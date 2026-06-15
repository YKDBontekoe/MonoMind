using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Domain.World;
using Autonocraft.Engine;

namespace Autonocraft.World
{
    public enum ChunkMeshDetail
    {
        Full,
        Surface,
        Shell
    }

    public class Chunk : IDisposable
    {
        public const int Width = 16;
        public const int Height = 192;
        public const int Depth = 16;

        public int ChunkX { get; }
        public int ChunkZ { get; }

        private readonly BlockType[] _blocks;
        // Cached highest/lowest solid Y per column (lx + lz * Width). -1 = no solid block.
        private readonly short[] _columnHighestSolid = new short[Width * Depth];
        private readonly short[] _columnLowestSolid = new short[Width * Depth];
        // Mesh extent includes alpha-cutout blocks (e.g. tree leaves above trunks).
        private readonly short[] _columnHighestMesh = new short[Width * Depth];
        private readonly short[] _columnLowestMesh = new short[Width * Depth];
        private bool _columnHeightsBuilt;

        private VertexBuffer? _fullVertexBuffer;
        private IndexBuffer? _fullIndexBuffer;
        private int _fullIndexCount;

        private VertexBuffer? _fullWaterVertexBuffer;
        private IndexBuffer? _fullWaterIndexBuffer;
        private int _fullWaterIndexCount;
        private Vertex[]? _fullWaterVertices;

        private VertexBuffer? _surfaceVertexBuffer;
        private IndexBuffer? _surfaceIndexBuffer;
        private int _surfaceIndexCount;

        private VertexBuffer? _surfaceWaterVertexBuffer;
        private IndexBuffer? _surfaceWaterIndexBuffer;
        private int _surfaceWaterIndexCount;
        private Vertex[]? _surfaceWaterVertices;

        private VertexBuffer? _shellVertexBuffer;
        private IndexBuffer? _shellIndexBuffer;
        private int _shellIndexCount;

        private FloraVertex[]? _floraVertices;
        private uint[]? _floraIndices;
        private int _floraIndexCount;

        private bool _fullMeshBuilt;
        private bool _surfaceMeshBuilt;
        private bool _shellMeshBuilt;
        private bool _floraMeshBuilt;
        // Written from background thread: volatile prevents stale reads.
        private volatile bool _hasWaterBlocks;
        private volatile bool _hasAlphaCutoutBlocks;

        // Set on main thread before Task.Run, cleared on main thread in ApplyPrebuiltMesh.
        // Prevents queueing duplicate background builds for the same chunk+detail.
        internal volatile bool FullMeshBuildInFlight;
        internal volatile bool SurfaceMeshBuildInFlight;
        internal volatile bool ShellMeshBuildInFlight;
        // Set when a neighbor chunk appeared — keeps old GPU buffers visible while rebuilding.
        internal volatile bool MeshStale;

        private List<Vertex>? _vertexScratch;
        private List<uint>? _indexScratch;

        /// <summary>Holds vertex/index arrays computed on a background thread, ready for GPU upload.</summary>
        internal sealed class PrebuiltMeshData
        {
            public readonly Vertex[] Vertices;
            public readonly uint[] Indices;
            public readonly Vertex[] WaterVertices;
            public readonly uint[] WaterIndices;
            public readonly ChunkMeshDetail Detail;
            public readonly bool BuildFlora;
            public readonly FloraVertex[]? FloraVertices;
            public readonly uint[]? FloraIndices;

            public PrebuiltMeshData(Vertex[] v, uint[] i, Vertex[] wv, uint[] wi, ChunkMeshDetail detail, bool buildFlora,
                FloraVertex[]? fv = null, uint[]? fi = null)
            {
                Vertices = v; Indices = i; WaterVertices = wv; WaterIndices = wi; Detail = detail; BuildFlora = buildFlora;
                FloraVertices = fv; FloraIndices = fi;
            }
        }

        public bool HasWaterBlocks => _hasWaterBlocks;
        public bool HasAlphaCutoutBlocks => _hasAlphaCutoutBlocks;

        public Vertex[]? GetWaterVertices(ChunkMeshDetail detail)
        {
            return detail switch
            {
                ChunkMeshDetail.Surface => _surfaceWaterVertices,
                _ => _fullWaterVertices
            };
        }

        public (VertexBuffer? vertexBuffer, IndexBuffer? indexBuffer, int indexCount) GetWaterMesh(ChunkMeshDetail detail)
        {
            return detail switch
            {
                ChunkMeshDetail.Surface => (_surfaceWaterVertexBuffer, _surfaceWaterIndexBuffer, _surfaceWaterIndexCount),
                ChunkMeshDetail.Shell => (null, null, 0),
                _ => (_fullWaterVertexBuffer, _fullWaterIndexBuffer, _fullWaterIndexCount)
            };
        }

        public VertexBuffer? VertexBuffer => _fullVertexBuffer;
        public IndexBuffer? IndexBuffer => _fullIndexBuffer;
        public int IndexCount => _fullIndexCount;

        public int FullIndexCount => _fullIndexCount;
        public int SurfaceIndexCount => _surfaceIndexCount;
        public int ShellIndexCount => _shellIndexCount;

        public Chunk(int chunkX, int chunkZ)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            _blocks = new BlockType[Width * Height * Depth];
            Array.Fill(_columnHighestSolid, (short)-1);
            Array.Fill(_columnLowestSolid, (short)-1);
            Array.Fill(_columnHighestMesh, (short)-1);
            Array.Fill(_columnLowestMesh, (short)-1);
        }

        /// <summary>Rebuilds per-column height cache after terrain generation or bulk edits.</summary>
        internal void RebuildColumnHeights()
        {
            for (int lz = 0; lz < Depth; lz++)
            {
                for (int lx = 0; lx < Width; lx++)
                {
                    int lowestSolid = -1;
                    int highestSolid = -1;
                    int lowestMesh = -1;
                    int highestMesh = -1;
                    for (int y = 0; y < Height; y++)
                    {
                        BlockType type = _blocks[GetIndex(lx, y, lz)];
                        if (type.IsSolidForSpawn())
                        {
                            if (lowestSolid < 0)
                            {
                                lowestSolid = y;
                            }

                            highestSolid = y;
                        }

                        if (type.IsSolidForSpawn() || type.IsAlphaCutout())
                        {
                            if (lowestMesh < 0)
                            {
                                lowestMesh = y;
                            }

                            highestMesh = y;
                        }
                    }

                    int idx = lz * Width + lx;
                    _columnLowestSolid[idx] = (short)lowestSolid;
                    _columnHighestSolid[idx] = (short)highestSolid;
                    _columnLowestMesh[idx] = (short)lowestMesh;
                    _columnHighestMesh[idx] = (short)highestMesh;
                }
            }

            _columnHeightsBuilt = true;
        }

        internal int GetCachedLowestSolidY(int lx, int lz)
        {
            if (!_columnHeightsBuilt)
            {
                RebuildColumnHeights();
            }

            return _columnLowestSolid[lz * Width + lx];
        }

        internal int GetCachedHighestSolidY(int lx, int lz)
        {
            if (!_columnHeightsBuilt)
            {
                RebuildColumnHeights();
            }

            return _columnHighestSolid[lz * Width + lx];
        }

        private int GetIndex(int x, int y, int z)
        {
            return x + z * Width + y * Width * Depth;
        }

        public bool IsInLocalBounds(int x, int y, int z)
        {
            return x >= 0 && x < Width &&
                   y >= 0 && y < Height &&
                   z >= 0 && z < Depth;
        }

        public BlockType GetBlock(int x, int y, int z)
        {
            if (!IsInLocalBounds(x, y, z)) return BlockType.Air;
            return _blocks[GetIndex(x, y, z)];
        }

        public void SetBlock(int x, int y, int z, BlockType type)
        {
            if (!IsInLocalBounds(x, y, z))
            {
                return;
            }

            _blocks[GetIndex(x, y, z)] = type;
            if (_columnHeightsBuilt)
            {
                UpdateColumnHeightCache(x, y, z, type);
            }
        }

        internal int GetCachedHighestMeshY(int lx, int lz)
        {
            if (!_columnHeightsBuilt)
            {
                RebuildColumnHeights();
            }

            return _columnHighestMesh[lz * Width + lx];
        }

        internal int GetCachedLowestMeshY(int lx, int lz)
        {
            if (!_columnHeightsBuilt)
            {
                RebuildColumnHeights();
            }

            return _columnLowestMesh[lz * Width + lx];
        }

        private void UpdateColumnHeightCache(int lx, int y, int lz, BlockType type)
        {
            int idx = lz * Width + lx;
            bool solid = type.IsSolidForSpawn();
            bool meshBlock = solid || type.IsAlphaCutout();

            if (solid)
            {
                int currentHigh = _columnHighestSolid[idx];
                int currentLow = _columnLowestSolid[idx];
                if (currentHigh < 0 || y > currentHigh)
                {
                    _columnHighestSolid[idx] = (short)y;
                }

                if (currentLow < 0 || y < currentLow)
                {
                    _columnLowestSolid[idx] = (short)y;
                }
            }

            if (meshBlock)
            {
                int meshHigh = _columnHighestMesh[idx];
                int meshLow = _columnLowestMesh[idx];
                if (meshHigh < 0 || y > meshHigh)
                {
                    _columnHighestMesh[idx] = (short)y;
                }

                if (meshLow < 0 || y < meshLow)
                {
                    _columnLowestMesh[idx] = (short)y;
                }

                return;
            }

            int high = _columnHighestSolid[idx];
            int low = _columnLowestSolid[idx];
            int meshHighScan = _columnHighestMesh[idx];
            int meshLowScan = _columnLowestMesh[idx];
            if (y != high && y != low && y != meshHighScan && y != meshLowScan)
            {
                return;
            }

            int lowestSolid = -1;
            int highestSolid = -1;
            int lowestMesh = -1;
            int highestMesh = -1;
            for (int scanY = 0; scanY < Height; scanY++)
            {
                BlockType scanType = _blocks[GetIndex(lx, scanY, lz)];
                if (scanType.IsSolidForSpawn())
                {
                    if (lowestSolid < 0)
                    {
                        lowestSolid = scanY;
                    }

                    highestSolid = scanY;
                }

                if (scanType.IsSolidForSpawn() || scanType.IsAlphaCutout())
                {
                    if (lowestMesh < 0)
                    {
                        lowestMesh = scanY;
                    }

                    highestMesh = scanY;
                }
            }

            _columnLowestSolid[idx] = (short)lowestSolid;
            _columnHighestSolid[idx] = (short)highestSolid;
            _columnLowestMesh[idx] = (short)lowestMesh;
            _columnHighestMesh[idx] = (short)highestMesh;
        }

        public void GenerateAllMeshes(GraphicsDevice device, VoxelWorld world)
        {
            if (!world.TryCreateMeshBuildContext(this, out var context))
            {
                return;
            }

            EnsureMesh(device, context, ChunkMeshDetail.Full);
            EnsureMesh(device, context, ChunkMeshDetail.Surface);
            EnsureMesh(device, context, ChunkMeshDetail.Shell);
        }

        public bool HasFloraMesh() => _floraMeshBuilt && _floraIndexCount > 0;

        public (FloraVertex[]? vertices, uint[]? indices, int indexCount) GetFloraMesh()
        {
            return (_floraVertices, _floraIndices, _floraIndexCount);
        }

        // Flora vertex/index buffers are managed globally in FloraRenderer.cs for batching.

        public void EnsureFloraMesh(int seed, BiomeMap? biomeMap = null)
        {
            if (_floraMeshBuilt)
            {
                return;
            }

            var vertices = new List<FloraVertex>(1024);
            var indices = new List<uint>(1536);
            FloraMeshBuilder.Build(this, biomeMap, vertices, indices);

            _floraIndexCount = indices.Count;
            if (_floraIndexCount > 0)
            {
                _floraVertices = vertices.ToArray();
                _floraIndices = indices.ToArray();
            }

            _floraMeshBuilt = true;
        }

        public bool HasMesh(ChunkMeshDetail detail)
        {
            return detail switch
            {
                ChunkMeshDetail.Surface => _surfaceMeshBuilt,
                ChunkMeshDetail.Shell => _shellMeshBuilt,
                _ => _fullMeshBuilt
            };
        }

        public bool HasAnyMesh()
        {
            return _fullMeshBuilt || _surfaceMeshBuilt || _shellMeshBuilt;
        }

        /// <summary>
        /// Marks meshes as needing rebuild without disposing GPU buffers.
        /// Old geometry stays visible until the replacement upload completes.
        /// </summary>
        internal void MarkMeshesStale()
        {
            MeshStale = true;
        }

        internal void EnsureMeshForTest(ChunkMeshDetail detail)
        {
            switch (detail)
            {
                case ChunkMeshDetail.Surface:
                    _surfaceMeshBuilt = true;
                    break;
                case ChunkMeshDetail.Shell:
                    _shellMeshBuilt = true;
                    break;
                default:
                    _fullMeshBuilt = true;
                    break;
            }
        }

        public void EnsureMesh(GraphicsDevice device, VoxelWorld world, ChunkMeshDetail detail)
        {
            if (HasMesh(detail) && !MeshStale)
            {
                return;
            }

            if (!world.TryCreateMeshBuildContext(this, out var context))
            {
                return;
            }

            EnsureMesh(device, context, detail);
        }

        internal void EnsureMesh(GraphicsDevice device, MeshBuildContext context, ChunkMeshDetail detail, bool buildFlora = true)
        {
            VertexBuffer? dummyVB = null;
            IndexBuffer? dummyIB = null;
            int dummyCount = 0;
            Vertex[]? dummyVertices = null;

            switch (detail)
            {
                case ChunkMeshDetail.Surface:
                    if (_surfaceMeshBuilt && !MeshStale) return;
                    BuildMesh(device, context, ChunkMeshDetail.Surface, ref _surfaceVertexBuffer, ref _surfaceIndexBuffer, ref _surfaceIndexCount,
                        ref _surfaceWaterVertexBuffer, ref _surfaceWaterIndexBuffer, ref _surfaceWaterIndexCount, ref _surfaceWaterVertices);
                    _surfaceMeshBuilt = true;
                    break;
                case ChunkMeshDetail.Shell:
                    if (_shellMeshBuilt && !MeshStale) return;
                    BuildMesh(device, context, ChunkMeshDetail.Shell, ref _shellVertexBuffer, ref _shellIndexBuffer, ref _shellIndexCount,
                        ref dummyVB, ref dummyIB, ref dummyCount, ref dummyVertices);
                    _shellMeshBuilt = true;
                    break;
                default:
                    if (_fullMeshBuilt && !MeshStale) return;
                    BuildMesh(device, context, ChunkMeshDetail.Full, ref _fullVertexBuffer, ref _fullIndexBuffer, ref _fullIndexCount,
                        ref _fullWaterVertexBuffer, ref _fullWaterIndexBuffer, ref _fullWaterIndexCount, ref _fullWaterVertices);
                    _fullMeshBuilt = true;
                    break;
            }

            MeshStale = false;

            if (buildFlora)
            {
                EnsureFloraMesh(context.Seed, context.BiomeMap);
            }
        }

        /// <summary>
        /// Pure CPU mesh build — safe to call from any thread.
        /// Reads only immutable _blocks data; does NOT touch the GraphicsDevice.
        /// </summary>
        internal PrebuiltMeshData BuildMeshCpuOnly(MeshBuildContext context, ChunkMeshDetail detail, bool buildFlora)
        {
            var vertices = new List<Vertex>(8192);
            var indices = new List<uint>(12288);
            var waterVertices = new List<Vertex>(2048);
            var waterIndices = new List<uint>(3072);

            if (detail == ChunkMeshDetail.Shell)
                BuildShellMesh(context, ChunkX * Width, ChunkZ * Depth, vertices, indices);
            else if (detail == ChunkMeshDetail.Surface)
                BuildSurfaceMeshList(context, vertices, indices, waterVertices, waterIndices);
            else
                BuildFullMeshList(context, vertices, indices, waterVertices, waterIndices);

            FloraVertex[]? fv = null;
            uint[]? fi = null;
            if (buildFlora && !_floraMeshBuilt)
            {
                var fvList = new List<FloraVertex>(1024);
                var fiList = new List<uint>(1536);
                FloraMeshBuilder.Build(this, context.BiomeMap, fvList, fiList);
                if (fvList.Count > 0) { fv = fvList.ToArray(); fi = fiList.ToArray(); }
            }

            return new PrebuiltMeshData(vertices.ToArray(), indices.ToArray(), waterVertices.ToArray(), waterIndices.ToArray(), detail, buildFlora, fv, fi);
        }

        /// <summary>
        /// Main-thread GPU upload of mesh data pre-built by <see cref="BuildMeshCpuOnly"/>.
        /// Also clears the corresponding in-flight flag.
        /// </summary>
        private void MarkEmptyMeshDetail(ChunkMeshDetail detail)
        {
            switch (detail)
            {
                case ChunkMeshDetail.Surface:
                    _surfaceVertexBuffer?.Dispose();
                    _surfaceIndexBuffer?.Dispose();
                    _surfaceVertexBuffer = null;
                    _surfaceIndexBuffer = null;
                    _surfaceIndexCount = 0;
                    _surfaceWaterVertexBuffer?.Dispose();
                    _surfaceWaterIndexBuffer?.Dispose();
                    _surfaceWaterVertexBuffer = null;
                    _surfaceWaterIndexBuffer = null;
                    _surfaceWaterIndexCount = 0;
                    _surfaceWaterVertices = null;
                    _surfaceMeshBuilt = true;
                    break;
                case ChunkMeshDetail.Shell:
                    _shellVertexBuffer?.Dispose();
                    _shellIndexBuffer?.Dispose();
                    _shellVertexBuffer = null;
                    _shellIndexBuffer = null;
                    _shellIndexCount = 0;
                    _shellMeshBuilt = true;
                    break;
                default:
                    _fullVertexBuffer?.Dispose();
                    _fullIndexBuffer?.Dispose();
                    _fullVertexBuffer = null;
                    _fullIndexBuffer = null;
                    _fullIndexCount = 0;
                    _fullWaterVertexBuffer?.Dispose();
                    _fullWaterIndexBuffer?.Dispose();
                    _fullWaterVertexBuffer = null;
                    _fullWaterIndexBuffer = null;
                    _fullWaterIndexCount = 0;
                    _fullWaterVertices = null;
                    _fullMeshBuilt = true;
                    break;
            }
        }

        internal void ForceMarkMeshDetailComplete(ChunkMeshDetail detail) => MarkEmptyMeshDetail(detail);

        internal void ApplyPrebuiltMesh(GraphicsDevice device, PrebuiltMeshData data)
        {
            bool wasStale = MeshStale;

            switch (data.Detail)
            {
                case ChunkMeshDetail.Surface:
                    if (data.Indices.Length == 0 && data.WaterIndices.Length == 0)
                    {
                        MarkEmptyMeshDetail(ChunkMeshDetail.Surface);
                    }
                    else
                    {
                        if (!_surfaceMeshBuilt || wasStale)
                        {
                            UploadMeshBuffers(device, data.Vertices, data.Indices,
                                ref _surfaceVertexBuffer, ref _surfaceIndexBuffer, ref _surfaceIndexCount);
                            UploadMeshBuffers(device, data.WaterVertices, data.WaterIndices,
                                ref _surfaceWaterVertexBuffer, ref _surfaceWaterIndexBuffer, ref _surfaceWaterIndexCount);
                            _surfaceWaterVertices = data.WaterVertices;
                            _surfaceMeshBuilt = true;
                        }
                    }
                    SurfaceMeshBuildInFlight = false;
                    break;
                case ChunkMeshDetail.Shell:
                    if (data.Indices.Length == 0)
                    {
                        MarkEmptyMeshDetail(ChunkMeshDetail.Shell);
                    }
                    else if (!_shellMeshBuilt || wasStale)
                    {
                        UploadMeshBuffers(device, data.Vertices, data.Indices,
                            ref _shellVertexBuffer, ref _shellIndexBuffer, ref _shellIndexCount);
                        _shellMeshBuilt = true;
                    }
                    ShellMeshBuildInFlight = false;
                    break;
                default:
                    if (data.Indices.Length == 0 && data.WaterIndices.Length == 0)
                    {
                        MarkEmptyMeshDetail(ChunkMeshDetail.Full);
                    }
                    else
                    {
                        if (!_fullMeshBuilt || wasStale)
                        {
                            UploadMeshBuffers(device, data.Vertices, data.Indices,
                                ref _fullVertexBuffer, ref _fullIndexBuffer, ref _fullIndexCount);
                            UploadMeshBuffers(device, data.WaterVertices, data.WaterIndices,
                                ref _fullWaterVertexBuffer, ref _fullWaterIndexBuffer, ref _fullWaterIndexCount);
                            _fullWaterVertices = data.WaterVertices;
                            _fullMeshBuilt = true;
                        }
                    }
                    FullMeshBuildInFlight = false;
                    break;
            }

            MeshStale = false;

            if (data.BuildFlora && (!_floraMeshBuilt || wasStale))
            {
                _floraVertices = data.FloraVertices;
                _floraIndices = data.FloraIndices;
                _floraIndexCount = data.FloraIndices?.Length ?? 0;
                _floraMeshBuilt = true;
            }
        }

        /// <summary>CPU-only mesh build for profiling without a graphics device.</summary>
        internal float BenchmarkBuildMeshCpu(MeshBuildContext context, ChunkMeshDetail detail)
        {
            _vertexScratch ??= new List<Vertex>(8192);
            _indexScratch ??= new List<uint>(12288);
            _waterVertexScratch ??= new List<Vertex>(2048);
            _waterIndexScratch ??= new List<uint>(3072);
            _vertexScratch.Clear();
            _indexScratch.Clear();
            _waterVertexScratch.Clear();
            _waterIndexScratch.Clear();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (detail == ChunkMeshDetail.Shell)
            {
                BuildShellMesh(context, ChunkX * Width, ChunkZ * Depth, _vertexScratch, _indexScratch);
            }
            else if (detail == ChunkMeshDetail.Surface)
            {
                BuildSurfaceMeshList(context, _vertexScratch, _indexScratch, _waterVertexScratch, _waterIndexScratch);
            }
            else
            {
                BuildFullMeshList(context, _vertexScratch, _indexScratch, _waterVertexScratch, _waterIndexScratch);
            }

            sw.Stop();
            return (float)sw.Elapsed.TotalMilliseconds;
        }

        public void InvalidateMeshes()
        {
            DisposeMeshBuffers();
            _fullMeshBuilt = false;
            _surfaceMeshBuilt = false;
            _shellMeshBuilt = false;
            _floraMeshBuilt = false;
            _hasWaterBlocks = false;
            _hasAlphaCutoutBlocks = false;
            // Reset in-flight flags so re-queuing works immediately.
            FullMeshBuildInFlight = false;
            SurfaceMeshBuildInFlight = false;
            ShellMeshBuildInFlight = false;
            MeshStale = false;
        }

        /// <summary>
        /// Drops one LOD tier so coarser meshes can still render while this tier rebuilds.
        /// </summary>
        public void InvalidateMeshDetail(ChunkMeshDetail detail)
        {
            switch (detail)
            {
                case ChunkMeshDetail.Surface:
                    _surfaceVertexBuffer?.Dispose();
                    _surfaceIndexBuffer?.Dispose();
                    _surfaceWaterVertexBuffer?.Dispose();
                    _surfaceWaterIndexBuffer?.Dispose();
                    _surfaceVertexBuffer = null;
                    _surfaceIndexBuffer = null;
                    _surfaceWaterVertexBuffer = null;
                    _surfaceWaterIndexBuffer = null;
                    _surfaceIndexCount = 0;
                    _surfaceWaterIndexCount = 0;
                    _surfaceWaterVertices = null;
                    _surfaceMeshBuilt = false;
                    break;
                case ChunkMeshDetail.Shell:
                    _shellVertexBuffer?.Dispose();
                    _shellIndexBuffer?.Dispose();
                    _shellVertexBuffer = null;
                    _shellIndexBuffer = null;
                    _shellIndexCount = 0;
                    _shellMeshBuilt = false;
                    break;
                default:
                    _fullVertexBuffer?.Dispose();
                    _fullIndexBuffer?.Dispose();
                    _fullWaterVertexBuffer?.Dispose();
                    _fullWaterIndexBuffer?.Dispose();
                    _fullVertexBuffer = null;
                    _fullIndexBuffer = null;
                    _fullWaterVertexBuffer = null;
                    _fullWaterIndexBuffer = null;
                    _fullIndexCount = 0;
                    _fullWaterIndexCount = 0;
                    _fullWaterVertices = null;
                    _fullMeshBuilt = false;
                    break;
            }
        }

        public void InvalidateFloraMesh()
        {
            _floraVertices = null;
            _floraIndices = null;
            _floraIndexCount = 0;
            _floraMeshBuilt = false;
        }

        private void DisposeMeshBuffers()
        {
            _fullVertexBuffer?.Dispose();
            _fullIndexBuffer?.Dispose();
            _fullWaterVertexBuffer?.Dispose();
            _fullWaterIndexBuffer?.Dispose();
            _surfaceVertexBuffer?.Dispose();
            _surfaceIndexBuffer?.Dispose();
            _surfaceWaterVertexBuffer?.Dispose();
            _surfaceWaterIndexBuffer?.Dispose();
            _shellVertexBuffer?.Dispose();
            _shellIndexBuffer?.Dispose();

            _fullVertexBuffer = null;
            _fullIndexBuffer = null;
            _fullIndexCount = 0;
            _fullWaterVertexBuffer = null;
            _fullWaterIndexBuffer = null;
            _fullWaterIndexCount = 0;
            _fullWaterVertices = null;
            _surfaceVertexBuffer = null;
            _surfaceIndexBuffer = null;
            _surfaceIndexCount = 0;
            _surfaceWaterVertexBuffer = null;
            _surfaceWaterIndexBuffer = null;
            _surfaceWaterIndexCount = 0;
            _surfaceWaterVertices = null;
            _shellVertexBuffer = null;
            _shellIndexBuffer = null;
            _shellIndexCount = 0;
            _floraVertices = null;
            _floraIndices = null;
            _floraIndexCount = 0;
        }

        private List<Vertex>? _waterVertexScratch;
        private List<uint>? _waterIndexScratch;

        public (VertexBuffer? vertexBuffer, IndexBuffer? indexBuffer, int indexCount) GetMesh(ChunkMeshDetail detail)
        {
            return detail switch
            {
                ChunkMeshDetail.Surface => (_surfaceVertexBuffer, _surfaceIndexBuffer, _surfaceIndexCount),
                ChunkMeshDetail.Shell => (_shellVertexBuffer, _shellIndexBuffer, _shellIndexCount),
                _ => (_fullVertexBuffer, _fullIndexBuffer, _fullIndexCount)
            };
        }

        private void BuildMesh(GraphicsDevice device, MeshBuildContext context, ChunkMeshDetail detail,
            ref VertexBuffer? vertexBuffer, ref IndexBuffer? indexBuffer, ref int indexCount,
            ref VertexBuffer? waterVertexBuffer, ref IndexBuffer? waterIndexBuffer, ref int waterIndexCount,
            ref Vertex[]? waterVerticesCPU)
        {
            var data = BuildMeshCpuOnly(context, detail, buildFlora: false);
            UploadMeshBuffers(device, data.Vertices, data.Indices, ref vertexBuffer, ref indexBuffer, ref indexCount);
            UploadMeshBuffers(device, data.WaterVertices, data.WaterIndices, ref waterVertexBuffer, ref waterIndexBuffer, ref waterIndexCount);
            waterVerticesCPU = data.WaterVertices;
        }

        private void NoteBlockType(BlockType type)
        {
            if (type.IsWater())
            {
                _hasWaterBlocks = true;
            }

            if (type.IsAlphaCutout())
            {
                _hasAlphaCutoutBlocks = true;
            }
        }

        internal int GetMeshIndexCount(VoxelWorld world, ChunkMeshDetail detail)
        {
            if (!world.TryCreateMeshBuildContext(this, out var context))
            {
                return 0;
            }

            if (detail == ChunkMeshDetail.Shell)
            {
                var vertices = new List<Vertex>();
                var indices = new List<uint>();
                BuildShellMesh(context, ChunkX * Width, ChunkZ * Depth, vertices, indices);
                return indices.Count;
            }

            if (detail == ChunkMeshDetail.Surface)
            {
                var surfaceVertices = new List<Vertex>();
                var surfaceIndices = new List<uint>();
                var dummyWaterV = new List<Vertex>();
                var dummyWaterI = new List<uint>();
                BuildSurfaceMeshList(context, surfaceVertices, surfaceIndices, dummyWaterV, dummyWaterI);
                return surfaceIndices.Count + dummyWaterI.Count;
            }

            var fullVertices = new List<Vertex>();
            var fullIndices = new List<uint>();
            var dummyWaterV2 = new List<Vertex>();
            var dummyWaterI2 = new List<uint>();
            BuildFullMeshList(context, fullVertices, fullIndices, dummyWaterV2, dummyWaterI2);
            return fullIndices.Count + dummyWaterI2.Count;
        }

        private void BuildFullMeshList(MeshBuildContext context, List<Vertex> vertices, List<uint> indices, List<Vertex> waterVertices, List<uint> waterIndices)
        {
            int worldOffsetX = ChunkX * Width;
            int worldOffsetZ = ChunkZ * Depth;

            for (int y = 0; y < Height; y++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        BlockType type = _blocks[GetIndex(x, y, z)];
                        if (type == BlockType.Air) continue;
                        NoteBlockType(type);

                        EmitBlockFaces(
                            context,
                            vertices,
                            indices,
                            waterVertices,
                            waterIndices,
                            worldOffsetX + x,
                            y,
                            worldOffsetZ + z,
                            type,
                            includeAo: true,
                            shellTopOnly: false,
                            x,
                            z);
                    }
                }
            }
        }

        private void BuildSurfaceMeshList(MeshBuildContext context, List<Vertex> vertices, List<uint> indices, List<Vertex> waterVertices, List<uint> waterIndices)
        {
            int worldOffsetX = ChunkX * Width;
            int worldOffsetZ = ChunkZ * Depth;

            for (int z = 0; z < Depth; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int yMin = GetCachedLowestMeshY(x, z);
                    int yMax = GetCachedHighestMeshY(x, z);
                    if (yMin < 0 || yMax < 0)
                    {
                        continue;
                    }

                    for (int y = yMin; y <= yMax; y++)
                    {
                        BlockType type = _blocks[GetIndex(x, y, z)];
                        if (type == BlockType.Air)
                        {
                            continue;
                        }

                        NoteBlockType(type);

                        int wx = worldOffsetX + x;
                        int wy = y;
                        int wz = worldOffsetZ + z;

                        if (IsSurfaceBlock(context, wx, wy, wz))
                        {
                            EmitBlockFaces(context, vertices, indices, waterVertices, waterIndices, wx, wy, wz, type, includeAo: false, shellTopOnly: false, x, z);
                        }
                    }
                }
            }
        }

        private void BuildShellMesh(MeshBuildContext context, int worldOffsetX, int worldOffsetZ, List<Vertex> vertices, List<uint> indices)
        {
            for (int z = 0; z < Depth; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int topY = -1;
                    BlockType topType = BlockType.Air;

                    for (int y = Height - 1; y >= 0; y--)
                    {
                        BlockType type = _blocks[GetIndex(x, y, z)];
                        if (type == BlockType.Air)
                        {
                            continue;
                        }

                        int wx = worldOffsetX + x;
                        int wz = worldOffsetZ + z;
                        bool exposedTop = y == Height - 1 || context.GetBlock(wx, y + 1, wz).IsTransparent();
                        if (!exposedTop)
                        {
                            continue;
                        }

                        if (type.IsAlphaCutout())
                        {
                            NoteBlockType(type);
                            EmitBlockFaces(
                                context,
                                vertices,
                                indices,
                                vertices,
                                indices,
                                wx,
                                y,
                                wz,
                                type,
                                includeAo: false,
                                shellTopOnly: true,
                                x,
                                z);
                            continue;
                        }

                        if (type.IsSolidForSpawn() || type.IsWater())
                        {
                            topY = y;
                            topType = type;
                            break;
                        }
                    }

                    if (topY < 0) continue;

                    NoteBlockType(topType);
                    int shellWx = worldOffsetX + x;
                    int shellWy = topY;
                    int shellWz = worldOffsetZ + z;

                    EmitBlockFaces(context, vertices, indices, vertices, indices, shellWx, shellWy, shellWz, topType, includeAo: false, shellTopOnly: true, x, z);
                }
            }
        }

        private static bool IsSurfaceBlock(MeshBuildContext context, int wx, int wy, int wz)
        {
            if (wy < Chunk.Height - 1 && context.GetBlock(wx, wy + 1, wz).IsTransparent())
            {
                return true;
            }

            return context.GetBlock(wx + 1, wy, wz).IsTransparent()
                || context.GetBlock(wx - 1, wy, wz).IsTransparent()
                || context.GetBlock(wx, wy, wz + 1).IsTransparent()
                || context.GetBlock(wx, wy, wz - 1).IsTransparent();
        }

        private void EmitBlockFaces(MeshBuildContext context, List<Vertex> vertices, List<uint> indices, List<Vertex> waterVertices, List<uint> waterIndices,
            int wx, int wy, int wz, BlockType type, bool includeAo, bool shellTopOnly, int localX, int localZ)
        {
            if (type.IsFloraModel())
            {
                return;
            }

            var targetVertices = type.IsWater() ? waterVertices : vertices;
            var targetIndices = type.IsWater() ? waterIndices : indices;

            Vector3 pos = new Vector3(wx, wy, wz);
            Vector3 color = Vector3.One;

            if (wy == Height - 1 || context.GetBlock(wx, wy + 1, wz).IsTransparent())
            {
                AddFace(targetVertices, targetIndices, pos, new Vector3(0, 1, 0), color, type, context, includeAo,
                    new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0));
            }

            if (shellTopOnly)
            {
                return;
            }

            if (wy == 0 || context.GetBlock(wx, wy - 1, wz).IsTransparent())
            {
                AddFace(targetVertices, targetIndices, pos, new Vector3(0, -1, 0), color, type, context, includeAo,
                    new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1));
            }

            if (context.GetBlock(wx + 1, wy, wz).IsTransparent())
            {
                AddFace(targetVertices, targetIndices, pos, new Vector3(1, 0, 0), color, type, context, includeAo,
                    new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1));
            }

            if (context.GetBlock(wx - 1, wy, wz).IsTransparent())
            {
                AddFace(targetVertices, targetIndices, pos, new Vector3(-1, 0, 0), color, type, context, includeAo,
                    new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0));
            }

            if (context.GetBlock(wx, wy, wz + 1).IsTransparent())
            {
                AddFace(targetVertices, targetIndices, pos, new Vector3(0, 0, 1), color, type, context, includeAo,
                    new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1));
            }

            if (context.GetBlock(wx, wy, wz - 1).IsTransparent())
            {
                AddFace(targetVertices, targetIndices, pos, new Vector3(0, 0, -1), color, type, context, includeAo,
                    new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0));
            }
        }

        private Vector3 GetWaterVertexColor(MeshBuildContext context, int wx, int wy, int wz, Vector3 cornerOffset)
        {
            int depth = 0;
            while (wy - depth > 0 && context.GetBlock(wx, wy - depth - 1, wz).IsWater())
            {
                depth++;
            }

            Vector3 waterColor;
            if (depth <= 0)
            {
                waterColor = new Vector3(0.5f, 0.85f, 0.95f);
            }
            else if (depth == 1)
            {
                waterColor = new Vector3(0.35f, 0.65f, 0.85f);
            }
            else if (depth == 2)
            {
                waterColor = new Vector3(0.2f, 0.45f, 0.75f);
            }
            else
            {
                waterColor = new Vector3(0.1f, 0.25f, 0.6f);
            }

            bool isShore = false;
            int dx = cornerOffset.X > 0.5f ? 1 : -1;
            int dz = cornerOffset.Z > 0.5f ? 1 : -1;

            var blockX = context.GetBlock(wx + dx, wy, wz);
            var blockZ = context.GetBlock(wx, wy, wz + dz);
            var blockXZ = context.GetBlock(wx + dx, wy, wz + dz);

            if ((blockX != BlockType.Air && !blockX.IsWater()) ||
                (blockZ != BlockType.Air && !blockZ.IsWater()) ||
                (blockXZ != BlockType.Air && !blockXZ.IsWater()))
            {
                isShore = true;
            }

            if (isShore)
            {
                return Vector3.Lerp(waterColor, new Vector3(1.1f, 1.1f, 1.1f), 0.75f);
            }

            return waterColor;
        }

        private void AddFace(List<Vertex> vertices, List<uint> indices, Vector3 pos, Vector3 normal, Vector3 baseColor, BlockType blockType,
                             MeshBuildContext context, bool includeAo, Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3)
        {
            uint startIndex = (uint)vertices.Count;

            var uv = BlockAtlas.GetFaceUVs(blockType, normal);
            int wx = (int)pos.X;
            int wy = (int)pos.Y;
            int wz = (int)pos.Z;

            float smoothedVariation = BlockAtlas.UseCpuBlockVariation
                ? BlockTextureBlend.GetSmoothedVariation(context, wx, wy, wz)
                : 1f;

            float ao0 = includeAo ? ComputeCornerAO(context, wx, wy, wz, new Vector3(wx, wy, wz) + c0, normal) : ComputeColumnAO(context, wx, wy, wz, normal);
            float ao1 = includeAo ? ComputeCornerAO(context, wx, wy, wz, new Vector3(wx, wy, wz) + c1, normal) : ComputeColumnAO(context, wx, wy, wz, normal);
            float ao2 = includeAo ? ComputeCornerAO(context, wx, wy, wz, new Vector3(wx, wy, wz) + c2, normal) : ComputeColumnAO(context, wx, wy, wz, normal);
            float ao3 = includeAo ? ComputeCornerAO(context, wx, wy, wz, new Vector3(wx, wy, wz) + c3, normal) : ComputeColumnAO(context, wx, wy, wz, normal);

            Vector3 col0 = blockType.IsWater() ? GetWaterVertexColor(context, wx, wy, wz, c0) : BuildCornerColor(context, wx, wy, wz, blockType, c0, normal, ao0, smoothedVariation);
            Vector3 col1 = blockType.IsWater() ? GetWaterVertexColor(context, wx, wy, wz, c1) : BuildCornerColor(context, wx, wy, wz, blockType, c1, normal, ao1, smoothedVariation);
            Vector3 col2 = blockType.IsWater() ? GetWaterVertexColor(context, wx, wy, wz, c2) : BuildCornerColor(context, wx, wy, wz, blockType, c2, normal, ao2, smoothedVariation);
            Vector3 col3 = blockType.IsWater() ? GetWaterVertexColor(context, wx, wy, wz, c3) : BuildCornerColor(context, wx, wy, wz, blockType, c3, normal, ao3, smoothedVariation);

            Vector2 uv0 = BlockTextureBlend.ComputeWorldAlignedUv(pos, c0, normal, uv.uMin, uv.vMin, uv.uMax, uv.vMax);
            Vector2 uv1 = BlockTextureBlend.ComputeWorldAlignedUv(pos, c1, normal, uv.uMin, uv.vMin, uv.uMax, uv.vMax);
            Vector2 uv2 = BlockTextureBlend.ComputeWorldAlignedUv(pos, c2, normal, uv.uMin, uv.vMin, uv.uMax, uv.vMax);
            Vector2 uv3 = BlockTextureBlend.ComputeWorldAlignedUv(pos, c3, normal, uv.uMin, uv.vMin, uv.uMax, uv.vMax);

            Vector2 uvr0 = uv0;
            Vector2 uvr1 = uv1;
            Vector2 uvr2 = uv2;
            Vector2 uvr3 = uv3;

            bool isDirectional = blockType == BlockType.OakLog || blockType == BlockType.BirchLog ||
                                 blockType == BlockType.PineLog || blockType == BlockType.WillowLog ||
                                 blockType == BlockType.PalmLog || blockType == BlockType.OakPlank ||
                                 blockType == BlockType.BirchPlank || blockType == BlockType.PinePlank;

            bool isGrassSide = blockType == BlockType.Grass && MathF.Abs(normal.Y) < 0.1f;

            if (!blockType.IsTransparent() && !blockType.IsStation() && !isDirectional && !isGrassSide)
            {
                int hash = (int)(wx * 73 + wy * 37 + wz * 19 + normal.X * 13 + normal.Y * 7 + normal.Z * 3);
                int rotation = Math.Abs(hash) % 4;
                switch (rotation)
                {
                    case 1:
                        uvr0 = uv1; uvr1 = uv2; uvr2 = uv3; uvr3 = uv0;
                        break;
                    case 2:
                        uvr0 = uv2; uvr1 = uv3; uvr2 = uv0; uvr3 = uv1;
                        break;
                    case 3:
                        uvr0 = uv3; uvr1 = uv0; uvr2 = uv1; uvr3 = uv2;
                        break;
                }
            }

            vertices.Add(new Vertex(pos + c0, col0, normal, uvr0));
            vertices.Add(new Vertex(pos + c1, col1, normal, uvr1));
            vertices.Add(new Vertex(pos + c2, col2, normal, uvr2));
            vertices.Add(new Vertex(pos + c3, col3, normal, uvr3));

            // Voxel Ambient Occlusion Triangulation Flip Fix
            // If the diagonal 0-2 connects more occluded corners than 1-3, flip the diagonal.
            if (ao0 + ao2 < ao1 + ao3)
            {
                indices.Add(startIndex + 0);
                indices.Add(startIndex + 1);
                indices.Add(startIndex + 3);

                indices.Add(startIndex + 1);
                indices.Add(startIndex + 2);
                indices.Add(startIndex + 3);
            }
            else
            {
                indices.Add(startIndex + 0);
                indices.Add(startIndex + 1);
                indices.Add(startIndex + 2);

                indices.Add(startIndex + 0);
                indices.Add(startIndex + 2);
                indices.Add(startIndex + 3);
            }
        }

        private static Vector3 BuildCornerColor(
            MeshBuildContext context,
            int wx,
            int wy,
            int wz,
            BlockType blockType,
            Vector3 cornerOffset,
            Vector3 normal,
            float ao,
            float smoothedVariation)
        {
            int cx = cornerOffset.X > 0.5f ? 1 : 0;
            int cy = cornerOffset.Y > 0.5f ? 1 : 0;
            int cz = cornerOffset.Z > 0.5f ? 1 : 0;
            float variation = BlockAtlas.UseCpuBlockVariation ? smoothedVariation : 1f;

            return BlockTextureBlend.ComputeCornerColor(
                context,
                wx,
                wy,
                wz,
                blockType,
                cx,
                cy,
                cz,
                normal,
                variation,
                ao);
        }

        private static Vector3 FloorVec3(Vector3 v)
        {
            return new Vector3(MathF.Floor(v.X), MathF.Floor(v.Y), MathF.Floor(v.Z));
        }

        private static float ComputeColumnAO(MeshBuildContext context, int wx, int wy, int wz, Vector3 normal)
        {
            if (normal.Y > 0.5f)
            {
                BlockType above = context.GetBlock(wx, wy + 1, wz);
                return above.IsTransparent() ? 1f : 0.86f;
            }

            if (normal.Y < -0.5f)
            {
                return 0.82f;
            }

            int occ = 0;
            if (!context.GetBlock(wx + 1, wy, wz).IsTransparent()) occ++;
            if (!context.GetBlock(wx - 1, wy, wz).IsTransparent()) occ++;
            if (!context.GetBlock(wx, wy, wz + 1).IsTransparent()) occ++;
            if (!context.GetBlock(wx, wy, wz - 1).IsTransparent()) occ++;
            if (!context.GetBlock(wx, wy + 1, wz).IsTransparent()) occ++;
            return 1f - occ * 0.06f;
        }

        private static float ComputeCornerAO(
            MeshBuildContext context,
            int bx,
            int by,
            int bz,
            Vector3 vertexPos,
            Vector3 normal)
        {
            Vector3 up = Math.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 tang1 = Vector3.Normalize(Vector3.Cross(normal, up));
            Vector3 tang2 = Vector3.Cross(normal, tang1);

            Vector3 vpFloor = FloorVec3(vertexPos + new Vector3(0.001f));
            float t1 = Vector3.Dot(vertexPos - vpFloor, tang1) > 0 ? 1 : -1;
            float t2 = Vector3.Dot(vertexPos - vpFloor, tang2) > 0 ? 1 : -1;

            Vector3 blockCenter = new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f);

            Vector3 s1pos = blockCenter + tang1 * t1;
            Vector3 s2pos = blockCenter + tang2 * t2;
            Vector3 copos = blockCenter + tang1 * t1 + tang2 * t2;

            bool s1 = SampleAoOcclusion(context, s1pos, normal, by);
            bool s2 = SampleAoOcclusion(context, s2pos, normal, by);
            bool co = SampleAoOcclusion(context, copos, normal, by);

            if (normal.Y > 0.5f)
            {
                if (s1 && s2) return 0.82f;
                int topOcc = (s1 ? 1 : 0) + (s2 ? 1 : 0) + (co ? 1 : 0);
                return MathF.Max(0.86f, 1.0f - topOcc * 0.06f);
            }

            if (s1 && s2) return 0.58f;
            int occ = (s1 ? 1 : 0) + (s2 ? 1 : 0) + (co ? 1 : 0);
            return 1.0f - occ * 0.14f;
        }

        private static bool SampleAoOcclusion(MeshBuildContext context, Vector3 pos, Vector3 normal, int blockY)
        {
            int x = (int)MathF.Floor(pos.X);
            int y = (int)MathF.Floor(pos.Y);
            int z = (int)MathF.Floor(pos.Z);

            if (normal.Y > 0.5f)
            {
                // Top faces: only overhangs above the sampled column cast corner shade.
                // Flush same-height neighbors must not darken coplanar terrain.
                return context.GetBlock(x, blockY + 1, z).IsSolidForSpawn();
            }

            if (context.GetBlock(x, y, z).IsSolidForSpawn())
            {
                return true;
            }

            if (normal.Y < -0.5f)
            {
                return context.GetBlock(x, y - 1, z).IsSolidForSpawn();
            }

            return context.GetBlock(x, y - 1, z).IsSolidForSpawn()
                || context.GetBlock(x, y + 1, z).IsSolidForSpawn();
        }

        private static void UploadMeshBuffers(GraphicsDevice device, Vertex[] vertices, uint[] indices,
            ref VertexBuffer? vertexBuffer, ref IndexBuffer? indexBuffer, ref int indexCount)
        {
            if (vertices.Length == 0 || indices.Length == 0)
            {
                vertexBuffer?.Dispose();
                indexBuffer?.Dispose();
                vertexBuffer = null;
                indexBuffer = null;
                indexCount = 0;
                return;
            }

            if (vertexBuffer == null || vertexBuffer.VertexCount < vertices.Length)
            {
                vertexBuffer?.Dispose();
                vertexBuffer = new VertexBuffer(device, typeof(Vertex), vertices.Length, BufferUsage.WriteOnly);
            }
            vertexBuffer.SetData(vertices);

            if (indexBuffer == null || indexBuffer.IndexCount < indices.Length)
            {
                indexBuffer?.Dispose();
                indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            }
            indexBuffer.SetData(indices);
            indexCount = indices.Length;
        }

        public void Dispose()
        {
            DisposeMeshBuffers();
            _fullMeshBuilt = false;
            _surfaceMeshBuilt = false;
            _shellMeshBuilt = false;
            _floraMeshBuilt = false;
        }
    }
}
