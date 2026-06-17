using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Domain.World;

namespace Autonocraft.World
{
    // Block array and column height cache.
    public partial class Chunk
    {
        private readonly BlockType[] _blocks;
        // Cached highest/lowest solid Y per column (lx + lz * Width). -1 = no solid block.
        private readonly short[] _columnHighestSolid = new short[Width * Depth];
        private readonly short[] _columnLowestSolid = new short[Width * Depth];
        // Mesh extent includes alpha-cutout blocks (e.g. tree leaves above trunks).
        private readonly short[] _columnHighestMesh = new short[Width * Depth];
        private readonly short[] _columnLowestMesh = new short[Width * Depth];
        private bool _columnHeightsBuilt;
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

                        if (type.IsSolidForSpawn() || type.IsSlab() || type.IsAlphaCutout() || type.IsWater())
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

        /// <summary>
        /// Fast column fill during terrain generation — writes blocks directly without per-set bounds overhead.
        /// Column height caches are finalized via <see cref="RebuildColumnHeights"/> after carving/decoration.
        /// </summary>
        internal void FillTerrainColumn(int lx, int lz, TerrainColumn column)
        {
            int height = column.SurfaceHeight;

            for (int y = 0; y < Height; y++)
            {
                BlockType block;
                if (y > height)
                {
                    if (y <= WorldConstants.SeaLevel && (column.Biome.Primary == BiomeType.Ocean || column.IsRiver || column.IsLake))
                    {
                        bool freezeSurface = column.Biome.Primary == BiomeType.SnowyPeaks;
                        block = y == WorldConstants.SeaLevel && freezeSurface
                            ? BlockType.Ice
                            : BlockType.Water;
                    }
                    else
                    {
                        block = BlockType.Air;
                    }
                }
                else if (y == height)
                {
                    block = column.SurfaceBlock;
                }
                else if (y > height - WorldConstants.DirtDepth)
                {
                    block = column.SubsurfaceBlock;
                }
                else if (y <= 2)
                {
                    block = BlockType.Stone;
                }
                else
                {
                    block = column.FillerBlock;
                }

                _blocks[GetIndex(lx, y, lz)] = block;
            }
        }

        internal void SetBlockUnchecked(int lx, int y, int lz, BlockType type) =>
            _blocks[GetIndex(lx, y, lz)] = type;

        internal BlockType GetBlockUnchecked(int lx, int y, int lz) =>
            _blocks[GetIndex(lx, y, lz)];

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
            bool meshBlock = solid || type.IsSlab() || type.IsAlphaCutout() || type.IsWater();

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

                if (!solid)
                {
                    int solidHigh = _columnHighestSolid[idx];
                    int solidLow = _columnLowestSolid[idx];
                    if (y == solidHigh || y == solidLow)
                    {
                        RescanColumnHeights(lx, lz);
                    }
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

            RescanColumnHeights(lx, lz);
        }

        private void RescanColumnHeights(int lx, int lz)
        {
            int idx = lz * Width + lx;
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

                if (scanType.IsSolidForSpawn() || scanType.IsSlab() || scanType.IsAlphaCutout() || scanType.IsWater())
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
    }
}
