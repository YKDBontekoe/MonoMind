using System;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class PerfCommand : IDevCommand
    {
        public string Name => "perf";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            return string.Join("\n", new[]
            {
                "PERF COUNTERS:",
                $"  FPS (rolling): {RuntimeMetrics.RollingFps:F1}",
                $"  UpdateMs: {PerfCounters.LastUpdateMs:F2}",
                $"  DrawMs: {PerfCounters.LastDrawMs:F2}",
                $"  PeakUpdateMs: {PerfCounters.PeakUpdateMs:F2}",
                $"  PeakDrawMs: {PerfCounters.PeakDrawMs:F2}",
                $"  GetBlockCalls: {PerfCounters.GetBlockCalls}",
                $"  RaycastBlockVisits: {PerfCounters.RaycastBlockVisits}",
                $"  TerrainDrawCalls: {PerfCounters.TerrainDrawCalls}",
                $"  TerrainOpaqueDrawCalls: {PerfCounters.TerrainOpaqueDrawCalls}",
                $"  TerrainWaterDrawCalls: {PerfCounters.TerrainWaterDrawCalls}",
                $"  TerrainCutoutDrawCalls: {PerfCounters.TerrainCutoutDrawCalls}",
                $"  FloraDrawCalls: {PerfCounters.FloraDrawCalls}",
                $"  FloraVertexCount: {PerfCounters.FloraVertexCount}",
                $"  FloraDrawMs: {PerfCounters.FloraDrawMs:F2}",
                $"  RenderDistance: {host.Settings.RenderDistance}",
                $"  StreamRenderDistance: {session.Grid.StreamRenderDistance}",
                $"  PendingMeshCount: {PerfCounters.PendingMeshCount}",
                $"  ChunksMeshedThisFrame: {PerfCounters.ChunksMeshedThisFrame}",
                $"  MeshBuildMs: {PerfCounters.MeshBuildMs:F2}",
                $"  LastFrameMeshBuildMs: {PerfCounters.LastFrameMeshBuildMs:F2}",
                $"  PeakMeshBuildMs: {PerfCounters.PeakMeshBuildMs:F2}",
                $"  ActiveChunks: {session.Grid.ActiveChunkCount}"
            });
        }
    }

    internal sealed class ChunksCommand : IDevCommand
    {
        public string Name => "chunks";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var pos = session.Player.Position;
            int cx = (int)MathF.Floor(pos.X) >> 4;
            int cy = (int)MathF.Floor(pos.Y) >> 4;
            int cz = (int)MathF.Floor(pos.Z) >> 4;
            return $"Active chunks: {session.Grid.ActiveChunkCount} | stream distance: {session.Grid.StreamRenderDistance} | player chunk: ({cx}, {cy}, {cz}) | seed: {session.Grid.Seed}";
        }
    }
}

