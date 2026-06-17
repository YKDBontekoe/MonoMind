using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Autonocraft.Core
{
    /// <summary>
    /// Process and frame metrics for live monitoring and crash diagnosis.
    /// Enable file logging with --debug-metrics or AUTONOCRAFT_DEBUG_METRICS=1.
    /// </summary>
    public static class RuntimeMetrics
    {
        private const int FrameHistorySize = 120;
        private const float FileLogIntervalSeconds = 2f;

        private static readonly object Gate = new();
        private static readonly Process Process = Process.GetCurrentProcess();
        private static readonly float[] FrameHistory = new float[FrameHistorySize];
        private static readonly Stopwatch Uptime = Stopwatch.StartNew();

        private static bool _fileLoggingEnabled;
        private static StreamWriter? _writer;
        private static float _fileLogTimer;
        private static int _frameHistoryCount;
        private static int _frameHistoryIndex;
        private static float _ringSum;
        private static float _lastFrameMs;
        private static float _peakFrameMs;
        private static int _managedExceptionCount;
        private static GameState _gameState = GameState.MainMenu;
        private static int _activeChunks;
        private static int _pendingMesh;
        private static float _meshBuildMs;
        private static float _spawnWarmupRemaining;
        private static float _lastUpdateMs;
        private static float _lastDrawMs;
        private static int _lastTerrainDrawCalls;
        private static TimeSpan _lastCpuTime;
        private static double _lastCpuSampleSeconds;
        private static float _cpuPercent;

        public static bool FileLoggingEnabled => _fileLoggingEnabled;

        public static float RollingFps
        {
            get
            {
                lock (Gate)
                {
                    float avgFrameMs = _frameHistoryCount > 0 ? _ringSum / _frameHistoryCount : 0f;
                    return avgFrameMs > 0f ? 1000f / avgFrameMs : 0f;
                }
            }
        }

        public static string LogPath => Path.Combine(GetAppDataDir(), "Autonocraft", "metrics.log");

        public static void EnableFromEnvironment()
        {
            string? env = Environment.GetEnvironmentVariable("AUTONOCRAFT_DEBUG_METRICS");
            if (string.Equals(env, "1", StringComparison.Ordinal) ||
                string.Equals(env, "true", StringComparison.OrdinalIgnoreCase))
            {
                EnableFileLogging(fromCli: false);
            }
        }

        public static void EnableFileLogging(bool fromCli = false)
        {
            lock (Gate)
            {
                if (_fileLoggingEnabled)
                {
                    return;
                }

                _fileLoggingEnabled = true;
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                _writer = new StreamWriter(LogPath, append: false, Encoding.UTF8) { AutoFlush = true };
                _writer.WriteLine($"# metrics started {DateTime.Now:O} cli={fromCli}");
                Console.WriteLine($"[Metrics] File logging -> {LogPath}");
            }

            _lastCpuTime = Process.TotalProcessorTime;
            _lastCpuSampleSeconds = Uptime.Elapsed.TotalSeconds;
        }

        public static void RecordFrame(
            float deltaTime,
            GameState state,
            int activeChunks,
            int pendingMesh,
            float meshBuildMs,
            float spawnWarmupRemaining,
            float updateMs = 0f,
            float drawMs = 0f,
            int terrainDrawCalls = 0)
        {
            float frameMs = Math.Max(0f, deltaTime * 1000f);

            lock (Gate)
            {
                _lastFrameMs = frameMs;
                _peakFrameMs = Math.Max(_peakFrameMs, frameMs);
                _lastUpdateMs = updateMs;
                _lastDrawMs = drawMs;
                _lastTerrainDrawCalls = terrainDrawCalls;

                if (_frameHistoryCount < FrameHistorySize)
                {
                    _frameHistoryCount++;
                    _ringSum += frameMs;
                    FrameHistory[_frameHistoryIndex] = frameMs;
                    _frameHistoryIndex = (_frameHistoryIndex + 1) % FrameHistorySize;
                }
                else
                {
                    float replaced = FrameHistory[_frameHistoryIndex];
                    _ringSum += frameMs - replaced;
                    FrameHistory[_frameHistoryIndex] = frameMs;
                    _frameHistoryIndex = (_frameHistoryIndex + 1) % FrameHistorySize;
                }

                _gameState = state;
                _activeChunks = activeChunks;
                _pendingMesh = pendingMesh;
                _meshBuildMs = meshBuildMs;
                _spawnWarmupRemaining = spawnWarmupRemaining;

                float avgFrameMs = _frameHistoryCount > 0 ? _ringSum / _frameHistoryCount : 0f;
                PerfCounters.RollingFps = avgFrameMs > 0f ? 1000f / avgFrameMs : 0f;

                double nowSeconds = Uptime.Elapsed.TotalSeconds;
                double elapsed = nowSeconds - _lastCpuSampleSeconds;
                if (elapsed >= 0.25)
                {
                    TimeSpan cpu = Process.TotalProcessorTime;
                    double cpuDeltaMs = (cpu - _lastCpuTime).TotalMilliseconds;
                    _cpuPercent = elapsed > 0
                        ? (float)Math.Clamp(cpuDeltaMs / (elapsed * 1000.0 * Environment.ProcessorCount) * 100.0, 0.0, 999.0)
                        : 0f;
                    _lastCpuTime = cpu;
                    _lastCpuSampleSeconds = nowSeconds;
                }

                if (_fileLoggingEnabled)
                {
                    _fileLogTimer += deltaTime;
                    if (_fileLogTimer >= FileLogIntervalSeconds)
                    {
                        _fileLogTimer = 0f;
                        WriteFileSnapshotLocked();
                    }
                }
            }
        }

        public static void RecordManagedException(string context, Exception ex)
        {
            lock (Gate)
            {
                _managedExceptionCount++;
            }

            CrashLog.Write(context, ex, fatal: false);
        }

        public static string ToJson()
        {
            lock (Gate)
            {
                long workingSet = Process.WorkingSet64;
                long managedHeap = GC.GetTotalMemory(forceFullCollection: false);
                float avgFrameMs = _frameHistoryCount > 0 ? _ringSum / _frameHistoryCount : 0f;
                float fps = avgFrameMs > 0f ? 1000f / avgFrameMs : 0f;

                var sb = new StringBuilder(512);
                sb.Append('{');
                AppendJsonNumber(sb, "uptimeSeconds", Uptime.Elapsed.TotalSeconds, first: true);
                AppendJsonNumber(sb, "fps", fps);
                AppendJsonNumber(sb, "frameMsLast", _lastFrameMs);
                AppendJsonNumber(sb, "frameMsAvg", avgFrameMs);
                AppendJsonNumber(sb, "frameMsPeak", _peakFrameMs);
                AppendJsonNumber(sb, "cpuPercent", _cpuPercent);
                AppendJsonNumber(sb, "memoryWorkingSetMb", workingSet / (1024.0 * 1024.0));
                AppendJsonNumber(sb, "memoryManagedMb", managedHeap / (1024.0 * 1024.0));
                AppendJsonNumber(sb, "gcGen0", GC.CollectionCount(0));
                AppendJsonNumber(sb, "gcGen1", GC.CollectionCount(1));
                AppendJsonNumber(sb, "gcGen2", GC.CollectionCount(2));
                AppendJsonNumber(sb, "managedExceptions", _managedExceptionCount);
                AppendJsonString(sb, "gameState", _gameState.ToString());
                AppendJsonNumber(sb, "activeChunks", _activeChunks);
                AppendJsonNumber(sb, "pendingMesh", _pendingMesh);
                AppendJsonNumber(sb, "meshBuildMs", _meshBuildMs);
                AppendJsonNumber(sb, "updateMs", _lastUpdateMs);
                AppendJsonNumber(sb, "drawMs", _lastDrawMs);
                AppendJsonNumber(sb, "terrainDrawCalls", _lastTerrainDrawCalls);
                AppendJsonNumber(sb, "peakUpdateMs", PerfCounters.PeakUpdateMs);
                AppendJsonNumber(sb, "peakDrawMs", PerfCounters.PeakDrawMs);
                AppendJsonNumber(sb, "spawnWarmupRemaining", _spawnWarmupRemaining);
                AppendJsonNumber(sb, "peakMeshBuildMs", PerfCounters.PeakMeshBuildMs);
                AppendJsonNumber(sb, "cpuUpdatePlayerMs", PerfCounters.UpdatePlayerMs);
                AppendJsonNumber(sb, "cpuUpdateChunksMs", PerfCounters.UpdateChunksMs);
                AppendJsonNumber(sb, "cpuUpdateFluidsMs", PerfCounters.UpdateFluidsMs);
                AppendJsonNumber(sb, "cpuUpdateAnimalsMs", PerfCounters.UpdateAnimalsMs);
                AppendJsonNumber(sb, "cpuUpdateVillagesMs", PerfCounters.UpdateVillagesMs);
                AppendJsonNumber(sb, "cpuUpdateParticlesMs", PerfCounters.UpdateParticlesMs);
                AppendJsonNumber(sb, "gpuDrawSkyMs", PerfCounters.DrawSkyMs);
                AppendJsonNumber(sb, "gpuDrawTerrainOpaqueMs", PerfCounters.DrawTerrainOpaqueMs);
                AppendJsonNumber(sb, "gpuDrawTerrainWaterMs", PerfCounters.DrawTerrainWaterMs);
                AppendJsonNumber(sb, "gpuDrawTerrainCutoutMs", PerfCounters.DrawTerrainCutoutMs);
                AppendJsonNumber(sb, "gpuDrawFloraMs", PerfCounters.DrawFloraMs);
                AppendJsonNumber(sb, "gpuDrawEntitiesMs", PerfCounters.DrawEntitiesMs);
                AppendJsonNumber(sb, "gpuDrawUiMs", PerfCounters.DrawUiMs);
                AppendJsonNumber(sb, "terrainOpaqueDrawCalls", PerfCounters.TerrainOpaqueDrawCalls);
                AppendJsonNumber(sb, "terrainWaterDrawCalls", PerfCounters.TerrainWaterDrawCalls);
                AppendJsonNumber(sb, "terrainCutoutDrawCalls", PerfCounters.TerrainCutoutDrawCalls);
                AppendJsonNumber(sb, "floraDrawCalls", PerfCounters.FloraDrawCalls);
                AppendJsonNumber(sb, "floraVertexCount", PerfCounters.FloraVertexCount);
                AppendJsonNumber(sb, "terrainOpaqueTriangles", PerfCounters.TerrainOpaqueIndexCount / 3);
                AppendJsonNumber(sb, "terrainWaterTriangles", PerfCounters.TerrainWaterIndexCount / 3);
                AppendJsonNumber(sb, "terrainCutoutTriangles", PerfCounters.TerrainCutoutIndexCount / 3);
                AppendJsonString(sb, "metricsLogPath", _fileLoggingEnabled ? LogPath : "");
                AppendJsonString(sb, "latestCrashLog", CrashLog.LatestPath ?? "");
                sb.Append('}');
                return sb.ToString();
            }
        }

        public static void Shutdown()
        {
            lock (Gate)
            {
                if (_writer == null)
                {
                    return;
                }

                WriteFileSnapshotLocked();
                _writer.WriteLine($"# metrics shutdown {DateTime.Now:O}");
                _writer.Dispose();
                _writer = null;
                _fileLoggingEnabled = false;
            }
        }

        private static void WriteFileSnapshotLocked()
        {
            _writer?.WriteLine(
                $"{DateTime.Now:HH:mm:ss.fff} {ToJson()}");
        }

        private static void AppendJsonNumber(StringBuilder sb, string key, double value, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(key).Append("\":");
            sb.Append(value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void AppendJsonString(StringBuilder sb, string key, string value)
        {
            sb.Append(',');
            sb.Append('"').Append(key).Append("\":");
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }

        private static string GetAppDataDir()
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(baseDir))
            {
                return baseDir;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share");
        }
    }
}
