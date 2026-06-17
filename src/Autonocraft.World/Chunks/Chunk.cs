using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Domain.World;

namespace Autonocraft.World
{
    public enum ChunkMeshDetail
    {
        Full,
        Surface,
        Shell
    }

    public partial class Chunk : IDisposable
    {
        public const int Width = 16;
        public const int Height = 192;
        public const int Depth = 16;
        /// <summary>Rendered water tops sit below the block ceiling, similar to Minecraft source water.</summary>
        public const float WaterSurfaceHeight = 0.875f;

        public int ChunkX { get; }
        public int ChunkZ { get; }

        // Set on main thread before Task.Run, cleared on main thread in ApplyPrebuiltMesh.
        // Prevents queueing duplicate background builds for the same chunk+detail.
        internal volatile bool FullMeshBuildInFlight;
        internal volatile bool SurfaceMeshBuildInFlight;
        internal volatile bool ShellMeshBuildInFlight;
        // Set when a neighbor chunk appeared — keeps old GPU buffers visible while rebuilding.
        internal volatile bool MeshStale;
        public volatile bool IsUnloaded;
        internal bool InitialLoadShellReported;
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
    }
}
