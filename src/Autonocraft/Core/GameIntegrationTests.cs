using System;
using System.IO;
using System.Numerics;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public static class GameIntegrationTests
    {
        public static bool Run()
        {
            Console.WriteLine("\n==================================================================");
            Console.WriteLine("RUNNING AUTONOCRAFT AUTOMATED INTEGRATION TESTS");
            Console.WriteLine("==================================================================");

            string? tempSavesDir = null;
            string? tempSettingsDir = null;

            try
            {
                tempSavesDir = Path.Combine(Path.GetTempPath(), "autonocraft-test-saves-" + Guid.NewGuid().ToString("N"));
                tempSettingsDir = Path.Combine(Path.GetTempPath(), "autonocraft-test-settings-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempSavesDir);
                Directory.CreateDirectory(tempSettingsDir);
                WorldSaveManager.SetSavesDirectoryForTests(tempSavesDir);
                GameSettingsManager.SetSettingsDirectoryForTests(tempSettingsDir);

                TestGameSettingsRoundTrip();
                TestChunkLodBands();
                TestChunkLodMeshCounts();
                TestWorldGenerationBasics();

                // Create game instance. No window is opened because we don't call game.Run()
                using (var game = new AutonocraftGame(runTests: true))
                {
                    var player = game.Player;
                    var world = game.Grid;

                    // Initialize chunks around spawn so collision checks work
                    world.UpdateChunksAround(null, player.Position, 2);

                    TestGravityAndCollision(player, world);
                    TestJumping(player, world);
                    TestInventory(player);
                    TestMiningAndPlacing(game, player, world);
                    TestWorldSaveRoundTrip(game, player, world);
                    TestAnimalGravity(world);
                    TestAnimalWanderCollision(world);
                    TestAnimalSpawnCap(world);
                    TestPlayerTakeDamage(player);
                    TestEntityRaycast(world);
                    TestMeleeKillAnimal(game, player, world);
                    TestFallDamage(game, player, world);
                    TestClickPriority(game, player, world);
                }

                Console.WriteLine("\n==================================================================");
                Console.WriteLine("ALL TESTS PASSED SUCCESSFULLY! (EXIT CODE: 0)");
                Console.WriteLine("==================================================================\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nTEST FAILURE: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Console.WriteLine("\n==================================================================");
                Console.WriteLine("TEST SUITE FAILED! (EXIT CODE: 1)");
                Console.WriteLine("==================================================================\n");
                return false;
            }
            finally
            {
                WorldSaveManager.SetSavesDirectoryForTests(null);
                GameSettingsManager.SetSettingsDirectoryForTests(null);
                if (tempSavesDir != null && Directory.Exists(tempSavesDir))
                {
                    Directory.Delete(tempSavesDir, recursive: true);
                }

                if (tempSettingsDir != null && Directory.Exists(tempSettingsDir))
                {
                    Directory.Delete(tempSettingsDir, recursive: true);
                }
            }
        }

        private static void TestGameSettingsRoundTrip()
        {
            Console.Write("Running Game Settings Round-Trip Test... ");

            var settings = new GameSettings { RenderDistance = 8 };
            GameSettingsManager.Save(settings);

            var loaded = GameSettingsManager.Load();
            if (loaded.RenderDistance != 8)
            {
                throw new Exception($"Expected render distance 8, got {loaded.RenderDistance}.");
            }

            loaded.RenderDistance = 99;
            loaded.Clamp();
            if (loaded.RenderDistance != GameSettings.MaxRenderDistance)
            {
                throw new Exception("Expected render distance to clamp to max value.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestChunkLodBands()
        {
            Console.Write("Running Chunk LOD Band Test... ");

            AssertLodDetail(4, 1, ChunkMeshDetail.Full);
            AssertLodDetail(4, 2, ChunkMeshDetail.Surface);
            AssertLodDetail(4, 4, ChunkMeshDetail.Shell);

            AssertLodDetail(6, 2, ChunkMeshDetail.Full);
            AssertLodDetail(6, 4, ChunkMeshDetail.Surface);
            AssertLodDetail(6, 6, ChunkMeshDetail.Shell);

            AssertLodDetail(10, 3, ChunkMeshDetail.Full);
            AssertLodDetail(10, 6, ChunkMeshDetail.Surface);
            AssertLodDetail(10, 10, ChunkMeshDetail.Shell);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void AssertLodDetail(int renderDistance, int chunkDistance, ChunkMeshDetail expected)
        {
            var detail = ChunkLod.SelectDetail(chunkDistance, renderDistance);
            if (detail != expected)
            {
                throw new Exception($"Expected LOD {expected} at distance {chunkDistance} with render distance {renderDistance}, got {detail}.");
            }
        }

        private static void TestWorldGenerationBasics()
        {
            Console.Write("Running World Generation Basics Test... ");

            var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));
            var column = generator.PreviewColumn(16, 16);

            if (column.SurfaceHeight <= WorldConstants.SeaLevel - 20 || column.SurfaceHeight >= Chunk.Height - 10)
            {
                throw new Exception($"Unexpected surface height at spawn preview: {column.SurfaceHeight}");
            }

            AssertLocalSlopePlayable(generator, 16, 16, maxStep: 6);

            using var world = new VoxelWorld(1337, WorldGenParams.ForType(WorldType.Default));
            world.UpdateChunksAround(null, new Vector3(16f, 64f, 16f), 1);

            bool foundOre = false;
            bool foundCave = false;
            int surfaceY = world.GetHighestSolidY(16, 16);
            if (surfaceY < 0 || !world.GetBlock(16, surfaceY, 16).IsSolidForSpawn())
            {
                throw new Exception($"Expected solid land surface at spawn, got Y={surfaceY}.");
            }

            for (int y = 1; y < surfaceY - 2; y++)
            {
                var block = world.GetBlock(16, y, 16);
                if (block == BlockType.Air)
                {
                    foundCave = true;
                }

                if (block is BlockType.CoalOre or BlockType.IronOre or BlockType.GoldOre)
                {
                    foundOre = true;
                }
            }

            if (!foundCave)
            {
                throw new Exception("Expected at least one underground air pocket (cave) near spawn column.");
            }

            if (!foundOre)
            {
                throw new Exception("Expected at least one ore block underground near spawn column.");
            }

            var oceanColumn = generator.PreviewColumn(0, 0);
            if (oceanColumn.Biome.Primary != BiomeType.Ocean && oceanColumn.Biome.Primary != BiomeType.Beach)
            {
                // Biome distribution is seed-dependent; verify determinism instead.
                var repeat = generator.PreviewColumn(0, 0);
                if (repeat.Biome.Primary != oceanColumn.Biome.Primary || repeat.SurfaceHeight != oceanColumn.SurfaceHeight)
                {
                    throw new Exception("Biome/height preview is not deterministic.");
                }
            }

            var riverColumn = FindPreviewColumn(generator, c => c.IsRiver, radius: 256, step: 4);
            if (riverColumn == null)
            {
                throw new Exception("Expected at least one generated river within preview range.");
            }

            if (riverColumn.Value.SurfaceHeight > WorldConstants.SeaLevel + 1)
            {
                throw new Exception($"Expected river bed near sea level, got {riverColumn.Value.SurfaceHeight}.");
            }

            var deepOceanColumn = FindPreviewColumn(generator, c => c.Biome.Primary == BiomeType.Ocean, radius: 256, step: 4);
            if (deepOceanColumn == null)
            {
                throw new Exception("Expected at least one generated ocean within preview range.");
            }

            if (deepOceanColumn.Value.SurfaceHeight > WorldConstants.SeaLevel - 4)
            {
                throw new Exception($"Expected ocean floor below sea level, got {deepOceanColumn.Value.SurfaceHeight}.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static TerrainColumn? FindPreviewColumn(WorldGenerator generator, Func<TerrainColumn, bool> predicate, int radius, int step)
        {
            for (int z = -radius; z <= radius; z += step)
            {
                for (int x = -radius; x <= radius; x += step)
                {
                    var column = generator.PreviewColumn(x, z);
                    if (predicate(column))
                    {
                        return column;
                    }
                }
            }

            return null;
        }

        private static void AssertLocalSlopePlayable(WorldGenerator generator, int wx, int wz, int maxStep)
        {
            int centerHeight = generator.PreviewColumn(wx, wz).SurfaceHeight;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    int neighborHeight = generator.PreviewColumn(wx + dx, wz + dz).SurfaceHeight;
                    int delta = Math.Abs(neighborHeight - centerHeight);
                    if (delta > maxStep)
                    {
                        throw new Exception($"Expected playable local slope near spawn, got height step {delta}.");
                    }
                }
            }
        }

        private static void TestChunkLodMeshCounts()
        {
            Console.Write("Running Chunk LOD Mesh Count Test... ");

            using var world = new VoxelWorld(1337);
            world.UpdateChunksAround(null, new Vector3(16f, 64f, 16f), 1);

            var chunks = world.GetActiveChunks();
            if (chunks.Count == 0)
            {
                throw new Exception("Expected at least one loaded chunk for LOD mesh count test.");
            }

            var chunk = chunks[0];
            int fullCount = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Full);
            int surfaceCount = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Surface);
            int shellCount = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Shell);

            if (shellCount > surfaceCount || surfaceCount > fullCount)
            {
                throw new Exception($"Expected shell <= surface <= full index counts, got shell={shellCount}, surface={surfaceCount}, full={fullCount}.");
            }

            if (fullCount <= 0)
            {
                throw new Exception("Expected full mesh to contain indices.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestGravityAndCollision(Player player, VoxelWorld world)
        {
            Console.WriteLine("Debugging blocks at X=16, Z=16:");
            for (int y = 20; y <= 45; y++)
            {
                BlockType bt = world.GetBlock(16, y, 16);
                if (bt != BlockType.Air)
                {
                    Console.WriteLine($"  Y={y}: {bt}");
                }
            }

            Console.Write("Running Gravity & Collision Test... ");

            int surfaceY = world.GetHighestSolidY(16, 16);
            if (surfaceY < 0)
            {
                throw new Exception("Expected solid ground at spawn for gravity test.");
            }

            float spawnY = surfaceY + 10f;
            while (!EntityCollision.IsSpaceClearAt(world, new Vector3(16.5f, spawnY, 16.5f), Player.Width, Player.Height) && spawnY < Chunk.Height - 4)
            {
                spawnY += 1f;
            }

            player.Position = new Vector3(16.5f, spawnY, 16.5f);
            player.Velocity = Vector3.Zero;
            player.FlyingMode = false;

            if (player.IsGrounded)
            {
                throw new Exception("Player should not be grounded when spawning high in the air.");
            }

            // Let them fall
            float dt = 0.016f; // ~60fps step
            bool fell = false;
            
            // Maximum of 3 seconds of simulation (180 frames)
            for (int i = 0; i < 180 && !player.IsGrounded; i++)
            {
                player.Update(dt, world, Vector3.Zero);
                if (player.Velocity.Y < 0) fell = true;
            }

            if (!fell)
            {
                throw new Exception("Player velocity did not accelerate downwards under gravity.");
            }

            if (!player.IsGrounded)
            {
                throw new Exception($"Player did not land on the ground after falling. Final Y: {player.Position.Y}");
            }

            // Verify they are sitting on a solid block
            int feetX = (int)MathF.Floor(player.Position.X);
            int feetY = (int)MathF.Floor(player.Position.Y - 0.05f); // just below feet
            int feetZ = (int)MathF.Floor(player.Position.Z);
            
            BlockType blockBelow = world.GetBlock(feetX, feetY, feetZ);
            if (blockBelow == BlockType.Air)
            {
                throw new Exception($"Player is grounded but block below is Air at ({feetX}, {feetY}, {feetZ}).");
            }

            // Verify velocity.Y has reset to 0
            if (MathF.Abs(player.Velocity.Y) > 0.001f)
            {
                throw new Exception($"Grounded player should have vertical velocity ~0, but has {player.Velocity.Y}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestJumping(Player player, VoxelWorld world)
        {
            Console.Write("Running Jumping Test... ");

            if (!player.IsGrounded)
            {
                throw new Exception("Player must be grounded to start the jump test.");
            }

            float initialY = player.Position.Y;
            player.Jump();

            if (player.IsGrounded)
            {
                throw new Exception("Player should not be grounded immediately after jumping.");
            }

            if (MathF.Abs(player.Velocity.Y - Player.JumpForce) > 0.001f)
            {
                throw new Exception($"Player jump force velocity should be {Player.JumpForce}, but got {player.Velocity.Y}");
            }

            // Tick a few times to verify they rise
            float dt = 0.016f;
            player.Update(dt, world, Vector3.Zero);
            
            if (player.Position.Y <= initialY)
            {
                throw new Exception($"Player height should have increased after jumping. Initial: {initialY}, Current: {player.Position.Y}");
            }

            // Fall back down
            for (int i = 0; i < 180 && !player.IsGrounded; i++)
            {
                player.Update(dt, world, Vector3.Zero);
            }

            if (!player.IsGrounded)
            {
                throw new Exception("Player should have landed back on the ground after jumping.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestInventory(Player player)
        {
            Console.Write("Running Inventory Collection Test... ");

            // Clear inventory
            for (int i = 0; i < 9; i++)
            {
                player.Hotbar[i] = new Player.InventorySlot { Type = BlockType.Air, Count = 0 };
            }

            // Add Grass
            player.AddToInventory(BlockType.Grass);
            if (player.Hotbar[0].Type != BlockType.Grass || player.Hotbar[0].Count != 1)
            {
                throw new Exception($"Expected slot 1 to contain Grass x1, got {player.Hotbar[0].Type} x{player.Hotbar[0].Count}");
            }

            // Add Grass again
            player.AddToInventory(BlockType.Grass);
            if (player.Hotbar[0].Count != 2)
            {
                throw new Exception($"Expected slot 1 to stack to Grass x2, got count {player.Hotbar[0].Count}");
            }

            // Add Stone
            player.AddToInventory(BlockType.Stone);
            if (player.Hotbar[1].Type != BlockType.Stone || player.Hotbar[1].Count != 1)
            {
                throw new Exception($"Expected slot 2 to contain Stone x1, got {player.Hotbar[1].Type} x{player.Hotbar[1].Count}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestMiningAndPlacing(AutonocraftGame game, Player player, VoxelWorld world)
        {
            Console.Write("Running Mining & Placing Test... ");

            int surfaceY = world.GetHighestSolidY(16, 16);
            if (surfaceY < 0)
            {
                throw new Exception("Could not find terrain surface for mining test.");
            }

            const int targetY = 30;
            for (int y = targetY + 1; y <= targetY + 3; y++)
            {
                world.SetBlock(16, y, 16, BlockType.Air);
            }

            player.FlyingMode = true;
            player.Position = new Vector3(16.5f, targetY + 1.2f, 16.5f);
            player.Velocity = Vector3.Zero;

            world.SetBlock(16, targetY, 16, BlockType.Stone);

            player.Yaw = -90f;
            player.Pitch = -89f;
            game.Camera.Position = player.Position + new Vector3(0f, Player.EyeHeight, 0f);
            game.Camera.Yaw = player.Yaw;
            game.Camera.Pitch = player.Pitch;

            // Mine block below
            game.SimulateClick(MouseButton.Left);

            BlockType minedBlock = world.GetBlock(16, targetY, 16);
            if (minedBlock != BlockType.Air)
            {
                throw new Exception($"Expected block at (16,{targetY},16) to be mined (Air), but is {minedBlock}");
            }

            // Verify player got Stone in inventory
            bool hasStone = false;
            for (int i = 0; i < 9; i++)
            {
                if (player.Hotbar[i].Type == BlockType.Stone && player.Hotbar[i].Count > 0)
                {
                    hasStone = true;
                    // Select this slot for placing
                    player.SelectedSlot = i;
                    break;
                }
            }

            if (!hasStone)
            {
                throw new Exception("Player did not collect Stone block in inventory after mining.");
            }

            // Now place the block back on top of the block at Y=39 (which is the terrain grass floor)
            int initialCount = player.Hotbar[player.SelectedSlot].Count;

            game.SimulateClick(MouseButton.Right);

            BlockType placedBlock = world.GetBlock(16, targetY, 16);
            if (placedBlock != BlockType.Stone)
            {
                throw new Exception($"Expected block at (16,{targetY},16) to be placed (Stone), but is {placedBlock}");
            }

            int finalCount = player.Hotbar[player.SelectedSlot].Count;
            if (finalCount != initialCount - 1)
            {
                throw new Exception($"Expected inventory count to decrease by 1, went from {initialCount} to {finalCount}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestWorldSaveRoundTrip(AutonocraftGame game, Player player, VoxelWorld world)
        {
            Console.Write("Running World Save/Load Round-Trip Test... ");

            const string slotId = "test-world";
            const string slotName = "Test World";

            player.Position = new Vector3(20.5f, 40f, 18.5f);
            player.Velocity = new Vector3(1f, 0f, -2f);
            player.Yaw = 45f;
            player.Pitch = -10f;
            player.FlyingMode = true;
            player.SelectedSlot = 2;
            player.Hotbar[2] = new Player.InventorySlot { Type = BlockType.Stone, Count = 12 };

            world.SetBlock(20, 35, 18, BlockType.Air);
            world.SetBlock(21, 35, 18, BlockType.Dirt);
            world.SetBlock(22, 36, 19, BlockType.Stone);

            game.SetTimeOfDay(0.42f);
            game.TimeScale = 0.02f;
            game.TimePaused = true;

            var saveData = WorldSaveManager.BuildFromGame(slotId, slotName, game, world);
            WorldSaveManager.Save(saveData);

            using var loadedWorld = new VoxelWorld(saveData.Seed);
            var loadedSave = WorldSaveManager.Load(slotId);
            loadedWorld.ApplySaveData(loadedSave);

            loadedWorld.UpdateChunksAround(null, new Vector3(loadedSave.Player.PosX, loadedSave.Player.PosY, loadedSave.Player.PosZ), 2);

            var loadedPlayer = new Player(Vector3.Zero);
            WorldSaveManager.ApplyPlayerSaveData(loadedPlayer, loadedSave.Player);

            if (loadedWorld.GetBlock(20, 35, 18) != BlockType.Air)
            {
                throw new Exception("Expected mined block at (20,35,18) to remain Air after load.");
            }

            if (loadedWorld.GetBlock(21, 35, 18) != BlockType.Dirt)
            {
                throw new Exception("Expected placed Dirt at (21,35,18) after load.");
            }

            if (loadedWorld.GetBlock(22, 36, 19) != BlockType.Stone)
            {
                throw new Exception("Expected placed Stone at (22,36,19) after load.");
            }

            if (MathF.Abs(loadedPlayer.Position.X - 20.5f) > 0.001f ||
                MathF.Abs(loadedPlayer.Position.Y - 40f) > 0.001f ||
                MathF.Abs(loadedPlayer.Position.Z - 18.5f) > 0.001f)
            {
                throw new Exception($"Loaded player position mismatch: {loadedPlayer.Position}");
            }

            if (MathF.Abs(loadedPlayer.Velocity.X - 1f) > 0.001f ||
                MathF.Abs(loadedPlayer.Velocity.Z + 2f) > 0.001f)
            {
                throw new Exception($"Loaded player velocity mismatch: {loadedPlayer.Velocity}");
            }

            if (loadedPlayer.Hotbar[2].Type != BlockType.Stone || loadedPlayer.Hotbar[2].Count != 12)
            {
                throw new Exception("Loaded hotbar slot 3 did not match saved inventory.");
            }

            if (MathF.Abs(loadedSave.Time.TimeOfDay - 0.42f) > 0.001f || !loadedSave.Time.TimePaused)
            {
                throw new Exception("Loaded time state did not match saved values.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestAnimalGravity(VoxelWorld world)
        {
            Console.Write("Running Animal Gravity Test... ");

            var animals = new AnimalManager(world.Seed);
            int surfaceY = world.GetHighestSolidY(16, 16);
            if (surfaceY < 0)
            {
                throw new Exception("Could not find surface for animal gravity test.");
            }

            var animal = animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, surfaceY + 10f, 16.5f), world);
            if (animal == null)
            {
                throw new Exception("Failed to spawn test sheep.");
            }

            float dt = 0.016f;
            for (int i = 0; i < 240 && !animal.IsGrounded; i++)
            {
                animals.Update(dt, world);
            }

            if (!animal.IsGrounded)
            {
                throw new Exception($"Animal did not land after falling. Final Y: {animal.Position.Y}");
            }

            if (!animal.IsGrounded || animal.Position.Y < surfaceY + 0.5f)
            {
                throw new Exception($"Animal did not land on solid ground. Grounded={animal.IsGrounded}, Y={animal.Position.Y}, surface={surfaceY}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestAnimalWanderCollision(VoxelWorld world)
        {
            Console.Write("Running Animal Wander Collision Test... ");

            var animals = new AnimalManager(world.Seed);
            int surfaceY = world.GetHighestSolidY(16, 16);
            if (surfaceY < 0)
            {
                throw new Exception("Could not find surface for animal collision test.");
            }

            for (int y = surfaceY + 1; y <= surfaceY + 5; y++)
            {
                world.SetBlock(16, y, 16, BlockType.Air);
            }

            for (int y = surfaceY + 1; y <= surfaceY + 3; y++)
            {
                world.SetBlock(17, y, 16, BlockType.Stone);
            }

            var animal = animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, surfaceY + 4f, 16.5f), world);
            if (animal == null)
            {
                throw new Exception("Failed to spawn test sheep for collision.");
            }

            animal.WanderDirection = Vector3.Normalize(new Vector3(1f, 0f, 0f));
            animal.WanderDistanceRemaining = 10f;

            float dt = 0.016f;
            for (int i = 0; i < 180; i++)
            {
                animals.Update(dt, world);
            }

            if (animal.Position.X >= 17.1f)
            {
                throw new Exception($"Animal walked through wall. Final X: {animal.Position.X}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestAnimalSpawnCap(VoxelWorld world)
        {
            Console.Write("Running Animal Spawn Cap Test... ");

            var animals = new AnimalManager(world.Seed);
            VoxelWorld.GetChunkCoords(16, 16, out int cx, out int cz, out _, out _);

            animals.TryPopulateChunk(cx, cz, world);
            int countAfterFirst = animals.Count;
            animals.TryPopulateChunk(cx, cz, world);
            int countAfterSecond = animals.Count;

            if (countAfterSecond != countAfterFirst)
            {
                throw new Exception($"Chunk was populated twice: {countAfterFirst} -> {countAfterSecond}");
            }

            if (countAfterFirst > AnimalManager.MaxAnimalsPerChunk)
            {
                throw new Exception($"Chunk spawned too many animals: {countAfterFirst}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestPlayerTakeDamage(Player player)
        {
            Console.Write("Running Player Take Damage Test... ");

            player.Health = 20f;
            player.MaxHealth = 20f;

            if (!player.TakeDamage(5f))
            {
                throw new Exception("Expected first TakeDamage call to succeed.");
            }

            if (MathF.Abs(player.Health - 15f) > 0.001f)
            {
                throw new Exception($"Expected health 15 after damage, got {player.Health}.");
            }

            if (player.TakeDamage(5f))
            {
                throw new Exception("Expected i-frames to block immediate second damage.");
            }

            player.UpdateInvulnerability(Player.InvulnerabilityDuration + 0.1f);

            if (!player.TakeDamage(5f))
            {
                throw new Exception("Expected damage after invulnerability expired.");
            }

            if (MathF.Abs(player.Health - 10f) > 0.001f)
            {
                throw new Exception($"Expected health 10 after second damage, got {player.Health}.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestEntityRaycast(VoxelWorld world)
        {
            Console.Write("Running Entity Raycast Test... ");

            var animals = new AnimalManager(world.Seed);
            int surfaceY = world.GetHighestSolidY(16, 16);
            if (surfaceY < 0)
            {
                throw new Exception("Could not find surface for entity raycast test.");
            }

            for (int y = surfaceY + 1; y <= surfaceY + 5; y++)
            {
                world.SetBlock(16, y, 16, BlockType.Air);
                world.SetBlock(16, y, 20, BlockType.Air);
            }

            var sheep = animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, surfaceY + 1f, 20.5f), world);
            if (sheep == null)
            {
                throw new Exception("Failed to spawn sheep for raycast test.");
            }

            var origin = new Vector3(16.5f, surfaceY + 2f, 16.5f);
            var direction = Vector3.Normalize(new Vector3(0f, 0f, 1f));
            var (hit, distance) = animals.RaycastTarget(origin, direction, BlockInteractionSystem.RaycastRange);

            if (hit != sheep)
            {
                throw new Exception("Raycast did not hit the spawned sheep.");
            }

            if (distance <= 0f || distance > BlockInteractionSystem.RaycastRange)
            {
                throw new Exception($"Unexpected raycast distance: {distance}.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void SyncCamera(AutonocraftGame game, Player player)
        {
            game.Camera.Position = player.Position + new Vector3(0f, Player.EyeHeight, 0f);
            game.Camera.Yaw = player.Yaw;
            game.Camera.Pitch = player.Pitch;
        }

        private static void AimAt(Player player, Vector3 target)
        {
            var eye = player.Position + new Vector3(0f, Player.EyeHeight, 0f);
            var delta = target - eye;
            if (delta == Vector3.Zero)
            {
                return;
            }

            delta = Vector3.Normalize(delta);
            player.Pitch = MathF.Asin(Math.Clamp(delta.Y, -1f, 1f)) * (180f / MathF.PI);
            player.Yaw = MathF.Atan2(delta.Z, delta.X) * (180f / MathF.PI);
        }

        private static void TestMeleeKillAnimal(AutonocraftGame game, Player player, VoxelWorld world)
        {
            Console.Write("Running Melee Kill Animal Test... ");

            int surfaceY = world.GetHighestSolidY(16, 16);
            if (surfaceY < 0)
            {
                throw new Exception("Could not find surface for melee kill test.");
            }

            for (int y = surfaceY + 1; y <= surfaceY + 5; y++)
            {
                world.SetBlock(16, y, 16, BlockType.Air);
                world.SetBlock(16, y, 18, BlockType.Air);
            }

            for (int z = 17; z <= 18; z++)
            {
                for (int y = surfaceY + 1; y <= surfaceY + 3; y++)
                {
                    world.SetBlock(16, y, z, BlockType.Air);
                }
            }

            player.FlyingMode = true;
            player.Position = new Vector3(16.5f, surfaceY + 1.2f, 16.5f);

            var sheep = game.Animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, surfaceY + 1f, 18.5f), world);
            if (sheep == null)
            {
                throw new Exception("Failed to spawn sheep for melee kill test.");
            }

            AimAt(player, sheep.Position + new Vector3(0f, sheep.Stats.Height * 0.5f, 0f));
            SyncCamera(game, player);

            int startCount = game.Animals.Count;
            for (int i = 0; i < 8; i++)
            {
                if (!game.Combat.TryInstantAttack(world, player, game.Animals, game.BlockInteraction, game.Camera.Position, game.Camera.Front))
                {
                    throw new Exception($"Attack {i + 1} failed to connect with sheep.");
                }
            }

            if (game.Animals.Count != startCount - 1)
            {
                throw new Exception($"Expected sheep to be removed. Animals: {startCount} -> {game.Animals.Count}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestFallDamage(AutonocraftGame game, Player player, VoxelWorld world)
        {
            Console.Write("Running Fall Damage Test... ");

            const int targetY = 30;
            for (int y = targetY + 1; y <= targetY + 12; y++)
            {
                world.SetBlock(16, y, 16, BlockType.Air);
            }

            world.SetBlock(16, targetY, 16, BlockType.Stone);

            player.FlyingMode = false;
            player.Health = 20f;
            player.Velocity = Vector3.Zero;
            player.Position = new Vector3(16.5f, targetY + 1.2f, 16.5f);

            float dt = 0.016f;
            for (int i = 0; i < 30; i++)
            {
                player.Update(dt, world, Vector3.Zero);
            }

            player.ResetFallTracking();
            player.Position = new Vector3(16.5f, targetY + 11f, 16.5f);
            player.Velocity = Vector3.Zero;
            SyncCamera(game, player);

            bool landed = false;
            for (int i = 0; i < 600; i++)
            {
                player.Update(dt, world, Vector3.Zero);
                if (player.JustLanded)
                {
                    landed = true;
                    break;
                }
            }

            if (!landed)
            {
                throw new Exception("Player did not land during fall damage test.");
            }

            float healthBefore = player.Health;
            player.ClearInvulnerability();
            game.Combat.Update(
                dt,
                world,
                player,
                game.Animals,
                game.BlockInteraction,
                game.Camera.Position,
                game.Camera.Front,
                leftHeld: false,
                leftPressed: false);

            if (player.Health >= healthBefore)
            {
                throw new Exception($"Expected fall damage. Health stayed at {player.Health}.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }

        private static void TestClickPriority(AutonocraftGame game, Player player, VoxelWorld world)
        {
            Console.Write("Running Click Priority Test... ");

            const int targetY = 30;
            for (int y = targetY + 1; y <= targetY + 3; y++)
            {
                world.SetBlock(16, y, 16, BlockType.Air);
            }

            player.FlyingMode = true;
            player.Position = new Vector3(16.5f, targetY + 1.2f, 16.5f);
            SyncCamera(game, player);

            world.SetBlock(16, targetY, 20, BlockType.Stone);

            for (int y = targetY + 1; y <= targetY + 3; y++)
            {
                world.SetBlock(16, y, 18, BlockType.Air);
            }

            for (int z = 17; z <= 19; z++)
            {
                for (int y = targetY + 1; y <= targetY + 3; y++)
                {
                    world.SetBlock(16, y, z, BlockType.Air);
                }
            }

            var sheep = game.Animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, targetY + 1f, 18.5f), world);
            if (sheep == null)
            {
                throw new Exception("Failed to spawn sheep for click priority test.");
            }

            AimAt(player, sheep.Position + new Vector3(0f, sheep.Stats.Height * 0.5f, 0f));
            SyncCamera(game, player);

            float healthBefore = sheep.Health;
            game.SimulateClick(MouseButton.Left);

            if (world.GetBlock(16, targetY, 20) != BlockType.Stone)
            {
                throw new Exception("Block behind sheep was mined instead of attacking the closer animal.");
            }

            if (MathF.Abs(sheep.Health - (healthBefore - CombatSystem.BareHandDamage)) > 0.001f)
            {
                throw new Exception($"Expected sheep to take damage. Health: {healthBefore} -> {sheep.Health}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }
    }
}
