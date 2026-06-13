using System;
using Autonocraft.Core;

namespace Autonocraft
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("---------------------------------------------");
            Console.WriteLine("Autonocraft: 3D Voxel Simulation with Local LLM");
            Console.WriteLine("---------------------------------------------");

            bool runTests = false;
            bool skipMenu = false;

            foreach (var arg in args)
            {
                if (arg == "--test")
                {
                    runTests = true;
                }
                else if (arg == "--skip-menu")
                {
                    skipMenu = true;
                }
            }

            if (runTests)
            {
                bool success = GameIntegrationTests.Run();
                Environment.Exit(success ? 0 : 1);
                return;
            }

            // Standard run path: launches the MonoGame application
            using (var game = new AutonocraftGame(skipMenu: skipMenu))
            {
                game.Run();
            }
        }
    }
}
