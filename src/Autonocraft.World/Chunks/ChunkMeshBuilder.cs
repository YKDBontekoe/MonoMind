using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Domain.World;

namespace Autonocraft.World
{
    // CPU mesh Full/Surface/Shell generation and flora.
    public partial class Chunk
    {
        [ThreadStatic]
        private static List<Vertex>? _scratchVertices;
        [ThreadStatic]
        private static List<uint>? _scratchIndices;
        [ThreadStatic]
        private static List<Vertex>? _scratchWaterVertices;
        [ThreadStatic]
        private static List<uint>? _scratchWaterIndices;
        [ThreadStatic]
        private static List<FloraVertex>? _scratchFloraVertices;
        [ThreadStatic]
        private static List<uint>? _scratchFloraIndices;

        private List<Vertex>? _vertexScratch;
        private List<uint>? _indexScratch;
        internal PrebuiltMeshData BuildMeshCpuOnly(MeshBuildContext context, ChunkMeshDetail detail, bool buildFlora)
        {
            _scratchVertices ??= new List<Vertex>(8192);
            _scratchIndices ??= new List<uint>(12288);
            _scratchWaterVertices ??= new List<Vertex>(2048);
            _scratchWaterIndices ??= new List<uint>(3072);

            _scratchVertices.Clear();
            _scratchIndices.Clear();
            _scratchWaterVertices.Clear();
            _scratchWaterIndices.Clear();

            if (detail == ChunkMeshDetail.Shell)
            {
                BuildShellMesh(context, ChunkX * Width, ChunkZ * Depth, _scratchVertices, _scratchIndices, _scratchWaterVertices, _scratchWaterIndices);
            }
            else if (detail == ChunkMeshDetail.Surface)
            {
                BuildSurfaceMeshList(context, _scratchVertices, _scratchIndices, _scratchWaterVertices, _scratchWaterIndices);
            }
            else
            {
                BuildFullMeshList(context, _scratchVertices, _scratchIndices, _scratchWaterVertices, _scratchWaterIndices);
            }

            int vertexCount = _scratchVertices.Count;
            int indexCount = _scratchIndices.Count;
            int waterVertexCount = _scratchWaterVertices.Count;
            int waterIndexCount = _scratchWaterIndices.Count;

            Vertex[] vertexArray = vertexCount == 0
                ? Array.Empty<Vertex>()
                : ArrayPool<Vertex>.Shared.Rent(vertexCount);
            if (vertexCount > 0)
            {
                _scratchVertices.CopyTo(vertexArray, 0);
            }

            uint[] indexArray = indexCount == 0
                ? Array.Empty<uint>()
                : ArrayPool<uint>.Shared.Rent(indexCount);
            if (indexCount > 0)
            {
                _scratchIndices.CopyTo(indexArray, 0);
            }

            Vertex[] waterVertexArray = waterVertexCount == 0
                ? Array.Empty<Vertex>()
                : ArrayPool<Vertex>.Shared.Rent(waterVertexCount);
            if (waterVertexCount > 0)
            {
                _scratchWaterVertices.CopyTo(waterVertexArray, 0);
            }

            uint[] waterIndexArray = waterIndexCount == 0
                ? Array.Empty<uint>()
                : ArrayPool<uint>.Shared.Rent(waterIndexCount);
            if (waterIndexCount > 0)
            {
                _scratchWaterIndices.CopyTo(waterIndexArray, 0);
            }

            FloraVertex[]? fv = null;
            uint[]? fi = null;
            if (buildFlora && !_floraMeshBuilt)
            {
                _scratchFloraVertices ??= new List<FloraVertex>(1024);
                _scratchFloraIndices ??= new List<uint>(1536);

                _scratchFloraVertices.Clear();
                _scratchFloraIndices.Clear();

                FloraMeshBuilder.Build(this, context.BiomeMap, _scratchFloraVertices, _scratchFloraIndices);
                if (_scratchFloraVertices.Count > 0)
                {
                    fv = _scratchFloraVertices.ToArray();
                    fi = _scratchFloraIndices.ToArray();
                }
            }

            return new PrebuiltMeshData(
                vertexArray,
                vertexCount,
                indexArray,
                indexCount,
                waterVertexArray,
                waterVertexCount,
                waterIndexArray,
                waterIndexCount,
                detail,
                buildFlora,
                fv,
                fi);
        }
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
                BuildShellMesh(context, ChunkX * Width, ChunkZ * Depth, _vertexScratch, _indexScratch, _waterVertexScratch, _waterIndexScratch);
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
                var waterVertices = new List<Vertex>();
                var waterIndices = new List<uint>();
                BuildShellMesh(context, ChunkX * Width, ChunkZ * Depth, vertices, indices, waterVertices, waterIndices);
                return indices.Count + waterIndices.Count;
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

