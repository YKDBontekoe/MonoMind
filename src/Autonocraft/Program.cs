using System;
using Autonocraft.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autonocraft
{
    internal class Program
    {
        private static void PrintHelp()
        {
            Console.WriteLine("Autonocraft — voxel sandbox game");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run --project src/Autonocraft -- [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --test        Run headless integration tests and exit");
            Console.WriteLine("  --bench       Run performance benchmarks and exit (brief windowed GPU pass)");
            Console.WriteLine("  --skip-menu   Skip main menu and load a world immediately");
            Console.WriteLine("  --structure-gallery  Skip menu and load the flat structure showcase world");
            Console.WriteLine("  --agent-port  Agent HTTP API port (default: 5001; macOS often blocks 5000)");
            Console.WriteLine($"  --render-distance  Override render distance for this session ({GameSettings.MinRenderDistance}-{GameSettings.MaxRenderDistance})");
            Console.WriteLine("  --debug-input Trace mouse/focus/streaming to input_debug.log");
            Console.WriteLine("  --debug-metrics Write CPU/memory/frame metrics to metrics.log + /metrics API");
            Console.WriteLine("  --help        Show this help text");
        }

        private static void Main(string[] args)
        {
            bool runTests = false;
            bool runBench = false;
            bool skipMenu = false;
            bool structureGallery = false;
            bool debugInput = false;
            bool debugMetrics = false;
            int agentPort = 5001;
            int? renderDistanceOverride = null;

            CrashLog.InstallGlobalHandlers();
            InputDebugTrace.EnableFromEnvironment();
            RuntimeMetrics.EnableFromEnvironment();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "--test")
                {
                    runTests = true;
                }
                else if (arg == "--bench")
                {
                    runBench = true;
                }
                else if (arg == "--skip-menu")
                {
                    skipMenu = true;
                }
                else if (arg == "--structure-gallery")
                {
                    structureGallery = true;
                    skipMenu = true;
                }
                else if (arg == "--debug-input")
                {
                    debugInput = true;
                }
                else if (arg == "--debug-metrics")
                {
                    debugMetrics = true;
                }
                else if (arg == "--agent-port")
                {
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out agentPort))
                    {
                        Console.WriteLine("Error: --agent-port requires a port number.");
                        Environment.Exit(1);
                    }
                }
                else if (arg == "--render-distance")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine($"Error: --render-distance requires a number between {GameSettings.MinRenderDistance} and {GameSettings.MaxRenderDistance}.");
                        Environment.Exit(1);
                    }

                    if (!int.TryParse(args[++i], out int parsedRenderDistance))
                    {
                        Console.WriteLine($"Error: --render-distance requires a number between {GameSettings.MinRenderDistance} and {GameSettings.MaxRenderDistance}.");
                        Environment.Exit(1);
                    }

                    renderDistanceOverride = parsedRenderDistance;
                }
                else if (arg is "--help" or "-h" or "/?")
                {
                    PrintHelp();
                    return;
                }
            }

            using var services = GameServiceProvider.Build(enableConsoleLogging: debugMetrics);
            services.GetService<ILoggerFactory>()?.CreateLogger("Autonocraft")
                ?.LogInformation("Autonocraft starting (tests={RunTests}, bench={RunBench})", runTests, runBench);

            if (runTests)
            {
                bool success = GameIntegrationTests.Run();
                Environment.Exit(success ? 0 : 1);
                return;
            }

            if (runBench)
            {
                using (var bench = new PerformanceBenchmarkGame())
                {
                    bench.Run();
                }

                RuntimeMetrics.Shutdown();
                InputDebugTrace.Shutdown();
                return;
            }

            Console.WriteLine("---------------------------------------------");
            Console.WriteLine("Autonocraft — voxel sandbox game");
            Console.WriteLine("---------------------------------------------");

            if (debugInput)
            {
                InputDebugTrace.Enable(fromCli: true);
            }

            if (debugMetrics)
            {
                RuntimeMetrics.EnableFileLogging(fromCli: true);
            }

            using (var game = new AutonocraftGame(skipMenu: skipMenu, structureGallery: structureGallery, agentPort: agentPort, debugMetrics: debugMetrics, renderDistanceOverride: renderDistanceOverride))
            {
                game.Run();
            }

            RuntimeMetrics.Shutdown();
            InputDebugTrace.Shutdown();
        }
    }
}
