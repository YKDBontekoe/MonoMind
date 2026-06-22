using System;
using Autonocraft.Core;
using Autonocraft.Domain.World;
using Autonocraft.Engine;
using Autonocraft.World;

namespace Autonocraft.Tests.Integration
{
    public static class SnowSystemTests
    {
        public static void RunSnowAccumulationAndMeltingTests(GameSession session, Player player, VoxelWorld world)
        {
            Console.Write("Running Snow Accumulation and Melting Tests... ");

            // 1. Verify block extension level helpers
            if (BlockType.SnowLayer1.GetSnowLevel() != 1 ||
                BlockType.SnowLayer9.GetSnowLevel() != 9 ||
                BlockType.SnowSlab.GetSnowLevel() != 5 ||
                BlockType.Snow.GetSnowLevel() != 10)
            {
                throw new Exception("GetSnowLevel did not return the expected values.");
            }

            if (BlockTypeExtensions.GetSnowBlockTypeForLevel(1) != BlockType.SnowLayer1 ||
                BlockTypeExtensions.GetSnowBlockTypeForLevel(5) != BlockType.SnowSlab ||
                BlockTypeExtensions.GetSnowBlockTypeForLevel(10) != BlockType.Snow)
            {
                throw new Exception("GetSnowBlockTypeForLevel did not return the expected values.");
            }

            // 2. Clear any block at a test position and set grass at y=64
            int tx = (int)player.Position.X + 4;
            int tz = (int)player.Position.Z + 4;
            Console.WriteLine("\n[DEBUG] Active chunks count: " + world.ActiveChunks.Count);
            foreach (var chunk in world.ActiveChunks)
            {
                Console.WriteLine($"[DEBUG] Active chunk: ({chunk.ChunkX}, {chunk.ChunkZ})");
            }
            world.SetBlock(tx, 64, tz, BlockType.Grass, null);
            for (int y = 65; y < Chunk.Height; y++)
            {
                world.SetBlock(tx, y, tz, BlockType.Air, null);
            }

            var snowSystem = new SnowSystem();
            var weather = new WeatherSystem();
            weather.ForceWeather(WeatherKind.Thunderstorm);
            weather.Update(20f); // Advance weather transition to reach full intensity
            weather.TemperatureOffset = -10f; // freezing

            float timeOfDay = 0.5f;

            Console.WriteLine($"\n[DEBUG] Immediately after SetBlock: y=64 is {world.GetBlock(tx, 64, tz)}, y=65 is {world.GetBlock(tx, 65, tz)}");
            Console.WriteLine($"[DEBUG] tx={tx}, tz={tz}, BaseTemp={world.SampleBiome(tx, tz).Temperature:F2}, Temp={snowSystem.GetTemperature(world, weather, tx, 64, tz, timeOfDay):F2}, RainIntensity={weather.RainIntensity:F2}, Weather={weather.CurrentWeather}");

            // 3. Test Snow Accumulation
            // Call UpdateColumn repeatedly until we get SnowLayer1 at y=65
            int maxAttempts = 100;
            int attempts = 0;
            while (world.GetBlock(tx, 65, tz) == BlockType.Air && attempts < maxAttempts)
            {
                snowSystem.UpdateColumn(world, weather, tx, tz, timeOfDay);
                attempts++;
            }

            if (world.GetBlock(tx, 65, tz) != BlockType.SnowLayer1)
            {
                throw new Exception($"Expected SnowLayer1 at y=65 after accumulation, got {world.GetBlock(tx, 65, tz)}");
            }

            // Continue accumulation to SnowSlab (level 5)
            attempts = 0;
            while (world.GetBlock(tx, 65, tz) != BlockType.SnowSlab && attempts < maxAttempts)
            {
                snowSystem.UpdateColumn(world, weather, tx, tz, timeOfDay);
                attempts++;
            }

            if (world.GetBlock(tx, 65, tz) != BlockType.SnowSlab)
            {
                throw new Exception($"Expected SnowSlab at y=65 after further accumulation, got {world.GetBlock(tx, 65, tz)}");
            }

            // Continue accumulation to full Snow block (level 10)
            attempts = 0;
            while (world.GetBlock(tx, 65, tz) != BlockType.Snow && attempts < maxAttempts)
            {
                snowSystem.UpdateColumn(world, weather, tx, tz, timeOfDay);
                attempts++;
            }

            if (world.GetBlock(tx, 65, tz) != BlockType.Snow)
            {
                throw new Exception($"Expected full Snow block at y=65, got {world.GetBlock(tx, 65, tz)}");
            }

            // Now test building snow on top of the full snow block (y=66)
            attempts = 0;
            while (world.GetBlock(tx, 66, tz) == BlockType.Air && attempts < maxAttempts)
            {
                snowSystem.UpdateColumn(world, weather, tx, tz, timeOfDay);
                attempts++;
            }

            if (world.GetBlock(tx, 66, tz) != BlockType.SnowLayer1)
            {
                throw new Exception($"Expected SnowLayer1 on top of full Snow block at y=66, got {world.GetBlock(tx, 66, tz)}");
            }

            // 4. Test Snow Melting
            // Set temperature to warm
            weather.TemperatureOffset = 15f; // warm
            
            // Melt the layer at y=66 back to air
            attempts = 0;
            while (world.GetBlock(tx, 66, tz) != BlockType.Air && attempts < maxAttempts)
            {
                snowSystem.UpdateColumn(world, weather, tx, tz, timeOfDay);
                attempts++;
            }

            if (world.GetBlock(tx, 66, tz) != BlockType.Air)
            {
                throw new Exception($"Expected y=66 to melt back to Air, got {world.GetBlock(tx, 66, tz)}");
            }

            // Melt y=65 full Snow block back to Air
            attempts = 0;
            while (world.GetBlock(tx, 65, tz) != BlockType.Air && attempts < maxAttempts)
            {
                snowSystem.UpdateColumn(world, weather, tx, tz, timeOfDay);
                attempts++;
            }

            if (world.GetBlock(tx, 65, tz) != BlockType.Air)
            {
                throw new Exception($"Expected y=65 to melt completely to Air, got {world.GetBlock(tx, 65, tz)}");
            }

            // 5. Test Flora Replacement
            // Place TallGrass at y=65
            world.SetBlock(tx, 64, tz, BlockType.Grass, null);
            world.SetBlock(tx, 65, tz, BlockType.TallGrass, null);

            weather.TemperatureOffset = -10f; // freezing again

            attempts = 0;
            while (world.GetBlock(tx, 65, tz) == BlockType.TallGrass && attempts < maxAttempts)
            {
                snowSystem.UpdateColumn(world, weather, tx, tz, timeOfDay);
                attempts++;
            }

            if (world.GetBlock(tx, 65, tz) != BlockType.SnowLayer1)
            {
                throw new Exception($"Expected TallGrass to be replaced with SnowLayer1, got {world.GetBlock(tx, 65, tz)}");
            }

            // 6. Test Storm Weather properties and accumulation
            weather.ForceWeather(WeatherKind.Storm);
            weather.Update(20f); // Reach transition completion
            weather.TemperatureOffset = -10f; // freezing

            if (weather.CurrentWeather != WeatherKind.Storm)
            {
                throw new Exception("Expected CurrentWeather to be Storm.");
            }
            if (weather.RainIntensity < 1.3f)
            {
                throw new Exception($"Expected RainIntensity to be around 1.4 for Storm, got {weather.RainIntensity}");
            }
            if (weather.WindIntensity < 1.5f)
            {
                throw new Exception($"Expected WindIntensity to be around 1.6 for Storm, got {weather.WindIntensity}");
            }

            // Clear position again
            world.SetBlock(tx, 64, tz, BlockType.Grass, null);
            for (int y = 65; y < Chunk.Height; y++)
            {
                world.SetBlock(tx, y, tz, BlockType.Air, null);
            }

            // Verify accumulation works under Storm
            attempts = 0;
            while (world.GetBlock(tx, 65, tz) == BlockType.Air && attempts < maxAttempts)
            {
                snowSystem.UpdateColumn(world, weather, tx, tz, timeOfDay);
                attempts++;
            }

            if (world.GetBlock(tx, 65, tz) != BlockType.SnowLayer1)
            {
                throw new Exception($"Expected SnowLayer1 at y=65 after storm accumulation, got {world.GetBlock(tx, 65, tz)}");
            }

            Console.WriteLine("PASSED");
        }
    }
}
