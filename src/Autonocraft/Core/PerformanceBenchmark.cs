using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Autonocraft.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.Core
{
    /// <summary>
    /// Headless-ish performance benchmarks for world load, mesh build, and hot-path queries.
    /// Invoked via <c>dotnet run --project src/Autonocraft -- --bench</c>.
    /// </summary>
    internal static class PerformanceBenchmark
    {
        private const int BenchmarkSeed = 42_424;
        private static readonly Vector3 SpawnPos = new(16.5f, 80f, 16.5f);

        public static int Run(GraphicsDevice? device)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("==================================================================");
            sb.AppendLine("AUTONOCRAFT PERFORMANCE BENCHMARK");
            sb.AppendLine($"Seed {BenchmarkSeed} | CPU cores {Environment.ProcessorCount} | GPU {(device == null ? "none (CPU-only suites)" : "available")}");
            sb.AppendLine("==================================================================");

            RunTerrainGeneration(sb);
            RunSingleChunkWorldGen(sb);
            RunMeshBuildCpu(sb);
            RunGetBlockThroughput(sb);

            if (device != null)
            {
                RunInitialLoad(sb, device);
                RunStreamingStep(sb, device);
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("[SKIP] GPU suites — no GraphicsDevice (run via --bench with windowed host).");
            }

            sb.AppendLine("==================================================================");
            Console.Write(sb.ToString());
            return 0;
        }

        private static void RunTerrainGeneration(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- Terrain generation (async, no mesh) ---");
            sb.AppendLine("RD | Chunks | Terrain ms | ms/chunk");
            foreach (int rd in new[] { 4, 8, 12, 16 })
            {
                int expected = (rd * 2 + 1) * (rd * 2 + 1);
                using var world = new VoxelWorld(BenchmarkSeed);

                var sw = Stopwatch.StartNew();
                PumpTerrainOnly(world, SpawnPos, rd, expected);
                sw.Stop();

                int loaded = world.ActiveChunkCount;
                double msPer = loaded > 0 ? sw.Elapsed.TotalMilliseconds / loaded : 0;
                sb.AppendLine($" {rd,2} | {loaded,6} / {expected,-6} | {sw.Elapsed.TotalMilliseconds,9:F1} | {msPer,7:F2}");
            }
        }

        private static void RunSingleChunkWorldGen(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- Single-chunk world generation (25 chunks, RD=2 area) ---");
            var generator = new WorldGenerator(BenchmarkSeed);
            double totalMs = 0;
            int count = 0;
            for (int cx = 0; cx < 5; cx++)
            {
                for (int cz = 0; cz < 5; cz++)
                {
                    var chunk = new Chunk(cx, cz);
                    var sw = Stopwatch.StartNew();
                    generator.GenerateChunkTerrain(chunk, null);
                    sw.Stop();
                    totalMs += sw.Elapsed.TotalMilliseconds;
                    count++;
                    chunk.Dispose();
                }
            }

            sb.AppendLine($" Avg {totalMs / count:F2} ms/chunk | total {totalMs:F0} ms for {count} chunks");
        }

        private static void RunMeshBuildCpu(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- CPU mesh build (BuildMeshCpuOnly, RD=2, 25 chunks) ---");
            using var world = new VoxelWorld(BenchmarkSeed);
            int expected = 25;
            PumpTerrainOnly(world, SpawnPos, 2, expected);

            double shellTotal = 0;
            double fullTotal = 0;
            int count = 0;
            foreach (var chunk in world.GetActiveChunks())
            {
                if (!world.TryCreateMeshBuildContext(chunk, out var context))
                {
                    continue;
                }

                var shellSw = Stopwatch.StartNew();
                chunk.BuildMeshCpuOnly(context, ChunkMeshDetail.Shell, buildFlora: false);
                shellSw.Stop();

                var fullSw = Stopwatch.StartNew();
                chunk.BuildMeshCpuOnly(context, ChunkMeshDetail.Full, buildFlora: false);
                fullSw.Stop();

                shellTotal += shellSw.Elapsed.TotalMilliseconds;
                fullTotal += fullSw.Elapsed.TotalMilliseconds;
                count++;
            }

            if (count == 0)
            {
                sb.AppendLine(" (no chunks)");
                return;
            }

            sb.AppendLine($" Shell avg {shellTotal / count:F2} ms | Full avg {fullTotal / count:F2} ms | ratio {shellTotal / fullTotal:P0}");
        }

        private static void RunGetBlockThroughput(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- GetBlock throughput (5M calls, RD=8) ---");
            using var world = new VoxelWorld(BenchmarkSeed);
            int expected = (8 * 2 + 1) * (8 * 2 + 1);
            PumpTerrainOnly(world, SpawnPos, 8, expected);

            const int calls = 5_000_000;
            int baseX = 16;
            int baseZ = 16;

            PerfCounters.GetBlockCalls = 0;
            var seqSw = Stopwatch.StartNew();
            for (int i = 0; i < calls; i++)
            {
                _ = world.GetBlock(baseX, 64 + (i & 3), baseZ);
            }

            seqSw.Stop();

            var rnd = new Random(BenchmarkSeed);
            var randSw = Stopwatch.StartNew();
            for (int i = 0; i < calls; i++)
            {
                _ = world.GetBlock(baseX + rnd.Next(0, 128), 64, baseZ + rnd.Next(0, 128));
            }

            randSw.Stop();

            double seqMps = calls / seqSw.Elapsed.TotalSeconds / 1_000_000;
            double randMps = calls / randSw.Elapsed.TotalSeconds / 1_000_000;
            sb.AppendLine($" Sequential column: {seqMps:F2} M calls/s ({seqSw.Elapsed.TotalMilliseconds:F0} ms)");
            sb.AppendLine($" Random XZ:       {randMps:F2} M calls/s ({randSw.Elapsed.TotalMilliseconds:F0} ms)");
        }

        private static void RunInitialLoad(StringBuilder sb, GraphicsDevice device)
        {
            sb.AppendLine();
            sb.AppendLine("--- Full initial load (terrain + GPU mesh, LoadingScreen parity) ---");
            sb.AppendLine("RD | Chunks | Load ms | ms/chunk | steps");
            foreach (int rd in new[] { 4, 8, 12, 16 })
            {
                int expected = (rd * 2 + 1) * (rd * 2 + 1);
                using var world = new VoxelWorld(BenchmarkSeed);
                int chunksPerFrame = rd >= 24 ? 28 :
                    rd >= 16 ? 24 :
                    rd >= 8 ? 22 :
                    12;
                int meshesPerFrame = VoxelWorld.GetLoadingMeshChunksPerFrame(rd);

                var sw = Stopwatch.StartNew();
                world.BeginInitialLoad(SpawnPos, rd);

                int totalSteps = 0;
                int guard = 0;
                bool done = false;
                while (!done && guard++ < 100_000)
                {
                    var stepTimer = Stopwatch.StartNew();
                    for (int step = 0; step < 24; step++)
                    {
                        totalSteps++;
                        if (world.AdvanceInitialLoad(device, chunksPerFrame, meshesPerFrame, rd, out _, out _))
                        {
                            done = true;
                            break;
                        }

                        if (step > 0 && stepTimer.Elapsed.TotalMilliseconds >= 20.0)
                        {
                            break;
                        }
                    }
                }

                sw.Stop();
                int loaded = world.ActiveChunkCount;
                double msPer = loaded > 0 ? sw.Elapsed.TotalMilliseconds / loaded : 0;
                sb.AppendLine($" {rd,2} | {loaded,6} / {expected,-6} | {sw.Elapsed.TotalMilliseconds,8:F0} | {msPer,7:F2} | {totalSteps,5}");
            }
        }

        private static void RunStreamingStep(StringBuilder sb, GraphicsDevice device)
        {
            sb.AppendLine();
            sb.AppendLine("--- Runtime streaming (teleport 40 chunks, RD=8) ---");
            using var world = new VoxelWorld(BenchmarkSeed);
            var pos = SpawnPos;
            int rd = 8;
            world.BeginInitialLoad(pos, rd);
            int guard = 0;
            while (guard++ < 100_000)
            {
                if (SimulateLoadingFrame(world, device, rd))
                {
                    break;
                }
            }

            double totalMs = 0;
            int frames = 0;
            int peakPending = 0;
            var profile = ChunkStreamingProfile.Stationary(pos);
            for (int i = 0; i < 40; i++)
            {
                pos.X += 16f;
                var frameSw = Stopwatch.StartNew();
                world.UpdateChunksAround(device, pos, rd, profile);
                world.ProcessPendingWork(
                    device,
                    pos,
                    rd,
                    profile,
                    VoxelWorld.GetRuntimeTerrainChunksPerFrame(rd),
                    VoxelWorld.GetRuntimeMeshChunksPerFrame(rd));
                frameSw.Stop();
                totalMs += frameSw.Elapsed.TotalMilliseconds;
                frames++;
                peakPending = Math.Max(peakPending, world.PendingMeshCount);
            }

            sb.AppendLine($" 40 chunk-hop frames: avg {totalMs / frames:F2} ms/frame | peak pending mesh {peakPending}");
        }

        private static bool SimulateLoadingFrame(VoxelWorld world, GraphicsDevice device, int rd)
        {
            int chunksPerFrame = rd >= 8 ? 22 : 12;
            int meshesPerFrame = VoxelWorld.GetLoadingMeshChunksPerFrame(rd);
            var stepTimer = Stopwatch.StartNew();
            for (int step = 0; step < 24; step++)
            {
                if (world.AdvanceInitialLoad(device, chunksPerFrame, meshesPerFrame, rd, out _, out _))
                {
                    return true;
                }

                if (step > 0 && stepTimer.Elapsed.TotalMilliseconds >= 20.0)
                {
                    break;
                }
            }

            return false;
        }

        private static void PumpTerrainOnly(VoxelWorld world, Vector3 agentPos, int rd, int expectedChunks)
        {
            int terrainPerFrame = Math.Max(12, Environment.ProcessorCount * 2);
            int guard = 0;
            while (world.ActiveChunkCount < expectedChunks && guard++ < 100_000)
            {
                world.UpdateChunksAround(null, agentPos, rd);
                world.ProcessPendingWork(
                    null,
                    agentPos,
                    rd,
                    maxTerrainPerFrame: terrainPerFrame,
                    maxMeshPerFrame: 0);
            }
        }
    }

    internal sealed class PerformanceBenchmarkGame : Game
    {
        private bool _ran;

        public PerformanceBenchmarkGame()
        {
            var gdm = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 64,
                PreferredBackBufferHeight = 64,
                IsFullScreen = false,
                HardwareModeSwitch = false,
                SynchronizeWithVerticalRetrace = false,
            };
            IsMouseVisible = true;
            Window.Title = "Autonocraft Benchmark";
        }

        protected override void Update(GameTime gameTime)
        {
            if (!_ran)
            {
                _ran = true;
                int code = PerformanceBenchmark.Run(GraphicsDevice);
                Exit();
                Environment.ExitCode = code;
            }

            base.Update(gameTime);
        }
    }
}
