using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Domain.World;

namespace Autonocraft.World
{
    // Vertex/index GPU buffers, upload, and invalidation.
    public partial class Chunk
    {
        private VertexBuffer? _fullVertexBuffer;
        private IndexBuffer? _fullIndexBuffer;
        private int _fullIndexCount;

        private VertexBuffer? _fullWaterVertexBuffer;
        private IndexBuffer? _fullWaterIndexBuffer;
        private int _fullWaterIndexCount;

        private VertexBuffer? _surfaceVertexBuffer;
        private IndexBuffer? _surfaceIndexBuffer;
        private int _surfaceIndexCount;

        private VertexBuffer? _surfaceWaterVertexBuffer;
        private IndexBuffer? _surfaceWaterIndexBuffer;
        private int _surfaceWaterIndexCount;

        private VertexBuffer? _shellVertexBuffer;
        private IndexBuffer? _shellIndexBuffer;
        private int _shellIndexCount;

        private VertexBuffer? _shellWaterVertexBuffer;
        private IndexBuffer? _shellWaterIndexBuffer;
        private int _shellWaterIndexCount;

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
        /// <summary>
        /// Holds vertex/index arrays computed on a background thread, ready for GPU upload.
        /// Backing arrays for solid and water geometry are rented from ArrayPool and must be returned exactly once.
        /// </summary>
        internal sealed class PrebuiltMeshData
        {
            public readonly Vertex[] Vertices;
            public readonly int VertexCount;
            public readonly uint[] Indices;
            public readonly int IndexCount;
            public readonly Vertex[] WaterVertices;
            public readonly int WaterVertexCount;
            public readonly uint[] WaterIndices;
            public readonly int WaterIndexCount;
            public readonly ChunkMeshDetail Detail;
            public readonly bool BuildFlora;
            public readonly FloraVertex[]? FloraVertices;
            public readonly uint[]? FloraIndices;

            public PrebuiltMeshData(
                Vertex[] vertices,
                int vertexCount,
                uint[] indices,
                int indexCount,
                Vertex[] waterVertices,
                int waterVertexCount,
                uint[] waterIndices,
                int waterIndexCount,
                ChunkMeshDetail detail,
                bool buildFlora,
                FloraVertex[]? floraVertices = null,
                uint[]? floraIndices = null)
            {
                Vertices = vertices;
                VertexCount = vertexCount;
                Indices = indices;
                IndexCount = indexCount;
                WaterVertices = waterVertices;
                WaterVertexCount = waterVertexCount;
                WaterIndices = waterIndices;
                WaterIndexCount = waterIndexCount;
                Detail = detail;
                BuildFlora = buildFlora;
                FloraVertices = floraVertices;
                FloraIndices = floraIndices;
            }

            public void ReturnToPools()
            {
                if (VertexCount > 0 && Vertices.Length > 0)
                {
                    ArrayPool<Vertex>.Shared.Return(Vertices, clearArray: false);
                }

                if (IndexCount > 0 && Indices.Length > 0)
                {
                    ArrayPool<uint>.Shared.Return(Indices, clearArray: false);
                }

                if (WaterVertexCount > 0 && WaterVertices.Length > 0)
                {
                    ArrayPool<Vertex>.Shared.Return(WaterVertices, clearArray: false);
                }

                if (WaterIndexCount > 0 && WaterIndices.Length > 0)
                {
                    ArrayPool<uint>.Shared.Return(WaterIndices, clearArray: false);
                }
            }
        }

        public bool HasWaterBlocks => _hasWaterBlocks;
        public bool HasAlphaCutoutBlocks => _hasAlphaCutoutBlocks;

        public (VertexBuffer? vertexBuffer, IndexBuffer? indexBuffer, int indexCount) GetWaterMesh(ChunkMeshDetail detail)
        {
            return detail switch
            {
                ChunkMeshDetail.Surface => (_surfaceWaterVertexBuffer, _surfaceWaterIndexBuffer, _surfaceWaterIndexCount),
                ChunkMeshDetail.Shell => (_shellWaterVertexBuffer, _shellWaterIndexBuffer, _shellWaterIndexCount),
                _ => (_fullWaterVertexBuffer, _fullWaterIndexBuffer, _fullWaterIndexCount)
            };
        }

        public VertexBuffer? VertexBuffer => _fullVertexBuffer;
        public IndexBuffer? IndexBuffer => _fullIndexBuffer;
        public int IndexCount => _fullIndexCount;

