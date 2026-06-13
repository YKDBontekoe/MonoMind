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

        public static int GetChunkDistance(int chunkX, int chunkZ, int agentChunkX, int agentChunkZ)
        {
            int dx = Math.Abs(chunkX - agentChunkX);
            int dz = Math.Abs(chunkZ - agentChunkZ);
            return Math.Max(dx, dz);
        }

        public static float GetFogEnd(int renderDistance)
        {
            // End fog before the outer loaded ring so distant block textures dissolve into the sky.
            return MathF.Max(32f, (renderDistance - 1f) * Chunk.Width * 0.88f);
        }

        public static float GetFogStart(int renderDistance)
        {
            return GetFogEnd(renderDistance) * 0.35f;
        }

        public static (float start, float end) GetFogRange(int renderDistance, ChunkMeshDetail detail)
        {
            float end = GetFogEnd(renderDistance);
            float start = GetFogStart(renderDistance);

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
