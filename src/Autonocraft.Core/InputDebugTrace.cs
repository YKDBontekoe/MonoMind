using System;
using System.IO;
using System.Text;
using Autonocraft.World;

namespace Autonocraft.Core
{
    /// <summary>
    /// Optional input/streaming trace (--debug-input or AUTONOCRAFT_DEBUG_INPUT=1).
    /// Writes to console and ~/Library/Application Support/Autonocraft/input_debug.log (macOS)
    /// or ~/.local/share/Autonocraft/input_debug.log (Linux).
    /// </summary>
    public static class InputDebugTrace
    {
        private static bool _enabled;
        private static StreamWriter? _writer;
        private static float _heartbeatTimer;
        private static bool _lastActive = true;
        private static bool _lastMouseLocked = true;
        private static GameState _lastState = GameState.MainMenu;
        private static float _lastWarmup = -1f;

        public static bool Enabled => _enabled;

        public static void EnableFromEnvironment()
        {
            string? env = Environment.GetEnvironmentVariable("AUTONOCRAFT_DEBUG_INPUT");
            if (string.Equals(env, "1", StringComparison.Ordinal) ||
                string.Equals(env, "true", StringComparison.OrdinalIgnoreCase))
            {
                Enable();
            }
        }

        public static void Enable(bool fromCli = false)
        {
            if (_enabled)
            {
                return;
            }

            _enabled = true;
            string path = GetLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _writer = new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
            Log($"TRACE ENABLED (cli={fromCli}) -> {path}");
        }

        public static void Shutdown()
        {
            if (!_enabled)
            {
                return;
            }

            Log("TRACE SHUTDOWN");
            _writer?.Dispose();
            _writer = null;
            _enabled = false;
        }

        public static void Log(string message)
        {
            if (!_enabled)
            {
                return;
            }

            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Console.WriteLine($"[InputDebug] {message}");
            _writer?.WriteLine(line);
        }

        public static void LogException(string context, Exception ex)
        {
            Log($"{context}: {ex.GetType().Name}: {ex.Message}");
            if (ex.StackTrace != null)
            {
                foreach (var stackLine in ex.StackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    Log($"  at {stackLine}");
                }
            }
        }

        public static void TickGameplay(
            float deltaTime,
            GameState state,
            bool isActive,
            bool mouseLocked,
            bool shouldCapture,
            bool skipMouseLook,
            int mouseX,
            int mouseY,
            int centerX,
            int centerY,
            float dx,
            float dy,
            float yaw,
            float spawnWarmupRemaining,
            int pendingMesh,
            float meshMs,
            bool overlayBlocking,
            string? overlayName)
        {
            if (!_enabled)
            {
                return;
            }

            if (isActive != _lastActive)
            {
                Log($"FOCUS {(isActive ? "GAINED" : "LOST")} state={state} mouseLocked={mouseLocked}");
                _lastActive = isActive;
            }

            if (mouseLocked != _lastMouseLocked)
            {
                Log($"MOUSE_LOCK {(mouseLocked ? "ON" : "OFF")} active={isActive} shouldCapture={shouldCapture}");
                _lastMouseLocked = mouseLocked;
            }

            if (state != _lastState)
            {
                Log($"GAME_STATE {_lastState} -> {state}");
                _lastState = state;
            }

            if (spawnWarmupRemaining > 0f && _lastWarmup <= 0f)
            {
                Log("SPAWN_WARMUP START");
            }
            else if (spawnWarmupRemaining <= 0f && _lastWarmup > 0f)
            {
                Log("SPAWN_WARMUP END");
            }

            _lastWarmup = spawnWarmupRemaining;

            if (overlayBlocking && overlayName != null)
            {
                Log($"OVERLAY blocking input: {overlayName}");
            }

            _heartbeatTimer += deltaTime;
            if (_heartbeatTimer < 0.5f)
            {
                return;
            }

            _heartbeatTimer = 0f;
            Log(
                $"HEARTBEAT active={isActive} lock={mouseLocked} capture={shouldCapture} skipLook={skipMouseLook} " +
                $"mouse=({mouseX},{mouseY}) center=({centerX},{centerY}) d=({dx:F1},{dy:F1}) yaw={yaw:F1} " +
                $"warmup={spawnWarmupRemaining:F1}s pendingMesh={pendingMesh} meshMs={meshMs:F1} dt={deltaTime * 1000f:F0}ms");
        }

        public static void LogChunkEvent(string message) => Log($"CHUNK {message}");

        private static string GetLogPath()
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(baseDir))
            {
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            }

            return Path.Combine(baseDir, "Autonocraft", "input_debug.log");
        }
    }
}
