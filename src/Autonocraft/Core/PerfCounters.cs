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
        public static int PendingMeshCount;
        public static int ChunksMeshedThisFrame;
        public static float MeshBuildMs;
        public static float LastFrameMeshBuildMs;
        public static float PeakMeshBuildMs;
        public static float LastUpdateMs;
        public static float LastDrawMs;
        public static float PeakUpdateMs;
        public static float PeakDrawMs;

        public static void ResetFrame()
        {
            GetBlockCalls = 0;
            RaycastBlockVisits = 0;
            TerrainDrawCalls = 0;
            ChunksMeshedThisFrame = 0;
            MeshBuildMs = 0f;
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
