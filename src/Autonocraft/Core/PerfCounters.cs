namespace Autonocraft.Core
{
    /// <summary>
    /// Lightweight dev counters for profiling hot paths. Reset once per frame from the game loop.
    /// </summary>
    public static class PerfCounters
    {
        public static int GetBlockCalls;
        public static int RaycastBlockVisits;
        public static int TerrainDrawCalls;
        public static int TerrainOpaqueDrawCalls;
        public static int TerrainWaterDrawCalls;
        public static int TerrainCutoutDrawCalls;
        public static int FloraDrawCalls;
        public static int FloraVertexCount;
        public static float FloraDrawMs;
        public static int PendingMeshCount;
        public static int ChunksMeshedThisFrame;
        public static float MeshBuildMs;
        public static float LastFrameMeshBuildMs;
        public static float PeakMeshBuildMs;
        public static float LastUpdateMs;
        public static float LastDrawMs;
        public static float PeakUpdateMs;
        public static float PeakDrawMs;

        public static bool ShowPerfHud;

        // CPU update breakdowns (ms)
        public static float UpdatePlayerMs;
        public static float UpdateChunksMs;
        public static float UpdateFluidsMs;
        public static float UpdateAnimalsMs;
        public static float UpdateVillagesMs;
        public static float UpdateParticlesMs;

        // Draw breakdowns (ms)
        public static float DrawSkyMs;
        public static float DrawTerrainOpaqueMs;
        public static float DrawTerrainWaterMs;
        public static float DrawTerrainCutoutMs;
        public static float DrawFloraMs;
        public static float DrawEntitiesMs;
        public static float DrawUiMs;

        // Triangle/Index counts
        public static int TerrainOpaqueIndexCount;
        public static int TerrainWaterIndexCount;
        public static int TerrainCutoutIndexCount;

        public static void ResetFrame()
        {
            GetBlockCalls = 0;
            RaycastBlockVisits = 0;
            TerrainDrawCalls = 0;
            TerrainOpaqueDrawCalls = 0;
            TerrainWaterDrawCalls = 0;
            TerrainCutoutDrawCalls = 0;
            FloraDrawCalls = 0;
            FloraVertexCount = 0;
            FloraDrawMs = 0f;
            ChunksMeshedThisFrame = 0;
            MeshBuildMs = 0f;

            TerrainOpaqueIndexCount = 0;
            TerrainWaterIndexCount = 0;
            TerrainCutoutIndexCount = 0;
        }

        public static void RecordUpdate(float milliseconds)
        {
            LastUpdateMs = milliseconds;
            if (milliseconds > PeakUpdateMs)
            {
                PeakUpdateMs = milliseconds;
            }
        }

        public static void RecordDraw(float milliseconds)
        {
            LastDrawMs = milliseconds;
            if (milliseconds > PeakDrawMs)
            {
                PeakDrawMs = milliseconds;
            }
        }

        public static void RecordMeshBuild(float milliseconds)
        {
            ChunksMeshedThisFrame++;
            MeshBuildMs += milliseconds;
            LastFrameMeshBuildMs = milliseconds;
            if (milliseconds > PeakMeshBuildMs)
            {
                PeakMeshBuildMs = milliseconds;
            }
        }
    }
}
