using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Autonocraft.Core
{
    /// <summary>
    /// Writes fatal and managed exception reports to Application Support/Autonocraft/crashes/.
    /// </summary>
    public static class CrashLog
    {
        private static readonly object Gate = new();
        private static string? _latestPath;

        public static string LogDirectory => Path.Combine(GetAppDataDir(), "Autonocraft", "crashes");

        public static string? LatestPath
        {
            get
            {
                lock (Gate)
                {
                    return _latestPath;
                }
            }
        }

        public static void InstallGlobalHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Write("UnhandledException", ex, fatal: true);
                }
                else
                {
                    WriteMessage("UnhandledException", args.ExceptionObject?.ToString() ?? "unknown", fatal: true);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Write("UnobservedTaskException", args.Exception, fatal: false);
                args.SetObserved();
            };
        }

        public static void Write(string context, Exception ex, bool fatal = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Autonocraft crash report");
            sb.AppendLine($"Time (UTC): {DateTime.UtcNow:O}");
            sb.AppendLine($"Context: {context}");
            sb.AppendLine($"Fatal: {fatal}");
            sb.AppendLine($"RuntimeMetrics: {RuntimeMetrics.ToJson()}");
            sb.AppendLine();
            sb.AppendLine(ex.ToString());

            WriteMessage(context, sb.ToString(), fatal);
        }

        public static void WriteMessage(string context, string message, bool fatal = false)
        {
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(LogDirectory);
                    string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    string prefix = fatal ? "fatal" : "error";
                    string path = Path.Combine(LogDirectory, $"{prefix}_{stamp}_{Sanitize(context)}.log");
                    File.WriteAllText(path, message, Encoding.UTF8);
                    _latestPath = path;
                    Console.WriteLine($"[CrashLog] Wrote {prefix} report -> {path}");
                }
                catch (Exception writeEx)
                {
                    Console.WriteLine($"[CrashLog] Failed to write crash log: {writeEx.Message}");
                    Console.WriteLine(message);
                }
            }
        }

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
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