        private void BuildShellMesh(
            MeshBuildContext context,
            int worldOffsetX,
            int worldOffsetZ,
            List<Vertex> vertices,
            List<uint> indices,
            List<Vertex> waterVertices,
            List<uint> waterIndices)
        {
            if (!_columnHeightsBuilt)
            {
                RebuildColumnHeights();
            }

            for (int z = 0; z < Depth; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int startY = GetCachedHighestMeshY(x, z);
                    if (startY < 0)
                    {
                        continue;
                    }

                    for (int y = startY; y >= 0; y--)
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

                        if (type.IsSolidForSpawn() || type.IsSlab() || type.IsWater())
                        {
                            NoteBlockType(type);
                            int shellWx = worldOffsetX + x;
                            int shellWy = y;
                            int shellWz = worldOffsetZ + z;

                            if (type.IsWater())
                            {
                                EmitBlockFaces(context, vertices, indices, waterVertices, waterIndices, shellWx, shellWy, shellWz, type, includeAo: false, shellTopOnly: true, x, z);
                            }
                            else
                            {
                                EmitBlockFaces(context, vertices, indices, vertices, indices, shellWx, shellWy, shellWz, type, includeAo: false, shellTopOnly: true, x, z);
                            }

                            break;
                        }
                    }
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

            float topY = type.IsSlab() ? 0.5f : 1f;

            static SideFaceCoverage GetSideFaceCoverage(MeshBuildContext context, int wx, int wy, int wz, int nx, int ny, int nz, BlockType type)
            {
                BlockType neighbor = context.GetBlock(nx, ny, nz);

                if (neighbor.IsTransparent())
                {
                    if (type.IsSlab() && neighbor.IsSlab())
                    {
                        return SideFaceCoverage.Hidden;
                    }

                    if (!type.IsSlab() && neighbor.IsSlab())
                    {
                        return SideFaceCoverage.UpperHalf;
                    }

                    return SideFaceCoverage.Full;
                }

                if (type.IsSlab())
                {
                    return neighbor.IsSlab() ? SideFaceCoverage.Hidden : SideFaceCoverage.Full;
                }

                if (neighbor.IsSlab())
                {
                    return SideFaceCoverage.UpperHalf;
                }

                // Full block against full block: keep the face when a slab above creates a step.
                if (context.GetBlock(nx, ny + 1, nz).IsSlab() || context.GetBlock(wx, wy + 1, wz).IsSlab())
                {
                    return SideFaceCoverage.Full;
                }

                return SideFaceCoverage.Hidden;
            }

            bool IsFaceCulled(BlockType neighbor)
            {
                if (!neighbor.IsTransparent())
                {
                    return true;
                }
                if (type.IsSlab() && neighbor.IsSlab())
                {
                    return true;
                }
                if (!type.IsSlab() && neighbor.IsSlab())
                {
                    return true;
                }
                return false;
            }

            void EmitSideFace(int nx, int ny, int nz, Vector3 normal, Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3)
            {
                if (!ShouldEmitWaterSideFace(context, nx, ny, nz, type))
                {
                    return;
                }

                var coverage = GetSideFaceCoverage(context, wx, wy, wz, nx, ny, nz, type);
                if (coverage == SideFaceCoverage.Hidden)
                {
                    return;
                }

                float yMin = 0f;
                float yMax = topY;
                if (coverage == SideFaceCoverage.UpperHalf)
                {
                    yMin = 0.5f;
                }

                c0.Y = yMin; c1.Y = yMax; c2.Y = yMax; c3.Y = yMin;
                var sideType = GetBleededBlockType(type, wx, wy, wz, normal);
                AddFace(targetVertices, targetIndices, pos, normal, color, sideType, context, includeAo, c0, c1, c2, c3);
            }

            BlockType GetBleededBlockType(BlockType self, int x, int y, int z, Vector3 normal)
            {
                if (MathF.Abs(normal.Y) < 0.1f && self.CanSupportBleeding())
                {
                    var above = context.GetBlock(x, y + 1, z);
                    if (above == BlockType.Grass || above == BlockType.GrassSlab)
                    {
                        return BlockType.Grass;
                    }
                    if (above == BlockType.Snow || above == BlockType.SnowSlab)
                    {
                        return BlockType.SnowSide;
                    }
                }
                return self;
            }

            var neighborYPlus = NeighborForFace(context, wx, wy + 1, wz, type);
            if (wy == Height - 1 || !IsFaceCulled(neighborYPlus))
            {
                AddFace(targetVertices, targetIndices, pos, new Vector3(0, 1, 0), color, type, context, includeAo,
                    new Vector3(0, topY, 0), new Vector3(0, topY, 1), new Vector3(1, topY, 1), new Vector3(1, topY, 0));
            }

            if (shellTopOnly)
            {
                return;
            }

            var neighborYMinus = NeighborForFace(context, wx, wy - 1, wz, type);
            if (wy == 0 || !IsFaceCulled(neighborYMinus))
            {
                if (ShouldEmitWaterSideFace(context, wx, wy - 1, wz, type))
                {
                    AddFace(targetVertices, targetIndices, pos, new Vector3(0, -1, 0), color, type, context, includeAo,
                        new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1));
                }
            }

