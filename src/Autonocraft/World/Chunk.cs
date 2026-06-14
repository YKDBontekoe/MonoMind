using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
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
        // Cached highest solid Y per column (lx + lz * Width). -1 = no solid block.
        private readonly short[] _columnHighestSolid = new short[Width * Depth];
        private bool _columnHeightsBuilt;

        private VertexBuffer? _fullVertexBuffer;
        private IndexBuffer? _fullIndexBuffer;
        private int _fullIndexCount;

        private VertexBuffer? _surfaceVertexBuffer;
        private IndexBuffer? _surfaceIndexBuffer;
        private int _surfaceIndexCount;

        private VertexBuffer? _shellVertexBuffer;
        private IndexBuffer? _shellIndexBuffer;
        private int _shellIndexCount;

        private VertexBuffer? _floraVertexBuffer;
        private IndexBuffer? _floraIndexBuffer;
        private int _floraIndexCount;

        private bool _fullMeshBuilt;
        private bool _surfaceMeshBuilt;
        private bool _shellMeshBuilt;
        private bool _floraMeshBuilt;
        // Written from background thread: volatile prevents stale reads.
        private volatile bool _hasWaterBlocks;

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
            public readonly ChunkMeshDetail Detail;
            public readonly bool BuildFlora;
            public readonly Vertex[]? FloraVertices;
            public readonly uint[]? FloraIndices;

            public PrebuiltMeshData(Vertex[] v, uint[] i, ChunkMeshDetail detail, bool buildFlora,
                Vertex[]? fv = null, uint[]? fi = null)
            {
                Vertices = v; Indices = i; Detail = detail; BuildFlora = buildFlora;
                FloraVertices = fv; FloraIndices = fi;
            }
        }

        public bool HasWaterBlocks => _hasWaterBlocks;

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
        }

        /// <summary>Rebuilds per-column height cache after terrain generation or bulk edits.</summary>
        internal void RebuildColumnHeights()
        {
            for (int lz = 0; lz < Depth; lz++)
            {
                for (int lx = 0; lx < Width; lx++)
                {
                    int best = -1;
                    for (int y = Height - 1; y >= 0; y--)
                    {
                        if (_blocks[GetIndex(lx, y, lz)].IsSolidForSpawn())
                        {
                            best = y;
                            break;
                        }
                    }

                    _columnHighestSolid[lz * Width + lx] = (short)best;
                }
            }

            _columnHeightsBuilt = true;
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

        private void UpdateColumnHeightCache(int lx, int y, int lz, BlockType type)
        {
            int idx = lz * Width + lx;
            int current = _columnHighestSolid[idx];
            bool solid = type.IsSolidForSpawn();

            if (solid && y >= current)
            {
                _columnHighestSolid[idx] = (short)y;
                return;
            }

            if (!solid && y == current)
            {
                for (int scanY = y - 1; scanY >= 0; scanY--)
                {
                    if (_blocks[GetIndex(lx, scanY, lz)].IsSolidForSpawn())
                    {
                        _columnHighestSolid[idx] = (short)scanY;
                        return;
                    }
                }

                _columnHighestSolid[idx] = -1;
            }
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

        public (VertexBuffer? vertexBuffer, IndexBuffer? indexBuffer, int indexCount) GetFloraMesh()
        {
            return (_floraVertexBuffer, _floraIndexBuffer, _floraIndexCount);
        }

        public void EnsureFloraMesh(GraphicsDevice device)
        {
            if (_floraMeshBuilt)
            {
                return;
            }

            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            FloraMeshBuilder.Build(this, vertices, indices);

            _floraIndexCount = indices.Count;
            if (_floraIndexCount > 0)
            {
                _floraVertexBuffer = new VertexBuffer(device, Vertex.VertexDeclaration, vertices.Count, BufferUsage.WriteOnly);
                _floraVertexBuffer.SetData(vertices.ToArray());
                _floraIndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
                _floraIndexBuffer.SetData(indices.ToArray());
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
            switch (detail)
            {
                case ChunkMeshDetail.Surface:
                    if (_surfaceMeshBuilt && !MeshStale) return;
                    BuildMesh(device, context, ChunkMeshDetail.Surface, ref _surfaceVertexBuffer, ref _surfaceIndexBuffer, ref _surfaceIndexCount);
                    _surfaceMeshBuilt = true;
                    break;
                case ChunkMeshDetail.Shell:
                    if (_shellMeshBuilt && !MeshStale) return;
                    BuildMesh(device, context, ChunkMeshDetail.Shell, ref _shellVertexBuffer, ref _shellIndexBuffer, ref _shellIndexCount);
                    _shellMeshBuilt = true;
                    break;
                default:
                    if (_fullMeshBuilt && !MeshStale) return;
                    BuildMesh(device, context, ChunkMeshDetail.Full, ref _fullVertexBuffer, ref _fullIndexBuffer, ref _fullIndexCount);
                    _fullMeshBuilt = true;
                    break;
            }

            MeshStale = false;

            if (buildFlora)
            {
                EnsureFloraMesh(device);
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

            if (detail == ChunkMeshDetail.Shell)
                BuildShellMesh(context, ChunkX * Width, ChunkZ * Depth, vertices, indices);
            else if (detail == ChunkMeshDetail.Surface)
                BuildSurfaceMeshList(context, vertices, indices);
            else
                BuildFullMeshList(context, vertices, indices);

            Vertex[]? fv = null;
            uint[]? fi = null;
            if (buildFlora && !_floraMeshBuilt)
            {
                var fvList = new List<Vertex>(1024);
                var fiList = new List<uint>(1536);
                FloraMeshBuilder.Build(this, fvList, fiList);
                if (fvList.Count > 0) { fv = fvList.ToArray(); fi = fiList.ToArray(); }
            }

            return new PrebuiltMeshData(vertices.ToArray(), indices.ToArray(), detail, buildFlora, fv, fi);
        }

        /// <summary>
        /// Main-thread GPU upload of mesh data pre-built by <see cref="BuildMeshCpuOnly"/>.
        /// Also clears the corresponding in-flight flag.
        /// </summary>
        internal void ApplyPrebuiltMesh(GraphicsDevice device, PrebuiltMeshData data)
        {
            bool wasStale = MeshStale;

            switch (data.Detail)
            {
                case ChunkMeshDetail.Surface:
                    if (!_surfaceMeshBuilt || wasStale)
                    {
                        UploadMeshBuffers(device, data.Vertices, data.Indices,
                            ref _surfaceVertexBuffer, ref _surfaceIndexBuffer, ref _surfaceIndexCount);
                        _surfaceMeshBuilt = true;
                    }
                    SurfaceMeshBuildInFlight = false;
                    break;
                case ChunkMeshDetail.Shell:
                    if (!_shellMeshBuilt || wasStale)
                    {
                        UploadMeshBuffers(device, data.Vertices, data.Indices,
                            ref _shellVertexBuffer, ref _shellIndexBuffer, ref _shellIndexCount);
                        _shellMeshBuilt = true;
                    }
                    ShellMeshBuildInFlight = false;
                    break;
                default:
                    if (!_fullMeshBuilt || wasStale)
                    {
                        UploadMeshBuffers(device, data.Vertices, data.Indices,
                            ref _fullVertexBuffer, ref _fullIndexBuffer, ref _fullIndexCount);
                        _fullMeshBuilt = true;
                    }
                    FullMeshBuildInFlight = false;
                    break;
            }

            MeshStale = false;

            if (data.BuildFlora && data.FloraVertices != null && data.FloraIndices != null
                && (!_floraMeshBuilt || wasStale))
            {
                UploadMeshBuffers(device, data.FloraVertices, data.FloraIndices,
                    ref _floraVertexBuffer, ref _floraIndexBuffer, ref _floraIndexCount);
                _floraMeshBuilt = true;
            }
        }

        /// <summary>CPU-only mesh build for profiling without a graphics device.</summary>
        internal float BenchmarkBuildMeshCpu(MeshBuildContext context, ChunkMeshDetail detail)
        {
            _vertexScratch ??= new List<Vertex>(8192);
            _indexScratch ??= new List<uint>(12288);
            _vertexScratch.Clear();
            _indexScratch.Clear();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (detail == ChunkMeshDetail.Shell)
            {
                BuildShellMesh(context, ChunkX * Width, ChunkZ * Depth, _vertexScratch, _indexScratch);
            }
            else if (detail == ChunkMeshDetail.Surface)
            {
                BuildSurfaceMeshList(context, _vertexScratch, _indexScratch);
            }
            else
            {
                BuildFullMeshList(context, _vertexScratch, _indexScratch);
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
                    _surfaceVertexBuffer = null;
                    _surfaceIndexBuffer = null;
                    _surfaceIndexCount = 0;
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
                    _fullVertexBuffer = null;
                    _fullIndexBuffer = null;
                    _fullIndexCount = 0;
                    _fullMeshBuilt = false;
                    break;
            }
        }

        public void InvalidateFloraMesh()
        {
            _floraVertexBuffer?.Dispose();
            _floraIndexBuffer?.Dispose();
            _floraVertexBuffer = null;
            _floraIndexBuffer = null;
            _floraIndexCount = 0;
            _floraMeshBuilt = false;
        }

        private void DisposeMeshBuffers()
        {
            _fullVertexBuffer?.Dispose();
            _fullIndexBuffer?.Dispose();
            _surfaceVertexBuffer?.Dispose();
            _surfaceIndexBuffer?.Dispose();
            _shellVertexBuffer?.Dispose();
            _shellIndexBuffer?.Dispose();
            _floraVertexBuffer?.Dispose();
            _floraIndexBuffer?.Dispose();

            _fullVertexBuffer = null;
            _fullIndexBuffer = null;
            _fullIndexCount = 0;
            _surfaceVertexBuffer = null;
            _surfaceIndexBuffer = null;
            _surfaceIndexCount = 0;
            _shellVertexBuffer = null;
            _shellIndexBuffer = null;
            _shellIndexCount = 0;
            _floraVertexBuffer = null;
            _floraIndexBuffer = null;
            _floraIndexCount = 0;
        }

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
            ref VertexBuffer? vertexBuffer, ref IndexBuffer? indexBuffer, ref int indexCount)
        {
            _vertexScratch ??= new List<Vertex>(8192);
            _indexScratch ??= new List<uint>(12288);
            _vertexScratch.Clear();
            _indexScratch.Clear();

            if (detail == ChunkMeshDetail.Shell)
            {
                BuildShellMesh(context, ChunkX * Width, ChunkZ * Depth, _vertexScratch, _indexScratch);
            }
            else if (detail == ChunkMeshDetail.Surface)
            {
                BuildSurfaceMeshList(context, _vertexScratch, _indexScratch);
            }
            else
            {
                BuildFullMeshList(context, _vertexScratch, _indexScratch);
            }

            UploadMeshBuffers(device, _vertexScratch.ToArray(), _indexScratch.ToArray(), ref vertexBuffer, ref indexBuffer, ref indexCount);
        }

        private void NoteBlockType(BlockType type)
        {
            if (type.IsWater())
            {
                _hasWaterBlocks = true;
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
                BuildSurfaceMeshList(context, surfaceVertices, surfaceIndices);
                return surfaceIndices.Count;
            }

            var fullVertices = new List<Vertex>();
            var fullIndices = new List<uint>();
            BuildFullMeshList(context, fullVertices, fullIndices);
            return fullIndices.Count;
        }

        private void BuildFullMeshList(MeshBuildContext context, List<Vertex> vertices, List<uint> indices)
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
                            worldOffsetX + x,
                            y,
                            worldOffsetZ + z,
                            type,
                            includeAo: true,
                            shellBoundaryOnly: false,
                            x,
                            z);
                    }
                }
            }
        }

        private void BuildSurfaceMeshList(MeshBuildContext context, List<Vertex> vertices, List<uint> indices)
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

                        int wx = worldOffsetX + x;
                        int wy = y;
                        int wz = worldOffsetZ + z;

                        if (IsSurfaceBlock(context, wx, wy, wz))
                        {
                            EmitBlockFaces(context, vertices, indices, wx, wy, wz, type, includeAo: false, shellBoundaryOnly: false, x, z);
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
                        if (type.IsSolidForSpawn())
                        {
                            topY = y;
                            topType = type;
                            break;
                        }
                    }

                    if (topY < 0)
                    {
                        for (int y = Height - 1; y >= 0; y--)
                        {
                            BlockType type = _blocks[GetIndex(x, y, z)];
                            if (!type.IsWater())
                            {
                                continue;
                            }

                            int wx = worldOffsetX + x;
                            int wz = worldOffsetZ + z;
                            if (y == Height - 1 || context.GetBlock(wx, y + 1, wz).IsTransparent())
                            {
                                topY = y;
                                topType = type;
                                break;
                            }
                        }
                    }

                    if (topY < 0) continue;

                    NoteBlockType(topType);
                    int shellWx = worldOffsetX + x;
                    int shellWy = topY;
                    int shellWz = worldOffsetZ + z;

                    EmitBlockFaces(context, vertices, indices, shellWx, shellWy, shellWz, topType, includeAo: false, shellBoundaryOnly: true, x, z);
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

        private void EmitBlockFaces(MeshBuildContext context, List<Vertex> vertices, List<uint> indices,
            int wx, int wy, int wz, BlockType type, bool includeAo, bool shellBoundaryOnly, int localX, int localZ)
        {
            if (type.IsFloraModel())
            {
                return;
            }

            Vector3 pos = new Vector3(wx, wy, wz);
            Vector3 color = Vector3.One;

            if (wy == Height - 1 || context.GetBlock(wx, wy + 1, wz).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(0, 1, 0), color, type, context, includeAo,
                    new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0));
            }

            if (!shellBoundaryOnly)
            {
                if (wy == 0 || context.GetBlock(wx, wy - 1, wz).IsTransparent())
                {
                    AddFace(vertices, indices, pos, new Vector3(0, -1, 0), color, type, context, includeAo,
                        new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1));
                }
            }

            if ((!shellBoundaryOnly || localX == Width - 1) && context.GetBlock(wx + 1, wy, wz).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(1, 0, 0), color, type, context, includeAo,
                    new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1));
            }

            if ((!shellBoundaryOnly || localX == 0) && context.GetBlock(wx - 1, wy, wz).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(-1, 0, 0), color, type, context, includeAo,
                    new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0));
            }

            if ((!shellBoundaryOnly || localZ == Depth - 1) && context.GetBlock(wx, wy, wz + 1).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(0, 0, 1), color, type, context, includeAo,
                    new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1));
            }

            if ((!shellBoundaryOnly || localZ == 0) && context.GetBlock(wx, wy, wz - 1).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(0, 0, -1), color, type, context, includeAo,
                    new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0));
            }
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

            Vector3 col0 = BuildCornerColor(context, wx, wy, wz, blockType, c0, normal, includeAo, smoothedVariation);
            Vector3 col1 = BuildCornerColor(context, wx, wy, wz, blockType, c1, normal, includeAo, smoothedVariation);
            Vector3 col2 = BuildCornerColor(context, wx, wy, wz, blockType, c2, normal, includeAo, smoothedVariation);
            Vector3 col3 = BuildCornerColor(context, wx, wy, wz, blockType, c3, normal, includeAo, smoothedVariation);

            Vector2 uv0 = BlockTextureBlend.ComputeWorldAlignedUv(pos, c0, normal, uv.uMin, uv.vMin, uv.uMax, uv.vMax);
            Vector2 uv1 = BlockTextureBlend.ComputeWorldAlignedUv(pos, c1, normal, uv.uMin, uv.vMin, uv.uMax, uv.vMax);
            Vector2 uv2 = BlockTextureBlend.ComputeWorldAlignedUv(pos, c2, normal, uv.uMin, uv.vMin, uv.uMax, uv.vMax);
            Vector2 uv3 = BlockTextureBlend.ComputeWorldAlignedUv(pos, c3, normal, uv.uMin, uv.vMin, uv.uMax, uv.vMax);

            vertices.Add(new Vertex(pos + c0, col0, normal, uv0));
            vertices.Add(new Vertex(pos + c1, col1, normal, uv1));
            vertices.Add(new Vertex(pos + c2, col2, normal, uv2));
            vertices.Add(new Vertex(pos + c3, col3, normal, uv3));

            indices.Add(startIndex + 0);
            indices.Add(startIndex + 1);
            indices.Add(startIndex + 2);

            indices.Add(startIndex + 0);
            indices.Add(startIndex + 2);
            indices.Add(startIndex + 3);
        }

        private static Vector3 BuildCornerColor(
            MeshBuildContext context,
            int wx,
            int wy,
            int wz,
            BlockType blockType,
            Vector3 cornerOffset,
            Vector3 normal,
            bool includeAo,
            float smoothedVariation)
        {
            int cx = cornerOffset.X > 0.5f ? 1 : 0;
            int cy = cornerOffset.Y > 0.5f ? 1 : 0;
            int cz = cornerOffset.Z > 0.5f ? 1 : 0;
            float ao = includeAo
                ? ComputeCornerAO(context, new Vector3(wx, wy, wz) + cornerOffset, normal)
                : ComputeColumnAO(context, wx, wy, wz, normal);
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

        private static float ComputeCornerAO(MeshBuildContext context, Vector3 vertexPos, Vector3 normal)
        {
            Vector3 up = Math.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 tang1 = Vector3.Normalize(Vector3.Cross(normal, up));
            Vector3 tang2 = Vector3.Cross(normal, tang1);

            Vector3 vpFloor = FloorVec3(vertexPos + new Vector3(0.001f));
            float t1 = Vector3.Dot(vertexPos - vpFloor, tang1) > 0 ? 1 : -1;
            float t2 = Vector3.Dot(vertexPos - vpFloor, tang2) > 0 ? 1 : -1;

            Vector3 blockCenter = FloorVec3(vertexPos - normal * 0.5f) + new Vector3(0.5f);

            Vector3 s1pos = blockCenter + tang1 * t1;
            Vector3 s2pos = blockCenter + tang2 * t2;
            Vector3 copos = blockCenter + tang1 * t1 + tang2 * t2;

            bool s1 = context.GetBlock((int)Math.Floor(s1pos.X), (int)Math.Floor(s1pos.Y), (int)Math.Floor(s1pos.Z)).IsSolidForSpawn();
            bool s2 = context.GetBlock((int)Math.Floor(s2pos.X), (int)Math.Floor(s2pos.Y), (int)Math.Floor(s2pos.Z)).IsSolidForSpawn();
            bool co = context.GetBlock((int)Math.Floor(copos.X), (int)Math.Floor(copos.Y), (int)Math.Floor(copos.Z)).IsSolidForSpawn();

            if (s1 && s2) return 0.58f;
            int occ = (s1 ? 1 : 0) + (s2 ? 1 : 0) + (co ? 1 : 0);
            return 1.0f - occ * 0.14f;
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