        public int FullIndexCount => _fullIndexCount;
        public int SurfaceIndexCount => _surfaceIndexCount;
        public int ShellIndexCount => _shellIndexCount;
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
            switch (detail)
            {
                case ChunkMeshDetail.Surface:
                    if (_surfaceMeshBuilt && !MeshStale) return;
                    BuildMesh(device, context, ChunkMeshDetail.Surface, ref _surfaceVertexBuffer, ref _surfaceIndexBuffer, ref _surfaceIndexCount,
                        ref _surfaceWaterVertexBuffer, ref _surfaceWaterIndexBuffer, ref _surfaceWaterIndexCount);
                    _surfaceMeshBuilt = true;
                    break;
                case ChunkMeshDetail.Shell:
                    if (_shellMeshBuilt && !MeshStale) return;
                    BuildMesh(device, context, ChunkMeshDetail.Shell, ref _shellVertexBuffer, ref _shellIndexBuffer, ref _shellIndexCount,
                        ref _shellWaterVertexBuffer, ref _shellWaterIndexBuffer, ref _shellWaterIndexCount);
                    _shellMeshBuilt = true;
                    break;
                default:
                    if (_fullMeshBuilt && !MeshStale) return;
                    BuildMesh(device, context, ChunkMeshDetail.Full, ref _fullVertexBuffer, ref _fullIndexBuffer, ref _fullIndexCount,
                        ref _fullWaterVertexBuffer, ref _fullWaterIndexBuffer, ref _fullWaterIndexCount);
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
                    _surfaceMeshBuilt = true;
                    break;
                case ChunkMeshDetail.Shell:
                    _shellVertexBuffer?.Dispose();
                    _shellIndexBuffer?.Dispose();
                    _shellVertexBuffer = null;
                    _shellIndexBuffer = null;
                    _shellIndexCount = 0;
                    _shellWaterVertexBuffer?.Dispose();
                    _shellWaterIndexBuffer?.Dispose();
                    _shellWaterVertexBuffer = null;
                    _shellWaterIndexBuffer = null;
                    _shellWaterIndexCount = 0;
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
                    if (data.IndexCount == 0 && data.WaterIndexCount == 0)
                    {
                        MarkEmptyMeshDetail(ChunkMeshDetail.Surface);
                    }
                    else
                    {
                        if (!_surfaceMeshBuilt || wasStale)
                        {
                            UploadMeshBuffers(
                                device,
                                data.Vertices,
                                data.VertexCount,
                                data.Indices,
                                data.IndexCount,
                                ref _surfaceVertexBuffer,
                                ref _surfaceIndexBuffer,
                                ref _surfaceIndexCount);

                            UploadMeshBuffers(
                                device,
                                data.WaterVertices,
                                data.WaterVertexCount,
                                data.WaterIndices,
                                data.WaterIndexCount,
                                ref _surfaceWaterVertexBuffer,
                                ref _surfaceWaterIndexBuffer,
                                ref _surfaceWaterIndexCount);

                            _surfaceMeshBuilt = true;
                        }
                    }
                    SurfaceMeshBuildInFlight = false;
                    break;
                case ChunkMeshDetail.Shell:
                    if (data.IndexCount == 0 && data.WaterIndexCount == 0)
                    {
                        MarkEmptyMeshDetail(ChunkMeshDetail.Shell);
                    }
                    else if (!_shellMeshBuilt || wasStale)
                    {
                        UploadMeshBuffers(
                            device,
                            data.Vertices,
                            data.VertexCount,
                            data.Indices,
                            data.IndexCount,
                            ref _shellVertexBuffer,
                            ref _shellIndexBuffer,
                            ref _shellIndexCount);

                        UploadMeshBuffers(
                            device,
                            data.WaterVertices,
                            data.WaterVertexCount,
                            data.WaterIndices,
                            data.WaterIndexCount,
                            ref _shellWaterVertexBuffer,
                            ref _shellWaterIndexBuffer,
                            ref _shellWaterIndexCount);

                        _shellMeshBuilt = true;
                    }
                    ShellMeshBuildInFlight = false;
                    break;
                default:
                    if (data.IndexCount == 0 && data.WaterIndexCount == 0)
                    {
                        MarkEmptyMeshDetail(ChunkMeshDetail.Full);
                    }
                    else
                    {
                        if (!_fullMeshBuilt || wasStale)
                        {
                            UploadMeshBuffers(
                                device,
                                data.Vertices,
                                data.VertexCount,
                                data.Indices,
                                data.IndexCount,
                                ref _fullVertexBuffer,
                                ref _fullIndexBuffer,
                                ref _fullIndexCount);

                            UploadMeshBuffers(
                                device,
                                data.WaterVertices,
                                data.WaterVertexCount,
                                data.WaterIndices,
                                data.WaterIndexCount,
                                ref _fullWaterVertexBuffer,
                                ref _fullWaterIndexBuffer,
                                ref _fullWaterIndexCount);

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
                    _surfaceMeshBuilt = false;
                    break;
                case ChunkMeshDetail.Shell:
                    _shellVertexBuffer?.Dispose();
                    _shellIndexBuffer?.Dispose();
                    _shellWaterVertexBuffer?.Dispose();
                    _shellWaterIndexBuffer?.Dispose();
                    _shellVertexBuffer = null;
                    _shellIndexBuffer = null;
                    _shellWaterVertexBuffer = null;
                    _shellWaterIndexBuffer = null;
                    _shellIndexCount = 0;
                    _shellWaterIndexCount = 0;
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
            _shellWaterVertexBuffer?.Dispose();
            _shellWaterIndexBuffer?.Dispose();

            _fullVertexBuffer = null;
            _fullIndexBuffer = null;
            _fullIndexCount = 0;
            _fullWaterVertexBuffer = null;
            _fullWaterIndexBuffer = null;
            _fullWaterIndexCount = 0;
            _surfaceVertexBuffer = null;
            _surfaceIndexBuffer = null;
            _surfaceIndexCount = 0;
            _surfaceWaterVertexBuffer = null;
            _surfaceWaterIndexBuffer = null;
            _surfaceWaterIndexCount = 0;
            _shellVertexBuffer = null;
            _shellIndexBuffer = null;
            _shellIndexCount = 0;
            _shellWaterVertexBuffer = null;
            _shellWaterIndexBuffer = null;
            _shellWaterIndexCount = 0;
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
            ref VertexBuffer? waterVertexBuffer, ref IndexBuffer? waterIndexBuffer, ref int waterIndexCount)
        {
            var data = BuildMeshCpuOnly(context, detail, buildFlora: false);
            try
            {
                UploadMeshBuffers(
                    device,
                    data.Vertices,
                    data.VertexCount,
                    data.Indices,
                    data.IndexCount,
                    ref vertexBuffer,
                    ref indexBuffer,
                    ref indexCount);

                UploadMeshBuffers(
                    device,
                    data.WaterVertices,
                    data.WaterVertexCount,
                    data.WaterIndices,
                    data.WaterIndexCount,
                    ref waterVertexBuffer,
                    ref waterIndexBuffer,
                    ref waterIndexCount);
            }
            finally
            {
                data.ReturnToPools();
            }
        }
        private static void UploadMeshBuffers(
            GraphicsDevice device,
            Vertex[] vertices,
            int vertexCount,
            uint[] indices,
            int indexCount,
            ref VertexBuffer? vertexBuffer,
            ref IndexBuffer? indexBuffer,
            ref int outIndexCount)
        {
            if (vertexCount == 0 || indexCount == 0)
            {
                vertexBuffer?.Dispose();
                indexBuffer?.Dispose();
                vertexBuffer = null;
                indexBuffer = null;
                outIndexCount = 0;
                return;
            }

            int targetVertexCount = ((vertexCount + 255) / 256) * 256;
            if (vertexBuffer == null || vertexBuffer.VertexCount < vertexCount)
            {
                vertexBuffer?.Dispose();
                vertexBuffer = new VertexBuffer(device, typeof(Vertex), targetVertexCount, BufferUsage.WriteOnly);
            }
            vertexBuffer.SetData(vertices, 0, vertexCount);

            int targetIndexCount = ((indexCount + 255) / 256) * 256;
            if (indexBuffer == null || indexBuffer.IndexCount < indexCount)
            {
                indexBuffer?.Dispose();
                indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, targetIndexCount, BufferUsage.WriteOnly);
            }
            indexBuffer.SetData(indices, 0, indexCount);
            outIndexCount = indexCount;
        }

        public void Dispose()
        {
            IsUnloaded = true;
            DisposeMeshBuffers();
            _fullMeshBuilt = false;
            _surfaceMeshBuilt = false;
            _shellMeshBuilt = false;
            _floraMeshBuilt = false;
        }
    }
}