            EmitSideFace(wx + 1, wy, wz, new Vector3(1, 0, 0),
                new Vector3(1, 0, 0), new Vector3(1, topY, 0), new Vector3(1, topY, 1), new Vector3(1, 0, 1));

            EmitSideFace(wx - 1, wy, wz, new Vector3(-1, 0, 0),
                new Vector3(0, 0, 1), new Vector3(0, topY, 1), new Vector3(0, topY, 0), new Vector3(0, 0, 0));

            EmitSideFace(wx, wy, wz + 1, new Vector3(0, 0, 1),
                new Vector3(1, 0, 1), new Vector3(1, topY, 1), new Vector3(0, topY, 1), new Vector3(0, 0, 1));

            EmitSideFace(wx, wy, wz - 1, new Vector3(0, 0, -1),
                new Vector3(0, 0, 0), new Vector3(0, topY, 0), new Vector3(1, topY, 0), new Vector3(1, 0, 0));
        }

        private enum SideFaceCoverage
        {
            Hidden,
            Full,
            UpperHalf
        }

        private static BlockType NeighborForFace(MeshBuildContext context, int nx, int ny, int nz, BlockType selfType)
        {
            return selfType.IsWater()
                ? context.GetCullingBlock(nx, ny, nz, selfType)
                : context.GetBlock(nx, ny, nz);
        }

        private static bool ShouldEmitWaterSideFace(MeshBuildContext context, int nx, int ny, int nz, BlockType type)
        {
            if (!type.IsWater())
            {
                return true;
            }

            var neighbor = context.GetCullingBlock(nx, ny, nz, type);
            return neighbor != BlockType.Air && !neighbor.IsWater();
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
                waterColor = new Vector3(0.42f, 0.72f, 0.88f);
            }
            else if (depth == 1)
            {
                waterColor = new Vector3(0.3f, 0.55f, 0.78f);
            }
            else if (depth == 2)
            {
                waterColor = new Vector3(0.18f, 0.38f, 0.68f);
            }
            else
            {
                waterColor = new Vector3(0.1f, 0.22f, 0.55f);
            }

            if (IsWaterShoreBlock(context, wx, wy, wz))
            {
                return Vector3.Lerp(waterColor, new Vector3(0.62f, 0.86f, 0.98f), 0.35f);
            }

            return waterColor;
        }

        private static bool IsWaterShoreBlock(MeshBuildContext context, int wx, int wy, int wz)
        {
            return IsLandBlock(context.GetBlock(wx + 1, wy, wz))
                || IsLandBlock(context.GetBlock(wx - 1, wy, wz))
                || IsLandBlock(context.GetBlock(wx, wy, wz + 1))
                || IsLandBlock(context.GetBlock(wx, wy, wz - 1));
        }

        private static bool IsLandBlock(BlockType block)
        {
            return block != BlockType.Air && !block.IsWater();
        }

        private static Vector3 AdjustWaterCornerOffset(Vector3 cornerOffset, BlockType blockType)
        {
            if (!blockType.IsWater() || cornerOffset.Y <= 0.5f)
            {
                return cornerOffset;
            }

            return new Vector3(cornerOffset.X, WaterSurfaceHeight, cornerOffset.Z);
        }

        private void AddFace(List<Vertex> vertices, List<uint> indices, Vector3 pos, Vector3 normal, Vector3 baseColor, BlockType blockType,
                             MeshBuildContext context, bool includeAo, Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3)
        {
            uint startIndex = (uint)vertices.Count;

            if (blockType.IsWater())
            {
                c0 = AdjustWaterCornerOffset(c0, blockType);
                c1 = AdjustWaterCornerOffset(c1, blockType);
                c2 = AdjustWaterCornerOffset(c2, blockType);
                c3 = AdjustWaterCornerOffset(c3, blockType);
            }

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

            bool isGrassSide = blockType.GetBaseBlockType() == BlockType.Grass && MathF.Abs(normal.Y) < 0.1f;

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
    }
}
