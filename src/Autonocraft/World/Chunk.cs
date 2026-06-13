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

        private VertexBuffer? _fullVertexBuffer;
        private IndexBuffer? _fullIndexBuffer;
        private int _fullIndexCount;

        private VertexBuffer? _surfaceVertexBuffer;
        private IndexBuffer? _surfaceIndexBuffer;
        private int _surfaceIndexCount;

        private VertexBuffer? _shellVertexBuffer;
        private IndexBuffer? _shellIndexBuffer;
        private int _shellIndexCount;

        private bool _fullMeshBuilt;
        private bool _surfaceMeshBuilt;
        private bool _shellMeshBuilt;

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
            if (IsInLocalBounds(x, y, z))
            {
                _blocks[GetIndex(x, y, z)] = type;
            }
        }

        public void GenerateAllMeshes(GraphicsDevice device, VoxelWorld world)
        {
            EnsureMesh(device, world, ChunkMeshDetail.Full);
            EnsureMesh(device, world, ChunkMeshDetail.Surface);
            EnsureMesh(device, world, ChunkMeshDetail.Shell);
        }

        public void EnsureMesh(GraphicsDevice device, VoxelWorld world, ChunkMeshDetail detail)
        {
            switch (detail)
            {
                case ChunkMeshDetail.Surface:
                    if (_surfaceMeshBuilt) return;
                    BuildMesh(device, world, ChunkMeshDetail.Surface, ref _surfaceVertexBuffer, ref _surfaceIndexBuffer, ref _surfaceIndexCount);
                    _surfaceMeshBuilt = true;
                    break;
                case ChunkMeshDetail.Shell:
                    if (_shellMeshBuilt) return;
                    BuildMesh(device, world, ChunkMeshDetail.Shell, ref _shellVertexBuffer, ref _shellIndexBuffer, ref _shellIndexCount);
                    _shellMeshBuilt = true;
                    break;
                default:
                    if (_fullMeshBuilt) return;
                    BuildMesh(device, world, ChunkMeshDetail.Full, ref _fullVertexBuffer, ref _fullIndexBuffer, ref _fullIndexCount);
                    _fullMeshBuilt = true;
                    break;
            }
        }

        public void InvalidateMeshes()
        {
            _fullMeshBuilt = false;
            _surfaceMeshBuilt = false;
            _shellMeshBuilt = false;
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

        private void BuildMesh(GraphicsDevice device, VoxelWorld world, ChunkMeshDetail detail,
            ref VertexBuffer? vertexBuffer, ref IndexBuffer? indexBuffer, ref int indexCount)
        {
            var vertices = new List<Vertex>();
            var indices = new List<uint>();

            if (detail == ChunkMeshDetail.Shell)
            {
                BuildShellMesh(world, ChunkX * Width, ChunkZ * Depth, vertices, indices);
            }
            else
            {
                var fullVertices = new List<Vertex>();
                var fullIndices = new List<uint>();
                var surfaceVertices = new List<Vertex>();
                var surfaceIndices = new List<uint>();
                BuildFullAndSurfaceMeshLists(world, fullVertices, fullIndices, surfaceVertices, surfaceIndices);

                if (detail == ChunkMeshDetail.Surface)
                {
                    vertices = surfaceVertices;
                    indices = surfaceIndices;
                }
                else
                {
                    vertices = fullVertices;
                    indices = fullIndices;
                }
            }

            UploadMeshBuffers(device, vertices.ToArray(), indices.ToArray(), ref vertexBuffer, ref indexBuffer, ref indexCount);
        }

        internal int GetMeshIndexCount(VoxelWorld world, ChunkMeshDetail detail)
        {
            if (detail == ChunkMeshDetail.Shell)
            {
                var vertices = new List<Vertex>();
                var indices = new List<uint>();
                BuildShellMesh(world, ChunkX * Width, ChunkZ * Depth, vertices, indices);
                return indices.Count;
            }

            var fullVertices = new List<Vertex>();
            var fullIndices = new List<uint>();
            var surfaceVertices = new List<Vertex>();
            var surfaceIndices = new List<uint>();
            BuildFullAndSurfaceMeshLists(world, fullVertices, fullIndices, surfaceVertices, surfaceIndices);
            return detail == ChunkMeshDetail.Surface ? surfaceIndices.Count : fullIndices.Count;
        }

        private void BuildFullAndSurfaceMeshLists(
            VoxelWorld world,
            List<Vertex> fullVertices,
            List<uint> fullIndices,
            List<Vertex> surfaceVertices,
            List<uint> surfaceIndices)
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

                        int wx = worldOffsetX + x;
                        int wy = y;
                        int wz = worldOffsetZ + z;

                        EmitBlockFaces(world, fullVertices, fullIndices, wx, wy, wz, type, includeAo: true, shellBoundaryOnly: false, x, z);

                        if (IsSurfaceBlock(world, wx, wy, wz))
                        {
                            EmitBlockFaces(world, surfaceVertices, surfaceIndices, wx, wy, wz, type, includeAo: false, shellBoundaryOnly: false, x, z);
                        }
                    }
                }
            }
        }

        private void BuildShellMesh(VoxelWorld world, int worldOffsetX, int worldOffsetZ, List<Vertex> vertices, List<uint> indices)
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

                    if (topY < 0) continue;

                    int wx = worldOffsetX + x;
                    int wy = topY;
                    int wz = worldOffsetZ + z;

                    EmitBlockFaces(world, vertices, indices, wx, wy, wz, topType, includeAo: false, shellBoundaryOnly: true, x, z);
                }
            }
        }

        private static bool IsSurfaceBlock(VoxelWorld world, int wx, int wy, int wz)
        {
            if (wy < Chunk.Height - 1 && world.GetBlock(wx, wy + 1, wz).IsTransparent())
            {
                return true;
            }

            return world.GetBlock(wx + 1, wy, wz).IsTransparent()
                || world.GetBlock(wx - 1, wy, wz).IsTransparent()
                || world.GetBlock(wx, wy, wz + 1).IsTransparent()
                || world.GetBlock(wx, wy, wz - 1).IsTransparent();
        }

        private void EmitBlockFaces(VoxelWorld world, List<Vertex> vertices, List<uint> indices,
            int wx, int wy, int wz, BlockType type, bool includeAo, bool shellBoundaryOnly, int localX, int localZ)
        {
            Vector3 pos = new Vector3(wx, wy, wz);
            Vector3 color = Vector3.One;

            if (wy == Height - 1 || world.GetBlock(wx, wy + 1, wz).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(0, 1, 0), color, type, world, includeAo,
                    new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0));
            }

            if (!shellBoundaryOnly)
            {
                if (wy == 0 || world.GetBlock(wx, wy - 1, wz).IsTransparent())
                {
                    AddFace(vertices, indices, pos, new Vector3(0, -1, 0), color, type, world, includeAo,
                        new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1));
                }
            }

            if ((!shellBoundaryOnly || localX == Width - 1) && world.GetBlock(wx + 1, wy, wz).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(1, 0, 0), color, type, world, includeAo,
                    new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1));
            }

            if ((!shellBoundaryOnly || localX == 0) && world.GetBlock(wx - 1, wy, wz).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(-1, 0, 0), color, type, world, includeAo,
                    new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0));
            }

            if ((!shellBoundaryOnly || localZ == Depth - 1) && world.GetBlock(wx, wy, wz + 1).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(0, 0, 1), color, type, world, includeAo,
                    new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1));
            }

            if ((!shellBoundaryOnly || localZ == 0) && world.GetBlock(wx, wy, wz - 1).IsTransparent())
            {
                AddFace(vertices, indices, pos, new Vector3(0, 0, -1), color, type, world, includeAo,
                    new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0));
            }
        }

        private void AddFace(List<Vertex> vertices, List<uint> indices, Vector3 pos, Vector3 normal, Vector3 baseColor, BlockType blockType,
                             VoxelWorld world, bool includeAo, Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3)
        {
            uint startIndex = (uint)vertices.Count;

            var uv = BlockAtlas.GetFaceUVs(blockType, normal);

            Vector3 col0;
            Vector3 col1;
            Vector3 col2;
            Vector3 col3;

            if (includeAo)
            {
                float ao0 = ComputeCornerAO(world, pos + c0, normal);
                float ao1 = ComputeCornerAO(world, pos + c1, normal);
                float ao2 = ComputeCornerAO(world, pos + c2, normal);
                float ao3 = ComputeCornerAO(world, pos + c3, normal);
                col0 = new Vector3(ao0, ao0, ao0);
                col1 = new Vector3(ao1, ao1, ao1);
                col2 = new Vector3(ao2, ao2, ao2);
                col3 = new Vector3(ao3, ao3, ao3);
            }
            else
            {
                col0 = col1 = col2 = col3 = baseColor;
            }

            vertices.Add(new Vertex(pos + c0, col0, normal, new Vector2(uv.uMin, uv.vMax)));
            vertices.Add(new Vertex(pos + c1, col1, normal, new Vector2(uv.uMin, uv.vMin)));
            vertices.Add(new Vertex(pos + c2, col2, normal, new Vector2(uv.uMax, uv.vMin)));
            vertices.Add(new Vertex(pos + c3, col3, normal, new Vector2(uv.uMax, uv.vMax)));

            indices.Add(startIndex + 0);
            indices.Add(startIndex + 1);
            indices.Add(startIndex + 2);

            indices.Add(startIndex + 0);
            indices.Add(startIndex + 2);
            indices.Add(startIndex + 3);
        }

        private static Vector3 FloorVec3(Vector3 v)
        {
            return new Vector3(MathF.Floor(v.X), MathF.Floor(v.Y), MathF.Floor(v.Z));
        }

        private static float ComputeCornerAO(VoxelWorld world, Vector3 vertexPos, Vector3 normal)
        {
            Vector3 up    = Math.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 tang1 = Vector3.Normalize(Vector3.Cross(normal, up));
            Vector3 tang2 = Vector3.Cross(normal, tang1);

            Vector3 vpFloor = FloorVec3(vertexPos + new Vector3(0.001f));
            float t1 = Vector3.Dot(vertexPos - vpFloor, tang1) > 0 ? 1 : -1;
            float t2 = Vector3.Dot(vertexPos - vpFloor, tang2) > 0 ? 1 : -1;

            Vector3 blockCenter = FloorVec3(vertexPos - normal * 0.5f) + new Vector3(0.5f);

            Vector3 s1pos = blockCenter + tang1 * t1;
            Vector3 s2pos = blockCenter + tang2 * t2;
            Vector3 copos = blockCenter + tang1 * t1 + tang2 * t2;

            bool s1 = world.GetBlock((int)Math.Floor(s1pos.X), (int)Math.Floor(s1pos.Y), (int)Math.Floor(s1pos.Z)).IsSolidForSpawn();
            bool s2 = world.GetBlock((int)Math.Floor(s2pos.X), (int)Math.Floor(s2pos.Y), (int)Math.Floor(s2pos.Z)).IsSolidForSpawn();
            bool co = world.GetBlock((int)Math.Floor(copos.X), (int)Math.Floor(copos.Y), (int)Math.Floor(copos.Z)).IsSolidForSpawn();

            if (s1 && s2) return 0.72f;
            int occ = (s1 ? 1 : 0) + (s2 ? 1 : 0) + (co ? 1 : 0);
            return 1.0f - occ * 0.09f;
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
            _fullVertexBuffer?.Dispose();
            _fullIndexBuffer?.Dispose();
            _surfaceVertexBuffer?.Dispose();
            _surfaceIndexBuffer?.Dispose();
            _shellVertexBuffer?.Dispose();
            _shellIndexBuffer?.Dispose();

            _fullVertexBuffer = null;
            _fullIndexBuffer = null;
            _fullIndexCount = 0;
            _surfaceVertexBuffer = null;
            _surfaceIndexBuffer = null;
            _surfaceIndexCount = 0;
            _shellVertexBuffer = null;
            _shellIndexBuffer = null;
            _shellIndexCount = 0;
            _fullMeshBuilt = false;
            _surfaceMeshBuilt = false;
            _shellMeshBuilt = false;
        }
    }
}
