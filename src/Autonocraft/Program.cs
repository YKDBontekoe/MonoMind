using System;
using Autonocraft.Core;

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
            Console.WriteLine("  --skip-menu   Skip main menu and load a world immediately");
            Console.WriteLine("  --agent-port  Agent HTTP API port (default: 5001; macOS often blocks 5000)");
            Console.WriteLine($"  --render-distance  Override render distance for this session ({GameSettings.MinRenderDistance}-{GameSettings.MaxRenderDistance})");
            Console.WriteLine("  --debug-input Trace mouse/focus/streaming to input_debug.log");
            Console.WriteLine("  --debug-metrics Write CPU/memory/frame metrics to metrics.log + /metrics API");
            Console.WriteLine("  --help        Show this help text");
        }

        private static void Main(string[] args)
        {
            bool runTests = false;
            bool skipMenu = false;
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
                else if (arg == "--skip-menu")
                {
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

            if (runTests)
            {
                bool success = GameIntegrationTests.Run();
                Environment.Exit(success ? 0 : 1);
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

            using (var game = new AutonocraftGame(skipMenu: skipMenu, agentPort: agentPort, debugMetrics: debugMetrics, renderDistanceOverride: renderDistanceOverride))
            {
                game.Run();
            }

            RuntimeMetrics.Shutdown();
            InputDebugTrace.Shutdown();
        }
    }
}
