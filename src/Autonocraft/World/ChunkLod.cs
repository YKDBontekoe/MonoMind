using System;

namespace Autonocraft.World
{
    public static class ChunkLod
    {
        public static (int lod0Max, int lod1Max) GetBandThresholds(int renderDistance)
        {
            int lod0Max = Math.Max(1, renderDistance / 3);
            int lod1Max = Math.Max(lod0Max + 1, (renderDistance * 2) / 3);
            return (lod0Max, lod1Max);
        }

        public static ChunkMeshDetail SelectDetail(int chunkDistance, int renderDistance)
        {
            var (lod0Max, lod1Max) = GetBandThresholds(renderDistance);

            if (chunkDistance <= lod0Max)
            {
                return ChunkMeshDetail.Full;
            }

            if (chunkDistance <= lod1Max)
            {
                return ChunkMeshDetail.Surface;
            }

            return ChunkMeshDetail.Shell;
        }

        /// <summary>
        /// Picks the next mesh tier to build during streaming. Starts with cheap Shell
        /// so new chunks pop in quickly, then upgrades toward the render target.
        /// </summary>
        public static ChunkMeshDetail SelectBuildDetail(Chunk chunk, int chunkDistance, int renderDistance, bool restrictLod = false)
        {
            var target = SelectRenderTarget(chunkDistance, renderDistance, restrictLod);
            return SelectBuildDetailToward(chunk, target);
        }

        public static ChunkMeshDetail SelectRenderTarget(int chunkDistance, int renderDistance, bool restrictLod)
        {
            var detail = SelectDetail(chunkDistance, renderDistance);
            if (!restrictLod || chunkDistance <= 1)
            {
                return detail;
            }

            // Fast travel: keep Full only in the immediate ring; mid/far bands unchanged.
            return detail == ChunkMeshDetail.Full ? ChunkMeshDetail.Surface : detail;
        }

        private static ChunkMeshDetail SelectBuildDetailToward(Chunk chunk, ChunkMeshDetail target)
        {
            // Stale meshes need a direct rebuild of the target tier — skip LOD progression.
            if (chunk.MeshStale)
            {
                return target;
            }

            if (!chunk.HasMesh(ChunkMeshDetail.Shell))
            {
                return ChunkMeshDetail.Shell;
            }

            if (target == ChunkMeshDetail.Shell)
            {
                return ChunkMeshDetail.Shell;
            }

            if (!chunk.HasMesh(ChunkMeshDetail.Surface))
            {
                return ChunkMeshDetail.Surface;
            }

            if (target == ChunkMeshDetail.Surface)
            {
                return ChunkMeshDetail.Surface;
            }

            if (!chunk.HasMesh(ChunkMeshDetail.Full))
            {
                return ChunkMeshDetail.Full;
            }

            return ChunkMeshDetail.Full;
        }

        public static bool NeedsHigherDetailBuild(Chunk chunk, int chunkDistance, int renderDistance, bool restrictLod = false)
        {
            if (chunk.MeshStale)
            {
                return true;
            }

            var target = SelectRenderTarget(chunkDistance, renderDistance, restrictLod);
            return !chunk.HasMesh(target);
        }

        public static bool ShouldBuildFlora(int chunkDistance, int renderDistance)
        {
            var (lod0Max, _) = GetBandThresholds(renderDistance);
            return chunkDistance <= lod0Max;
        }

        /// <summary>
        /// Picks the best mesh already built for rendering, falling back to coarser LOD while finer meshes build.
        /// </summary>
        public static bool TryGetRenderableDetail(Chunk chunk, ChunkMeshDetail desired, out ChunkMeshDetail actual)
        {
            if (chunk.HasMesh(desired))
            {
                actual = desired;
                return true;
            }

            if (desired == ChunkMeshDetail.Full)
            {
                if (chunk.HasMesh(ChunkMeshDetail.Surface))
                {
                    actual = ChunkMeshDetail.Surface;
                    return true;
                }

                if (chunk.HasMesh(ChunkMeshDetail.Shell))
                {
                    actual = ChunkMeshDetail.Shell;
                    return true;
                }
            }
            else if (desired == ChunkMeshDetail.Surface && chunk.HasMesh(ChunkMeshDetail.Shell))
            {
                actual = ChunkMeshDetail.Shell;
                return true;
            }

            actual = default;
            return false;
        }

        public static int GetChunkDistance(int chunkX, int chunkZ, int agentChunkX, int agentChunkZ)
        {
            int dx = Math.Abs(chunkX - agentChunkX);
            int dz = Math.Abs(chunkZ - agentChunkZ);
            return Math.Max(dx, dz);
        }

        public static float GetFogEnd(int renderDistance, float twilightFactor = 0f)
        {
            float end = MathF.Max(32f, (renderDistance - 1f) * Chunk.Width * 0.88f);
            return end * (1f - twilightFactor * 0.14f);
        }

        public static float GetFogStart(int renderDistance, float twilightFactor = 0f)
        {
            return GetFogEnd(renderDistance, twilightFactor) * 0.35f;
        }

        public static (float start, float end) GetFogRange(int renderDistance, ChunkMeshDetail detail, float twilightFactor = 0f)
        {
            float end = GetFogEnd(renderDistance, twilightFactor);
            float start = GetFogStart(renderDistance, twilightFactor);

            return detail switch
            {
                ChunkMeshDetail.Surface => (start * 0.8f, end * 0.94f),
                ChunkMeshDetail.Shell => (start * 0.5f, end * 0.76f),
                _ => (start, end)
            };
        }

        public static float GetProjectionFarPlane(int renderDistance)
        {
            return GetFogEnd(renderDistance) + Chunk.Width * 2f;
        }

        public static float GetAnimalCullRadius(int renderDistance)
        {
            return renderDistance * Chunk.Width;
        }
    }
}
