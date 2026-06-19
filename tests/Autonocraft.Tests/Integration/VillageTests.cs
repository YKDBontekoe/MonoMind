using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autonocraft.Core;
using DevCommands = Autonocraft.Core.DevCommands.DevCommandRouter;
using Autonocraft.Ai;
using Autonocraft.Domain.Core;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Tests.Integration;

public static class VillageTests
{
    private static bool TryFoundTestVillage(
        VillageManager villages,
        VoxelWorld world,
        string name,
        int ax,
        int az,
        out VillageEntity? village)
    {
        world.UpdateChunksAround(null, new Vector3(ax + 0.5f, 64f, az + 0.5f), 2);
        if (PlayerStructureRegistry.TryGet("town_heart", out var heart))
        {
            int ay = StructureFingerprint.FindSurfaceAnchorY(world, ax, az);
            foreach (var block in heart.Template.Blocks)
            {
                world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, BlockType.Air);
            }
        }

        return villages.TryFoundVillage(world, name, ax, az, out village);
    }

    private static void EnsureFlatVillagePad(VoxelWorld world, VillageEntity village, int radius)
    {
        world.UpdateChunksAround(null, village.Center, 3);
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                int x = village.AnchorX + dx;
                int z = village.AnchorZ + dz;
                int targetY = village.AnchorY;
                int top = world.GetHighestSolidY(x, z);
                for (int y = top; y > targetY; y--)
                {
                    world.SetBlock(x, y, z, BlockType.Air);
                }

                world.SetBlock(x, targetY, z, BlockType.Grass);
                if (targetY > 1)
                {
                    world.SetBlock(x, targetY - 1, z, BlockType.Dirt);
                }
            }
        }
    }

    public static void RunInventoryStacking()
    {
        Console.Write("Running Inventory Stacking Test... ");
        var inv = new Inventory(9);
        if (!inv.AddItem(ItemStack.CreateBlock(BlockType.OakLog, 32)))
        {
            throw new Exception("Failed to add blocks.");
        }

        if (inv.CountBlock(BlockType.OakLog) != 32)
        {
            throw new Exception("Block count mismatch.");
        }

        if (!inv.TryConsumeBlock(BlockType.OakLog, 10))
        {
            throw new Exception("Consume failed.");
        }

        if (inv.CountBlock(BlockType.OakLog) != 22)
        {
            throw new Exception("Remaining count wrong.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunBlockActionService()
    {
        Console.Write("Running Block Action Service Test... ");
        var world = new VoxelWorld(4242);
        world.UpdateChunksAround(null, new System.Numerics.Vector3(16.5f, 64f, 16.5f), 1);
        int x = 16;
        int z = 16;
        int y = world.GetHighestSolidY(x, z) + 1;
        world.SetBlock(x, y, z, BlockType.Stone);
        var inv = new Inventory(4);
        if (world.GetBlock(x, y, z) != BlockType.Stone)
        {
            throw new Exception("Setup block missing.");
        }

        if (!BlockActionService.TryBreakBlock(world, x, y, z, inv))
        {
            throw new Exception("Break failed.");
        }

        if (world.GetBlock(x, y, z) != BlockType.Air || inv.CountBlock(BlockType.Stone) != 1)
        {
            throw new Exception("Break result wrong.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunStarterSettlementOnNewWorld(AutonocraftGame game)
    {
        Console.Write("Running Starter Settlement On New World Test... ");
        var session = game.Session;
        var world = session.Grid;
        world.UpdateChunksAround(null, new System.Numerics.Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f), 2);

        session.Villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);

        var village = session.Villages.GetPrimaryVillage();
        if (village == null)
        {
            throw new Exception("Starter village missing.");
        }

        if (village.Population < 2)
        {
            throw new Exception("Expected at least 2 starter villagers.");
        }

        if (village.Storage.CountBlock(BlockType.OakPlank) < 8)
        {
            throw new Exception("Starter storage not seeded.");
        }

        int foodInStorage = 0;
        for (int i = 0; i < village.Storage.SlotCount; i++)
        {
            var stack = village.Storage.GetSlot(i);
            if (stack.IsFood() && stack.FoodId == ItemId.CookedMeat)
            {
                foodInStorage += stack.Count;
            }
        }

        if (foodInStorage < 2)
        {
            throw new Exception("Expected welcome cooked meat rations in starter storage.");
        }

        bool anyWorking = false;
        for (int tick = 0; tick < 240 && !anyWorking; tick++)
        {
            session.Villages.Update(0.05f, world, 0.3f, session.Animals);
            foreach (var villager in session.Villagers.All)
            {
                if (villager.VillageId == village.Id &&
                    villager.CurrentJob != Domain.Village.JobType.Idle &&
                    villager.CurrentJob != Domain.Village.JobType.Sleep)
                {
                    anyWorking = true;
                    break;
                }
            }
        }

        if (!anyWorking)
        {
            throw new Exception("Expected starter villagers to begin working quickly.");
        }

        if (!village.BuildingSites.Any(site => site.BlueprintId == "farm_plot"))
        {
            throw new Exception("Expected pre-queued farm plot at starter settlement.");
        }

        if (!PlayerStructureRegistry.TryGet("town_heart", out var heart))
        {
            throw new Exception("Town heart blueprint missing.");
        }

        bool hasHeartBlock = false;
        foreach (var block in heart.Template.Blocks)
        {
            if (world.GetBlock(village.AnchorX + block.Dx, village.AnchorY + block.Dy, village.AnchorZ + block.Dz) == block.Type)
            {
                hasHeartBlock = true;
                break;
            }
        }

        if (!hasHeartBlock)
        {
            throw new Exception("Town Heart blocks not placed.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunClaimWorldStructure()
    {
        Console.Write("Running Claim World Structure Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(5555);
        world.UpdateChunksAround(null, new System.Numerics.Vector3(32.5f, 64f, 32.5f), 2);

        int ax = 32;
        int az = 32;
        world.UpdateChunksAround(null, new System.Numerics.Vector3(ax + 0.5f, 64f, az + 0.5f), 2);
        int ay = StructureFingerprint.FindSurfaceAnchorY(world, ax, az) - 1;
        var cottage = StructureRegistry.All.First(s => s.Id == "PlainsCottage");
        var biome = world.SampleBiome(ax, az).Primary;
        int placementHash = StructureFingerprint.StructureHashForTests(ax, az, world.Seed, 11);
        int variantSalt = StructurePlacementKeys.VariantSaltForStructure(world.Seed, ax, az, cottage.Id, placementHash);
        var template = cottage.ResolveTemplate(world.Seed, ax, az, variantSalt, biome);
        foreach (var block in template.Blocks)
        {
            world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, BlockType.Air);
        }

        foreach (var block in template.Blocks)
        {
            world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, block.Type);
        }

        if (!StructureFingerprint.TryMatchWorldStructure(world, ax, ay, az, out var matched, out float ratio))
        {
            throw new Exception($"Structure fingerprint failed before claim (ratio={ratio:F2}).");
        }

        if (!villages.TryClaimStructure(world, ax, az, out var village) || village == null)
        {
            throw new Exception("Claim structure failed.");
        }

        if (village.Population < 1)
        {
            throw new Exception("Claimed village should spawn a villager.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunImprovedClaimableStructureAccess()
    {
        Console.Write("Running Improved Claimable Structure Access Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(6601);

        string[] claimableIds = ["PlainsCottage", "VillageOutpost"];
        for (int i = 0; i < claimableIds.Length; i++)
        {
            string id = claimableIds[i];
            int ax = 48 + i * 64;
            int az = 48;
            world.UpdateChunksAround(null, new Vector3(ax + 0.5f, 64f, az + 0.5f), 2);
            int ay = StructureFingerprint.FindSurfaceAnchorY(world, ax, az) - 1;

            var definition = StructureRegistry.All.First(s => s.Id == id);
            var biome = world.SampleBiome(ax, az).Primary;
            int placementHash = StructureFingerprint.StructureHashForTests(ax, az, world.Seed, 11);
            int variantSalt = StructurePlacementKeys.VariantSaltForStructure(world.Seed, ax, az, definition.Id, placementHash);
            var template = definition.ResolveTemplate(world.Seed, ax, az, variantSalt, biome);

            ClearStructureFootprint(world, ax, ay, az, template.FootprintRadius + 1, 10);
            StampTemplate(world, ax, ay, az, template);

            if (!StructureFingerprint.TryMatchWorldStructure(world, ax, ay, az, out _, out float ratio))
            {
                throw new Exception($"{id} fingerprint failed before claim (ratio={ratio:F2}).");
            }

            if (!villages.TryClaimStructure(world, ax, az, out var village) || village == null)
            {
                throw new Exception($"{id} claim failed.");
            }

            if (village.Population < 1)
            {
                throw new Exception($"{id} claim should spawn a villager.");
            }

            foreach (var chest in template.Chests)
            {
                int chestX = ax + chest.Dx;
                int chestY = ay + chest.Dy;
                int chestZ = az + chest.Dz;
                if (world.GetBlock(chestX, chestY, chestZ) != BlockType.Chest)
                {
                    throw new Exception($"{id} chest missing at ({chestX},{chestY},{chestZ}).");
                }

                if (world.GetBlock(chestX, chestY + 1, chestZ) != BlockType.Air)
                {
                    throw new Exception($"{id} chest headroom blocked at ({chestX},{chestY + 1},{chestZ}).");
                }
            }
        }

        Console.WriteLine("PASSED");
    }

    public static void RunStarterSettlementBeforeChunksLoaded()
    {
        Console.Write("Running Starter Settlement Before Chunks Loaded Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(5150);

        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);

        var village = villages.GetPrimaryVillage();
        if (village == null || village.Population < 2)
        {
            throw new Exception("Starter village not created before chunk load.");
        }

        if (village.AnchorY <= 1)
        {
            throw new Exception($"Town Heart anchored underground at Y={village.AnchorY}.");
        }

        var spawnPos = Player.FindSafeSpawnPosition(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        if (spawnPos.Y <= 2f)
        {
            throw new Exception($"Spawn position underground at Y={spawnPos.Y:F1}.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunLiveStyleJobAssignmentHasWorld()
    {
        Console.Write("Running Live-Style Job Assignment World Wiring Test... ");
        var session = new GameSession(9091);
        var world = session.Grid;
        world.UpdateChunksAround(null, new Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f), 2);
        session.Villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);

        var village = session.Villages.GetActiveVillage(new Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f));
        if (village == null)
        {
            throw new Exception("Starter village missing.");
        }

        Villager? worker = null;
        foreach (var villager in session.Villagers.All)
        {
            if (villager.VillageId == village.Id)
            {
                worker = villager;
                break;
            }
        }

        if (worker == null)
        {
            throw new Exception("No villager available for assignment.");
        }

        worker.AssignJob(JobType.Idle, null, null);
        if (!session.Villages.TryAssignJob(village, worker, JobType.Lumber))
        {
            throw new Exception("Live-style Lumber assignment failed without explicit target.");
        }

        if (worker.JobTarget == null)
        {
            throw new Exception("Lumber assignment did not resolve a target.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunFullVillageLifecycleJobs()
    {
        Console.Write("Running Full Village Lifecycle Jobs Test... ");
        var session = new GameSession(9191);
        var world = session.Grid;
        var villages = session.Villages;
        var villagers = session.Villagers;
        var animals = session.Animals;
        world.UpdateChunksAround(null, new Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f), 3);
        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage();
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        villagers.Update(0f, world, new[] { village });
        villages.CreativeMode = true;

        BuildHouseThroughSimulation(villages, villagers, world, animals, village);
        ExerciseFarmJob(villages, villagers, world, animals, village);
        ExerciseMineAndMasonJobs(villages, villagers, world, animals, village);
        ExerciseHaulJob(villages, villagers, world, animals, village);
        ExerciseCraftCookAndHuntJobs(villages, villagers, world, animals, village);

        Console.WriteLine("PASSED");
    }

    public static void RunAgentHttpVillageBridgeE2E()
    {
        Console.Write("Running Agent HTTP Village Bridge E2E Test... ");
        var session = new GameSession(9292);
        var world = session.Grid;
        var villages = session.Villages;
        var villagers = session.Villagers;
        var animals = session.Animals;
        world.UpdateChunksAround(null, new Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f), 3);
        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage() ?? throw new Exception("Starter village missing.");
        session.Player.Position = village.Center + new Vector3(0f, 4f, 0f);
        villagers.Update(0f, world, new[] { village });

        int port = GetFreeTcpPort();
        using var bridge = new TestAgentBridge(session);
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        try
        {
            AgentHttpServer.Start(bridge, port);
            var health = GetJson(http, "/health");
            if (!health.RootElement.GetProperty("ready").GetBoolean())
            {
                throw new Exception("Agent API health was not ready.");
            }

            var state = GetJson(http, "/state");
            if (state.RootElement.GetProperty("village").GetProperty("population").GetInt32() < 2)
            {
                throw new Exception("/state did not expose starter citizens.");
            }

            PostAction(http, "open_village");
            if (!bridge.VillageUiOpen)
            {
                throw new Exception("open_village did not route through bridge.");
            }

            PostAction(http, "close_village");
            if (bridge.VillageUiOpen)
            {
                throw new Exception("close_village did not route through bridge.");
            }

            var lumberjack = FirstCitizen(villagers, village);
            lumberjack.AssignJob(JobType.Idle, null, null);
            PostAction(http, "assign_job", ("villager_id", lumberjack.Id.ToString()), ("job", "Lumber"));
            StepUntil(bridge, 120, () =>
            {
                if (lumberjack.CurrentJob == JobType.Lumber && lumberjack.JobTarget.HasValue)
                {
                    var target = lumberjack.JobTarget.Value;
                    lumberjack.Position = target + new Vector3(0f, 1f, 1.5f);
                    lumberjack.SetAiPhase(VillagerAiPhase.Working);
                }

                return CountInventoryLogs(lumberjack.Inventory) > 0;
            });
            if (CountInventoryLogs(lumberjack.Inventory) == 0)
            {
                throw new Exception("API-assigned lumber job did not produce logs.");
            }

            int beforeRecruit = VillageSettlementHealth.GetLivePopulation(village, villagers);
            PostAction(http, "recruit_villager");
            StepUntil(bridge, 20, () => VillageSettlementHealth.GetLivePopulation(village, villagers) > beforeRecruit);
            if (VillageSettlementHealth.GetLivePopulation(village, villagers) <= beforeRecruit)
            {
                throw new Exception("recruit_villager did not increase population.");
            }

            PostAction(http, "set_creative", ("creative", "true"));
            StepUntil(bridge, 20, () => session.Player.CreativeMode);
            if (!session.Player.CreativeMode)
            {
                throw new Exception("set_creative did not update player through bridge queue.");
            }

            int houseX = village.AnchorX + 7;
            int houseY = village.AnchorY;
            int houseZ = village.AnchorZ;
            ClearBlueprintArea(world, "peasant_house", houseX, houseY, houseZ);
            PostAction(
                http,
                "queue_build",
                ("blueprint_id", "peasant_house"),
                ("anchor_x", houseX.ToString()),
                ("anchor_y", houseY.ToString()),
                ("anchor_z", houseZ.ToString()));
            var debugAfterQueue = GetJson(http, "/village/debug");
            if (debugAfterQueue.RootElement.GetProperty("buildingSites").GetArrayLength() == 0)
            {
                throw new Exception("/village/debug did not expose queued building site.");
            }

            var site = village.GetNearestPendingSite(village.Center) ?? throw new Exception("No pending house site after API queue_build.");
            var builder = FirstIdleOrAnyCitizen(villagers, village);
            PostAction(http, "assign_job", ("villager_id", builder.Id.ToString()), ("job", "Build"));
            StepUntil(bridge, 600, () =>
            {
                builder.Position = new Vector3(houseX + 4.5f, houseY + 1f, houseZ + 4.5f);
                if (builder.CurrentJob == JobType.Build)
                {
                    builder.SetAiPhase(VillagerAiPhase.Working);
                    builder.WorkTimer = 10f;
                }

                return village.HasCompletedBuilding("peasant_house");
            });

            if (!village.HasCompletedBuilding("peasant_house"))
            {
                throw new Exception("API queued and assigned house did not complete.");
            }

            var farm = AddCompletedBuilding(village, "farm_plot", BuildingKind.FarmPlot, 501, village.AnchorX, village.AnchorY, village.AnchorZ + 8);
            PlaceFarmBlocks(world, farm);
            world.SetBlock(farm.AnchorX, farm.AnchorY, farm.AnchorZ, BlockType.Wheat);
            var farmer = villagers.Spawn(village.Id, new Vector3(farm.AnchorX + 1.5f, farm.AnchorY + 1f, farm.AnchorZ + 1.5f), 8101);
            farmer.Role = VillagerRole.Farmer;
            farmer.IsGrounded = true;
            village.RegisterVillager(farmer.Id);
            float foodBefore = village.FoodStock;
            PostAction(http, "assign_job", ("villager_id", farmer.Id.ToString()), ("job", "Farm"));
            StepUntil(bridge, 80, () => village.FoodStock > foodBefore);
            if (village.FoodStock <= foodBefore)
            {
                throw new Exception("API-assigned farmer did not harvest food.");
            }

            var quarry = AddCompletedBuilding(village, "quarry", BuildingKind.Quarry, 502, village.AnchorX + 8, village.AnchorY, village.AnchorZ + 8);
            int mineX = quarry.AnchorX + 3;
            int mineY = quarry.AnchorY;
            int mineZ = quarry.AnchorZ;
            world.SetBlock(mineX, mineY, mineZ, BlockType.Stone);
            var miner = villagers.Spawn(village.Id, new Vector3(mineX + 1.5f, mineY + 1f, mineZ + 0.5f), 8102);
            miner.Role = VillagerRole.Miner;
            miner.IsGrounded = true;
            village.RegisterVillager(miner.Id);
            PostAction(
                http,
                "assign_job",
                ("villager_id", miner.Id.ToString()),
                ("job", "Mine"),
                ("target_x", (mineX + 0.5f).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("target_y", mineY.ToString()),
                ("target_z", (mineZ + 0.5f).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            StepUntil(bridge, 100, () => world.GetBlock(mineX, mineY, mineZ) == BlockType.Air);
            if (CountInventoryBlock(miner.Inventory, BlockType.Stone) == 0)
            {
                throw new Exception("API-assigned miner did not collect stone.");
            }

            var kitchen = AddCompletedBuilding(village, "kitchen", BuildingKind.Kitchen, 503, village.AnchorX - 8, village.AnchorY, village.AnchorZ - 8);
            village.Storage.ExpandSlots(18);
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Carrot, 2));
            var cook = villagers.Spawn(village.Id, village.Center + new Vector3(-1f, 1f, -1f), 8103);
            cook.Role = VillagerRole.Cook;
            cook.IsGrounded = true;
            village.RegisterVillager(cook.Id);
            foodBefore = village.FoodStock;
            PostAction(http, "assign_job", ("villager_id", cook.Id.ToString()), ("job", "Cook"));
            StepUntil(bridge, 80, () =>
            {
                if (cook.CurrentJob == JobType.Cook)
                {
                    cook.Position = village.GetBuildingWorkPosition(kitchen);
                    cook.SetAiPhase(VillagerAiPhase.Working);
                    cook.WorkTimer = 10f;
                }

                return village.FoodStock > foodBefore;
            });
            if (village.FoodStock <= foodBefore)
            {
                throw new Exception("API-assigned cook did not produce food.");
            }
        }
        finally
        {
            AgentHttpServer.Stop();
        }

        Console.WriteLine("PASSED");
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static JsonDocument GetJson(HttpClient http, string path)
    {
        var response = http.GetAsync(path).GetAwaiter().GetResult();
        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"GET {path} failed: {(int)response.StatusCode} {body}");
        }

        return JsonDocument.Parse(body);
    }

    private static JsonDocument PostAction(HttpClient http, string command, params (string key, string value)[] parameters)
    {
        var query = new List<string> { "cmd=" + Uri.EscapeDataString(command) };
        foreach (var (key, value) in parameters)
        {
            query.Add(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value));
        }

        string path = "/action?" + string.Join("&", query);
        var response = http.PostAsync(path, new ByteArrayContent(Array.Empty<byte>())).GetAwaiter().GetResult();
        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"POST {path} failed: {(int)response.StatusCode} {body}");
        }

        var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.GetProperty("success").GetBoolean())
        {
            throw new Exception($"POST {path} returned failure: {body}");
        }

        return doc;
    }

    private static void StepUntil(TestAgentBridge bridge, int maxSteps, Func<bool> condition)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            if (condition())
            {
                return;
            }

            bridge.Step(0.2f);
        }
    }

    private static Villager FirstCitizen(VillagerManager villagers, VillageEntity village)
    {
        foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, villagers))
        {
            return villager;
        }

        throw new Exception("Village has no citizens.");
    }

    private static Villager FirstIdleOrAnyCitizen(VillagerManager villagers, VillageEntity village)
    {
        Villager? fallback = null;
        foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, villagers))
        {
            fallback ??= villager;
            if (villager.CurrentJob == JobType.Idle)
            {
                return villager;
            }
        }

        return fallback ?? throw new Exception("Village has no citizens.");
    }

    private static int CountInventoryBlock(Inventory inventory, BlockType blockType)
    {
        int total = 0;
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var stack = inventory.GetSlot(i);
            if (stack.IsBlock() && stack.BlockType == blockType)
            {
                total += stack.Count;
            }
        }

        return total;
    }

    private static int CountInventoryLogs(Inventory inventory) =>
        CountInventoryBlock(inventory, BlockType.OakLog)
        + CountInventoryBlock(inventory, BlockType.BirchLog)
        + CountInventoryBlock(inventory, BlockType.PineLog)
        + CountInventoryBlock(inventory, BlockType.WillowLog)
        + CountInventoryBlock(inventory, BlockType.PalmLog);

    private sealed class TestAgentBridge : IGameAgentBridge, IDisposable
    {
        private readonly Thread _pumpThread;
        private bool _running = true;

        public TestAgentBridge(GameSession session)
        {
            Host = new GameHostContext(session, renderDistance: 2, settings: new GameSettings())
            {
                TimeOfDay = DayNightCycle.Noon,
                TimeScale = DayNightCycle.DefaultTimeScale
            };

            CurrentGameState = GameState.Playing;
            _pumpThread = new Thread(Pump)
            {
                IsBackground = true,
                Name = "Agent HTTP test bridge pump"
            };
            _pumpThread.Start();
        }

        public GameHostContext Host { get; }
        public GameState CurrentGameState { get; private set; }
        public ConcurrentQueue<Action> PendingActions { get; } = new();
        public HashSet<Key> SimulatedKeys { get; } = new();
        public bool VillageUiOpen { get; private set; }
        public bool ExitRequested { get; private set; }

        public void ReleaseSimulatedKeys() => SimulatedKeys.Clear();

        public void EnqueueAction(Action action, bool runImmediatelyInTests)
        {
            if (runImmediatelyInTests)
            {
                action();
            }
            else
            {
                PendingActions.Enqueue(action);
            }
        }

        public void OpenCrucibleAt(int x, int y, int z, BlockType stationType)
        {
        }

        public string ExecuteDevCommand(string input) => DevCommands.Execute(Host, input);

        public void SyncTimeFromHost()
        {
        }

        public void SyncCameraFromPlayer()
        {
        }

        public Task<byte[]> RequestScreenshotAsync(string? savePath = null)
        {
            byte[] png = Convert.FromHexString("89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000A49444154789C63000100000500010D0A2DB40000000049454E44AE426082");
            if (!string.IsNullOrWhiteSpace(savePath))
            {
                File.WriteAllBytes(savePath, png);
            }

            return Task.FromResult(png);
        }

        public void SimulateClick(MouseButton button)
        {
        }

        public void RequestExit() => ExitRequested = true;
        public void RequestOpenVillageUi() => VillageUiOpen = true;
        public void RequestCloseVillageUi() => VillageUiOpen = false;

        public void SetTimeOfDay(float value) => Host.SetTimeOfDay(value);

        public void SetTimeScale(float scale)
        {
            Host.TimeScale = Math.Max(0f, scale);
            Host.TimePaused = scale <= 0f;
        }

        public bool IsStructureGalleryWorld =>
            StructureGallery.IsGalleryWorld(Host.Session.Grid.GenerationParams.WorldType);

        public WorldType CurrentWorldType => Host.Session.Grid.GenerationParams.WorldType;

        public void RequestLoadStructureGallery()
        {
        }

        public void Step(float deltaTime)
        {
            DrainActions();
            Host.Session.UpdateVillages(deltaTime, Host.TimeOfDay);
            DrainActions();
        }

        public void Dispose()
        {
            _running = false;
            _pumpThread.Join(500);
        }

        private void Pump()
        {
            while (_running)
            {
                if (!DrainActions())
                {
                    Thread.Sleep(1);
                }
            }
        }

        private bool DrainActions()
        {
            bool drained = false;
            while (PendingActions.TryDequeue(out var action))
            {
                action();
                drained = true;
            }

            return drained;
        }
    }

    private static void BuildHouseThroughSimulation(
        VillageManager villages,
        VillagerManager villagers,
        VoxelWorld world,
        AnimalManager animals,
        VillageEntity village)
    {
        int houseX = village.AnchorX + 7;
        int houseZ = village.AnchorZ;
        int houseY = village.AnchorY;
        ClearBlueprintArea(world, "peasant_house", houseX, houseY, houseZ);
        if (!villages.TryQueueBlueprint(world, village, "peasant_house", houseX, houseZ, village.Storage, houseY))
        {
            throw new Exception("Could not queue peasant house.");
        }

        var site = village.GetNearestPendingSite(village.Center);
        if (site == null || site.BlueprintId != "peasant_house")
        {
            throw new Exception("Peasant house site not queued.");
        }

        var builder = villagers.Spawn(village.Id, new Vector3(houseX + 4.5f, houseY + 1f, houseZ + 4.5f), 7101);
        builder.Role = VillagerRole.Builder;
        builder.IsGrounded = true;
        village.RegisterVillager(builder.Id);
        if (!villages.TryAssignJob(village, builder, JobType.Build, buildingSiteId: site.Id))
        {
            throw new Exception("Builder assignment failed.");
        }

        for (int i = 0; i < 420 && !village.HasCompletedBuilding("peasant_house"); i++)
        {
            villages.CreativeMode = true;
            builder.Position = new Vector3(houseX + 4.5f, houseY + 1f, houseZ + 4.5f);
            if (builder.CurrentJob == JobType.Build)
            {
                builder.SetAiPhase(VillagerAiPhase.Working);
                builder.WorkTimer = 10f;
            }

            villages.Update(0.2f, world, DayNightCycle.Noon, animals);
        }

        if (!village.HasCompletedBuilding("peasant_house") || village.PopulationCap < 6)
        {
            throw new Exception("Peasant house was not completed and registered.");
        }
    }

    private static void ClearStructureFootprint(VoxelWorld world, int ax, int ay, int az, int radius, int height)
    {
        for (int x = ax - radius; x <= ax + radius; x++)
        {
            for (int z = az - radius; z <= az + radius; z++)
            {
                for (int y = ay; y <= ay + height; y++)
                {
                    world.SetBlock(x, y, z, BlockType.Air);
                }
            }
        }
    }

    private static void StampTemplate(VoxelWorld world, int ax, int ay, int az, StructureTemplate template)
    {
        foreach (var block in template.Blocks)
        {
            world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, block.Type);
        }

        foreach (var chest in template.Chests)
        {
            world.SetBlock(ax + chest.Dx, ay + chest.Dy, az + chest.Dz, BlockType.Chest);
            world.SetBlock(ax + chest.Dx, ay + chest.Dy + 1, az + chest.Dz, BlockType.Air);
        }
    }

    private static void ExerciseFarmJob(
        VillageManager villages,
        VillagerManager villagers,
        VoxelWorld world,
        AnimalManager animals,
        VillageEntity village)
    {
        var farm = AddCompletedBuilding(village, "farm_plot", BuildingKind.FarmPlot, 201, village.AnchorX, village.AnchorY, village.AnchorZ + 8);
        PlaceFarmBlocks(world, farm);
        world.SetBlock(farm.AnchorX, farm.AnchorY, farm.AnchorZ, BlockType.Wheat);

        var farmer = villagers.Spawn(village.Id, new Vector3(farm.AnchorX + 1.5f, farm.AnchorY + 1f, farm.AnchorZ + 1.5f), 7201);
        farmer.Role = VillagerRole.Farmer;
        farmer.IsGrounded = true;
        village.RegisterVillager(farmer.Id);
        float foodBefore = village.FoodStock;
        if (!villages.TryAssignJob(village, farmer, JobType.Farm, buildingId: farm.Id))
        {
            throw new Exception("Farmer assignment failed.");
        }

        for (int i = 0; i < 80 && village.FoodStock <= foodBefore; i++)
        {
            villages.Update(0.2f, world, DayNightCycle.Noon, animals);
        }

        if (village.FoodStock <= foodBefore)
        {
            throw new Exception("Farmer did not harvest food.");
        }
    }

    private static void ExerciseMineAndMasonJobs(
        VillageManager villages,
        VillagerManager villagers,
        VoxelWorld world,
        AnimalManager animals,
        VillageEntity village)
    {
        var quarry = AddCompletedBuilding(village, "quarry", BuildingKind.Quarry, 202, village.AnchorX + 8, village.AnchorY, village.AnchorZ + 8);
        int stoneX = quarry.AnchorX + 3;
        int stoneY = quarry.AnchorY;
        int stoneZ = quarry.AnchorZ;
        world.SetBlock(stoneX, stoneY, stoneZ, BlockType.Stone);

        var miner = villagers.Spawn(village.Id, new Vector3(stoneX + 1.5f, stoneY + 1f, stoneZ + 0.5f), 7301);
        miner.Role = VillagerRole.Miner;
        miner.IsGrounded = true;
        village.RegisterVillager(miner.Id);
        if (!villages.TryAssignJob(village, miner, JobType.Mine, new Vector3(stoneX + 0.5f, stoneY, stoneZ + 0.5f), buildingId: quarry.Id))
        {
            throw new Exception("Miner assignment failed.");
        }

        for (int i = 0; i < 120 && world.GetBlock(stoneX, stoneY, stoneZ) == BlockType.Stone; i++)
        {
            villages.Update(0.2f, world, DayNightCycle.Noon, animals);
        }

        if (world.GetBlock(stoneX, stoneY, stoneZ) == BlockType.Stone || miner.Inventory.CountBlock(BlockType.Stone) == 0)
        {
            throw new Exception("Miner did not break and collect stone.");
        }

        int masonX = quarry.AnchorX + 4;
        int masonZ = quarry.AnchorZ;
        world.SetBlock(masonX, stoneY, masonZ, BlockType.Cobblestone);
        var mason = villagers.Spawn(village.Id, new Vector3(masonX + 1.5f, stoneY + 1f, masonZ + 0.5f), 7302);
        mason.Role = VillagerRole.Mason;
        mason.IsGrounded = true;
        village.RegisterVillager(mason.Id);
        if (!villages.TryAssignJob(village, mason, JobType.Mason, new Vector3(masonX + 0.5f, stoneY, masonZ + 0.5f), buildingId: quarry.Id))
        {
            throw new Exception("Mason assignment failed.");
        }

        for (int i = 0; i < 120 && world.GetBlock(masonX, stoneY, masonZ) == BlockType.Cobblestone; i++)
        {
            villages.Update(0.2f, world, DayNightCycle.Noon, animals);
        }

        if (world.GetBlock(masonX, stoneY, masonZ) == BlockType.Cobblestone || mason.Inventory.CountBlock(BlockType.Cobblestone) == 0)
        {
            throw new Exception("Mason did not cut stone.");
        }
    }

    private static void ExerciseHaulJob(
        VillageManager villages,
        VillagerManager villagers,
        VoxelWorld world,
        AnimalManager animals,
        VillageEntity village)
    {
        var source = villagers.Spawn(village.Id, village.Center + new Vector3(2f, 1f, 0f), 7401);
        source.Role = VillagerRole.Miner;
        source.IsGrounded = true;
        source.AssignJob(JobType.Mine, source.Position, null);
        village.RegisterVillager(source.Id);
        for (int i = 0; i < source.Inventory.SlotCount; i++)
        {
            source.Inventory.SetSlot(i, ItemStack.CreateBlock(BlockType.Stone, 64));
        }

        var hauler = villagers.Spawn(village.Id, village.Center + new Vector3(1f, 1f, 0f), 7402);
        hauler.Role = VillagerRole.Hauler;
        hauler.IsGrounded = true;
        village.RegisterVillager(hauler.Id);
        int storageBefore = village.Storage.CountBlock(BlockType.Stone);

        for (int i = 0; i < 80 && village.Storage.CountBlock(BlockType.Stone) <= storageBefore; i++)
        {
            if (source.Inventory.CountBlock(BlockType.Stone) > 0 && source.CurrentJob == JobType.Idle)
            {
                source.AssignJob(JobType.Mine, source.Position, null);
            }

            Villager activeHauler = hauler;
            foreach (var citizen in villagers.All)
            {
                if (citizen.VillageId == village.Id && citizen.CurrentJob == JobType.Haul)
                {
                    activeHauler = citizen;
                    break;
                }
            }

            if (activeHauler.CurrentJob == JobType.Haul && !activeHauler.HaulIsDelivering)
            {
                activeHauler.Position = source.Position;
                activeHauler.SetAiPhase(VillagerAiPhase.Working);
            }
            else if (activeHauler.CurrentJob == JobType.Haul && activeHauler.HaulIsDelivering)
            {
                activeHauler.Position = village.StoragePosition;
                activeHauler.SetAiPhase(VillagerAiPhase.Working);
            }

            villages.Update(0.2f, world, DayNightCycle.Noon, animals);
        }

        if (village.Storage.CountBlock(BlockType.Stone) <= storageBefore)
        {
            throw new Exception(
                $"Hauler did not deliver carried goods to storage. " +
                $"hauler={hauler.CurrentJob}/{hauler.AiPhase}/delivering={hauler.HaulIsDelivering}/inv={hauler.Inventory.CountBlock(BlockType.Stone)}, " +
                $"source={source.CurrentJob}/{source.AiPhase}/inv={source.Inventory.CountBlock(BlockType.Stone)}, " +
                $"storageBefore={storageBefore}, storageAfter={village.Storage.CountBlock(BlockType.Stone)}");
        }
    }

    private static void ExerciseCraftCookAndHuntJobs(
        VillageManager villages,
        VillagerManager villagers,
        VoxelWorld world,
        AnimalManager animals,
        VillageEntity village)
    {
        var workshop = AddCompletedBuilding(village, "workshop", BuildingKind.Workshop, 203, village.AnchorX - 8, village.AnchorY, village.AnchorZ);
        village.Storage.ExpandSlots(18);
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakLog, 4));
        var smith = villagers.Spawn(village.Id, village.Center + new Vector3(-2f, 1f, 0f), 7501);
        smith.Role = VillagerRole.Smith;
        smith.IsGrounded = true;
        village.RegisterVillager(smith.Id);
        int planksBefore = village.Storage.CountBlock(BlockType.OakPlank);
        int logsBeforeCraft = village.Storage.CountBlock(BlockType.OakLog);
        int shovelsBefore = village.Storage.CountTools(ToolType.Shovel);
        if (!villages.TryAssignJob(village, smith, JobType.Craft, buildingId: workshop.Id))
        {
            throw new Exception("Smith assignment failed.");
        }

        for (int i = 0; i < 60 &&
            village.Storage.CountBlock(BlockType.OakPlank) <= planksBefore &&
            village.Storage.CountTools(ToolType.Shovel) <= shovelsBefore &&
            village.Storage.CountBlock(BlockType.OakLog) >= logsBeforeCraft; i++)
        {
            if (smith.CurrentJob == JobType.Craft)
            {
                smith.Position = village.GetBuildingWorkPosition(workshop);
                smith.SetAiPhase(VillagerAiPhase.Working);
                smith.WorkTimer = 10f;
            }

            villages.Update(0.2f, world, DayNightCycle.Noon, animals);
        }

        if (village.Storage.CountBlock(BlockType.OakPlank) <= planksBefore &&
            village.Storage.CountTools(ToolType.Shovel) <= shovelsBefore &&
            village.Storage.CountBlock(BlockType.OakLog) >= logsBeforeCraft)
        {
            throw new Exception(
                $"Smith did not craft workshop output. smith={smith.CurrentJob}/{smith.AiPhase}, " +
                $"logs={village.Storage.CountBlock(BlockType.OakLog)}, planksBefore={planksBefore}, planksAfter={village.Storage.CountBlock(BlockType.OakPlank)}, " +
                $"axes={village.Storage.CountTools(ToolType.Axe)}, pickaxes={village.Storage.CountTools(ToolType.Pickaxe)}, shovels={village.Storage.CountTools(ToolType.Shovel)}");
        }

        var kitchen = AddCompletedBuilding(village, "kitchen", BuildingKind.Kitchen, 204, village.AnchorX - 8, village.AnchorY, village.AnchorZ - 8);
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Carrot, 2));
        var cook = villagers.Spawn(village.Id, village.Center + new Vector3(-1f, 1f, -1f), 7502);
        cook.Role = VillagerRole.Cook;
        cook.IsGrounded = true;
        village.RegisterVillager(cook.Id);
        float foodBefore = village.FoodStock;
        if (!villages.TryAssignJob(village, cook, JobType.Cook, buildingId: kitchen.Id))
        {
            throw new Exception("Cook assignment failed.");
        }

        for (int i = 0; i < 80 && village.FoodStock <= foodBefore; i++)
        {
            if (cook.CurrentJob == JobType.Cook)
            {
                cook.Position = village.GetBuildingWorkPosition(kitchen);
                cook.SetAiPhase(VillagerAiPhase.Working);
                cook.WorkTimer = 10f;
            }

            villages.Update(0.2f, world, DayNightCycle.Noon, animals);
        }

        if (village.FoodStock <= foodBefore)
        {
            throw new Exception("Cook did not convert ingredients into food stock.");
        }

        var sheepPos = village.Center + new Vector3(3f, 1f, 3f);
        var sheep = animals.SpawnAt(AnimalType.Sheep, sheepPos, world);
        if (sheep == null)
        {
            throw new Exception("Could not spawn animal for hunter.");
        }

        var hunter = villagers.Spawn(village.Id, village.Center + new Vector3(2f, 1f, 2f), 7503);
        hunter.Role = VillagerRole.Hunter;
        hunter.IsGrounded = true;
        village.RegisterVillager(hunter.Id);
        if (!villages.TryAssignJob(village, hunter, JobType.Hunt))
        {
            throw new Exception("Hunter assignment failed.");
        }

        for (int i = 0; i < 120 && animals.Animals.Contains(sheep); i++)
        {
            villages.Update(0.2f, world, DayNightCycle.Noon, animals);
        }

        if (animals.Animals.Contains(sheep) || hunter.Inventory.CountBlock(BlockType.Carrot) == 0)
        {
            throw new Exception("Hunter did not kill animal and collect food.");
        }
    }

    private static VillageBuilding AddCompletedBuilding(
        VillageEntity village,
        string blueprintId,
        BuildingKind kind,
        int id,
        int anchorX,
        int anchorY,
        int anchorZ)
    {
        village.RestoreBuilding(new BuildingSaveData
        {
            Id = id,
            BlueprintId = blueprintId,
            Kind = (int)kind,
            AnchorX = anchorX,
            AnchorY = anchorY,
            AnchorZ = anchorZ,
            IsComplete = true
        });

        if (!village.TryGetBuilding(id, out var building))
        {
            throw new Exception($"Failed to add building {blueprintId}.");
        }

        return building;
    }

    private static void ClearBlueprintArea(VoxelWorld world, string blueprintId, int anchorX, int anchorY, int anchorZ)
    {
        if (!PlayerStructureRegistry.TryGet(blueprintId, out var blueprint))
        {
            throw new Exception($"Missing blueprint {blueprintId}.");
        }

        for (int x = anchorX - blueprint.Template.FootprintRadius - 2; x <= anchorX + blueprint.Template.FootprintRadius + 2; x++)
        {
            for (int z = anchorZ - blueprint.Template.FootprintRadius - 2; z <= anchorZ + blueprint.Template.FootprintRadius + 2; z++)
            {
                for (int y = anchorY; y <= anchorY + 8; y++)
                {
                    world.SetBlock(x, y, z, BlockType.Air);
                }
            }
        }
    }

    private static void PlaceFarmBlocks(VoxelWorld world, VillageBuilding farm)
    {
        if (!PlayerStructureRegistry.TryGet(farm.BlueprintId, out var blueprint))
        {
            throw new Exception("Farm plot blueprint missing.");
        }

        foreach (var block in blueprint.Template.Blocks)
        {
            if (block.Type == BlockType.Dirt)
            {
                world.SetBlock(farm.AnchorX + block.Dx, farm.AnchorY + block.Dy, farm.AnchorZ + block.Dz, BlockType.Dirt);
            }
        }
    }

    public static void RunFarmFoodProduction()
    {
        Console.Write("Running Farm Food Production Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(4242);
        int ax = 24;
        int az = 24;
        world.UpdateChunksAround(null, new Vector3(ax + 0.5f, 64f, az + 0.5f), 2);

        // Clear trees and foliage above the ground level in a 13x13 grid to prevent pathfinding blockage
        int testAy = StructureFingerprint.FindSurfaceAnchorY(world, ax, az);
        Console.WriteLine($"[DEBUG] FarmFoodProduction: testAy={testAy}, highestY={world.GetHighestSolidY(ax, az)}");

        for (int x = ax - 6; x <= ax + 6; x++)
        {
            for (int z = az - 6; z <= az + 6; z++)
            {
                for (int y = testAy; y <= testAy + 10; y++)
                {
                    world.SetBlock(x, y, z, BlockType.Air);
                }
            }
        }

        if (!TryFoundTestVillage(villages, world, "Farm Test", ax, az, out var village) || village == null)
        {
            throw new Exception("Failed to found farm test village.");
        }

        villagers.Update(0f, world, new[] { village });

        int ay = village.AnchorY;
        Console.WriteLine($"[DEBUG] FarmFoodProduction: village.AnchorY={ay}");
        village.RestoreBuilding(new BuildingSaveData
        {
            Id = 1,
            BlueprintId = "farm_plot",
            Kind = (int)BuildingKind.FarmPlot,
            AnchorX = ax,
            AnchorY = ay,
            AnchorZ = az,
            IsComplete = true
        });

        if (PlayerStructureRegistry.TryGet("farm_plot", out var farmBlueprint))
        {
            for (int dx = -5; dx <= 5; dx++)
            {
                for (int dz = -5; dz <= 5; dz++)
                {
                    if (Math.Abs(dx) <= 2 && Math.Abs(dz) <= 2)
                    {
                        continue;
                    }

                    world.SetBlock(ax + dx, ay, az + dz, BlockType.Dirt);
                }
            }

            foreach (var block in farmBlueprint.Template.Blocks)
            {
                if (block.Type != BlockType.Dirt)
                {
                    continue;
                }

                world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, BlockType.Dirt);
            }
        }

        world.SetBlock(ax, ay, az, BlockType.Wheat);

        var farmer = villagers.Spawn(village.Id, village.Center, 11);
        farmer.Role = VillagerRole.Farmer;
        farmer.Position = new Vector3(ax + 0.5f, ay + 1f, az - 3.5f);
        farmer.SetAiPhase(VillagerAiPhase.Working);
        village.RegisterVillager(farmer.Id);

        if (!villages.TryAssignJob(village, farmer, JobType.Farm, FarmCropHelper.GetBlockCenter(ax, ay, az), buildingId: 1))
        {
            throw new Exception("Failed to assign farmer to farm plot.");
        }

        var animals = new AnimalManager(4242);
        float initialFood = village.FoodStock;
        for (int i = 0; i < 120; i++)
        {
            villages.Update(0.25f, world, DayNightCycle.Noon, animals);
            if (i < 10)
            {
                if (villagers.TryGet(farmer.Id, out var f))
                {
                    var blockUnderFeet = world.GetBlock(24, 64, 20);
                    var blockAtCenter = world.GetBlock(24, 64, 24);
                    Console.WriteLine($"[DEBUG] Farmer Step {i}: Pos={f.Position}, Vel={f.Velocity}, Grounded={f.IsGrounded}, UnderFeet={blockUnderFeet}, Center={blockAtCenter}");
                }
            }
            else if (i % 10 == 0)
            {
                if (villagers.TryGet(farmer.Id, out var f))
                {
                    var blockUnderFeet = world.GetBlock(24, 64, 20);
                    var blockAtCenter = world.GetBlock(24, 64, 24);
                    Console.WriteLine($"[DEBUG] Farmer Tick {i}: Pos={f.Position}, Job={f.CurrentJob}, Phase={f.AiPhase}, Target={f.JobTarget}, WorkTimer={f.WorkTimer}, UnderFeet={blockUnderFeet}, Center={blockAtCenter}");
                }
            }
        }

        bool gainedFood = village.FoodStock > initialFood;
        bool hasCropInStorage = village.Storage.CountBlock(BlockType.Wheat) > 0
            || village.Storage.CountBlock(BlockType.Carrot) > 0;
        bool hasCropInChest = false;
        if (village.TryGetOutputChestForBuilding(1, out var chest))
        {
            hasCropInChest = chest.Buffer.CountBlock(BlockType.Wheat) > 0
                || chest.Buffer.CountBlock(BlockType.Carrot) > 0;
        }

        if (!gainedFood && !hasCropInStorage && !hasCropInChest)
        {
            throw new Exception("Farmer did not harvest crops into food stock or storage.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageFoundAndRecruit(AutonocraftGame game)
    {
        Console.Write("Running Village Found And Recruit Test... ");
        var session = game.Session;
        var world = session.Grid;
        world.UpdateChunksAround(null, session.Player.Position, 2);

        int ax = (int)session.Player.Position.X + 12;
        int az = (int)session.Player.Position.Z + 12;
        int ay = StructureFingerprint.FindSurfaceAnchorY(world, ax, az);
        if (PlayerStructureRegistry.TryGet("town_heart", out var heart))
        {
            foreach (var block in heart.Template.Blocks)
            {
                world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, BlockType.Air);
            }
        }

        if (!session.Villages.TryFoundVillage(world, "Test Hamlet", ax, az, out var village) || village == null)
        {
            throw new Exception("Could not found village.");
        }

        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 16));

        if (village.Population < 1)
        {
            throw new Exception("Founding should spawn a settler.");
        }

        if (!session.Villages.TryRecruit(village, world))
        {
            throw new Exception("Recruit failed.");
        }

        if (village.Population < 2)
        {
            throw new Exception("Recruit should add a second villager.");
        }

        if (!session.Villagers.TryGet(village.VillagerIds[^1], out var recruited))
        {
            throw new Exception("Recruited villager was not spawned in the world.");
        }

        int surfaceY = world.GetHighestSolidY((int)MathF.Floor(recruited.Position.X), (int)MathF.Floor(recruited.Position.Z));
        if (recruited.Position.Y <= surfaceY)
        {
            throw new Exception("Recruited villager spawned below ground.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageSaveRoundTrip(AutonocraftGame game)
    {
        Console.Write("Running Village Save Round-Trip Test... ");
        RunVillageSaveRoundTripV6(game);
    }

    public static void RunVillageSaveRoundTripV6(AutonocraftGame game)
    {
        Console.Write("Running Village Save Round-Trip V6 Test... ");
        var session = game.Session;
        var world = session.Grid;
        int ax = (int)session.Player.Position.X + 20;
        int az = (int)session.Player.Position.Z + 20;
        if (!TryFoundTestVillage(session.Villages, world, "Save Test", ax, az, out var village) || village == null)
        {
            throw new Exception("Could not found Save Test village.");
        }

        EnsureFlatVillagePad(world, village, radius: 10);
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Dirt, 32));
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));
        session.Villages.TryRecruit(village, world);
        int plotX = ax + 6;
        int plotZ = az + 6;
        if (!session.Villages.TryQueueBlueprint(world, village, "farm_plot", plotX, plotZ, village.Storage, village.AnchorY))
        {
            throw new Exception("Failed to queue farm_plot for save test.");
        }

        int expectedSites = village.BuildingSites.Count;

        if (village.VillagerIds.Count > 0 &&
            session.Villagers.TryGet(village.VillagerIds[0], out var seedVillager))
        {
            seedVillager.Skills.AddXp(Autonocraft.Items.VillagerSkill.Mining, 55f);
            seedVillager.Skills.AddXp(Autonocraft.Items.VillagerSkill.Farming, 12f);
        }

        var exportedVillages = session.Villages.ExportVillages();
        var exportedVillagers = session.Villagers.ExportVillagers();
        var exportedAnchors = session.Villages.ExportClaimedAnchors();
        if (exportedVillages.Count == 0)
        {
            throw new Exception("No villages exported.");
        }

        var exportedSave = exportedVillages.Find(v => v.Name == "Save Test");
        if (exportedSave == null)
        {
            throw new Exception("Save Test village missing from export.");
        }

        if (exportedSave.BuildingSites.Count == 0 && expectedSites > 0)
        {
            throw new Exception("Building sites missing from export.");
        }

        if (exportedSave.PopulationCap <= 0)
        {
            throw new Exception("Population cap not exported.");
        }

        session.Villages.LoadFromSave(exportedVillages, exportedVillagers, exportedAnchors);
        if (session.Villages.Villages.Count == 0)
        {
            throw new Exception("Villages not loaded.");
        }

        var loaded = session.Villages.GetVillage(exportedSave.Id);
        if (loaded == null)
        {
            throw new Exception("Save Test village not loaded.");
        }

        if (expectedSites > 0 && loaded.BuildingSites.Count == 0)
        {
            throw new Exception("Building sites not persisted.");
        }

        if (exportedVillagers.Count > 0)
        {
            var exportedVillager = exportedVillagers[0];
            session.Villagers.TryGet(exportedVillager.Id, out var loadedVillager);
            if (loadedVillager == null)
            {
                throw new Exception("Villager not loaded after save round-trip.");
            }

            if (string.IsNullOrEmpty(exportedVillager.Trait))
            {
                throw new Exception("Exported villager trait missing.");
            }

            if (!string.Equals(loadedVillager.Persona.Trait, exportedVillager.Trait, StringComparison.Ordinal))
            {
                throw new Exception($"Villager trait not restored: expected {exportedVillager.Trait}, got {loadedVillager.Persona.Trait}.");
            }

            if (loadedVillager.Skills.Mining.Level != exportedVillager.MiningLevel ||
                MathF.Abs(loadedVillager.Skills.Mining.Xp - exportedVillager.MiningXp) > 0.001f ||
                loadedVillager.Skills.Woodcutting.Level != exportedVillager.WoodcuttingLevel ||
                MathF.Abs(loadedVillager.Skills.Woodcutting.Xp - exportedVillager.WoodcuttingXp) > 0.001f ||
                loadedVillager.Skills.Farming.Level != exportedVillager.FarmingLevel ||
                MathF.Abs(loadedVillager.Skills.Farming.Xp - exportedVillager.FarmingXp) > 0.001f)
            {
                throw new Exception("Villager skills not restored after save round-trip.");
            }
        }

        Console.WriteLine("PASSED");
    }

    public static void RunBlueprintPlacementHelper()
    {
        Console.Write("Running Blueprint Placement Helper Test... ");
        var world = new VoxelWorld(8080);
        world.UpdateChunksAround(null, new System.Numerics.Vector3(16.5f, 64f, 16.5f), 2);

        int ax = 16;
        int az = 16;
        int surface = world.GetHighestSolidY(ax, az);
        world.SetBlock(ax, surface + 1, az, BlockType.Air);
        world.SetBlock(ax, surface + 2, az, BlockType.Air);

        var origin = new System.Numerics.Vector3(ax + 0.5f, surface + 3f, az + 0.5f);
        var direction = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(0f, -1f, 0f));
        var resolved = Autonocraft.Village.BlueprintPlacementHelper.ResolveFromLook(
            world,
            origin,
            direction,
            BlockInteractionSystem.RaycastRange);

        if (!resolved.HasHit)
        {
            throw new Exception("Expected top-face raycast hit.");
        }

        if (resolved.AnchorX != ax || resolved.AnchorZ != az || resolved.AnchorY != surface + 1)
        {
            throw new Exception($"Unexpected anchor ({resolved.AnchorX}, {resolved.AnchorY}, {resolved.AnchorZ}).");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunCanPlaceBlueprint()
    {
        Console.Write("Running Can Place Blueprint Test... ");
        var villagers = new Entities.VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(4242);
        int ax = 32;
        int az = 32;
        if (!TryFoundTestVillage(villages, world, "Placement Test", ax, az, out var village) || village == null)
        {
            throw new Exception("Could not found village.");
        }

        if (!PlayerStructureRegistry.TryGet("farm_plot", out var blueprint))
        {
            throw new Exception("farm_plot blueprint missing.");
        }

        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Dirt, 32));
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));

        if (villages.CanPlaceBlueprint(world, village, blueprint, ax + 80, az + 80, village.Storage))
        {
            throw new Exception("Expected out-of-radius blueprint placement to fail.");
        }

        int candidateX = ax + 8;
        int candidateZ = az + 8;
        int candidateY = StructureFingerprint.FindSurfaceAnchorY(world, candidateX, candidateZ);
        world.SetBlock(candidateX, candidateY, candidateZ, BlockType.Stone);
        if (villages.CanPlaceBlueprint(world, village, blueprint, candidateX, candidateZ, village.Storage, candidateY))
        {
            throw new Exception("Expected overlapping blueprint placement to fail.");
        }

        if (blueprint.CanAfford(village.Storage)
            && villages.CanPlaceBlueprint(world, village, blueprint, ax + 80, az, village.Storage))
        {
            throw new Exception("Expected out-of-radius placement to fail even when affordable.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunQueuedBuildingSiteSurvivesSync()
    {
        Console.Write("Running Queued Building Site Survives Sync Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers) { CreativeMode = true };
        var world = new VoxelWorld(4242);
        int ax = 32;
        int az = 32;
        if (!TryFoundTestVillage(villages, world, "Site Sync Test", ax, az, out var village) || village == null)
        {
            throw new Exception("Could not found village.");
        }

        int plotX = ax + 6;
        int plotZ = az + 6;
        if (!villages.TryQueueBlueprint(world, village, "farm_plot", plotX, plotZ, village.Storage))
        {
            throw new Exception("Failed to queue farm plot.");
        }

        var site = village.BuildingSites[^1];
        site.SyncWithWorld(world);
        if (site.IsComplete || site.RemainingCount == 0)
        {
            throw new Exception("Farm plot on natural dirt should stay pending until builders finish.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunBuildingJobWiring()
    {
        Console.Write("Running Building Job Wiring Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(7777);
        int ax = 24;
        int az = 24;
        TryFoundTestVillage(villages, world, "Building Test", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        villagers.Update(0f, world, new[] { village });

        villagers.Update(0f, world, new[] { village });

        int ay = village.AnchorY;
        village.RestoreBuilding(new BuildingSaveData
        {
            Id = 1,
            BlueprintId = "lumber_camp",
            Kind = (int)BuildingKind.LumberCamp,
            AnchorX = ax + 8,
            AnchorY = ay,
            AnchorZ = az,
            IsComplete = true
        });
        village.RestoreBuilding(new BuildingSaveData
        {
            Id = 2,
            BlueprintId = "workshop",
            Kind = (int)BuildingKind.Workshop,
            AnchorX = ax - 8,
            AnchorY = ay,
            AnchorZ = az,
            IsComplete = true
        });
        village.RestoreBuilding(new BuildingSaveData
        {
            Id = 3,
            BlueprintId = "farm_plot",
            Kind = (int)BuildingKind.FarmPlot,
            AnchorX = ax,
            AnchorY = ay,
            AnchorZ = az + 8,
            IsComplete = true
        });
        village.RestoreBuilding(new BuildingSaveData
        {
            Id = 4,
            BlueprintId = "quarry",
            Kind = (int)BuildingKind.Quarry,
            AnchorX = ax + 8,
            AnchorY = ay,
            AnchorZ = az + 8,
            IsComplete = true
        });

        var farmer = villagers.Spawn(village.Id, village.Center, 1);
        farmer.Role = VillagerRole.Farmer;
        village.RegisterVillager(farmer.Id);

        if (!villages.TryAssignJob(village, farmer, JobType.Farm, buildingId: 3))
        {
            throw new Exception("Farmer assignment to farm plot failed.");
        }

        if (farmer.AssignedBuildingId != 3)
        {
            throw new Exception("Farmer not bound to farm plot building.");
        }

        if (farmer.JobTarget == null || farmer.JobTarget.Value.Z < az + 7f)
        {
            throw new Exception("Farmer target not at farm plot anchor.");
        }

        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakLog, 4));
        var smith = villagers.Spawn(village.Id, village.Center, 2);
        smith.Role = VillagerRole.Smith;
        village.RegisterVillager(smith.Id);

        if (!villages.TryAssignJob(village, smith, JobType.Craft))
        {
            throw new Exception("Smith craft assignment failed without workshop recipe materials.");
        }

        if (smith.AssignedBuildingId != 2 || smith.Role != VillagerRole.Smith)
        {
            throw new Exception("Smith not bound to workshop.");
        }

        int logsBefore = village.Storage.CountBlock(BlockType.OakLog);
        smith.SetAiPhase(VillagerAiPhase.Working);
        smith.WorkTimer = 10f;
        var context = new VillageContext
        {
            Village = village,
            VillageCenter = village.Center,
            StoragePosition = village.StoragePosition,
            Storage = village.Storage
        };
        smith.Update(2f, world, context);
        if (village.Storage.CountBlock(BlockType.OakLog) >= logsBefore &&
            village.Storage.CountTools(ToolType.Pickaxe) == 0 &&
            village.Storage.CountTools(ToolType.Axe) == 0)
        {
            throw new Exception("Workshop smith did not craft or repair tools from storage.");
        }

        var lumberjack = villagers.Spawn(village.Id, village.Center, 3);
        lumberjack.Role = VillagerRole.Lumberjack;
        village.RegisterVillager(lumberjack.Id);
        if (!villages.TryAssignJob(village, lumberjack, JobType.Lumber, buildingId: 1))
        {
            throw new Exception("Lumberjack assignment to camp failed.");
        }

        if (lumberjack.AssignedBuildingId != 1)
        {
            throw new Exception("Lumberjack not bound to lumber camp.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillagerToolMining()
    {
        Console.Write("Running Villager Tool Mining Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(8888);
        int ax = 40;
        int az = 40;
        world.UpdateChunksAround(null, new Vector3(ax + 0.5f, 64f, az + 0.5f), 2);
        TryFoundTestVillage(villages, world, "Tool Test", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        villagers.Update(0f, world, new[] { village });

        int x = ax + 4;
        int z = az + 4;
        int y = world.GetHighestSolidY(x, z) + 1;
        world.SetBlock(x, y, z, BlockType.Stone);

        var miner = villagers.Spawn(village.Id, village.Center, 11);
        miner.Role = VillagerRole.Miner;
        village.RegisterVillager(miner.Id);

        var target = new Vector3(x + 0.5f, y, z + 0.5f);
        if (!villages.TryAssignJob(village, miner, JobType.Mine, target))
        {
            throw new Exception("Mine job assignment failed.");
        }

        miner.Position = target + new Vector3(0f, 0f, 1.5f);
        miner.SetAiPhase(VillagerAiPhase.Working);

        var context = new VillageContext
        {
            Village = village,
            VillageCenter = village.Center,
            StoragePosition = village.StoragePosition,
            Storage = village.Storage
        };

        miner.Update(0.5f, world, context);
        if (miner.CurrentJob != JobType.Mine)
        {
            throw new Exception("Miner without pickaxe should keep mining bare-handed.");
        }

        float bareBreakTime = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, ItemStack.Empty, miner.Skills);
        miner.BreakProgress = 0f;
        miner.Update(bareBreakTime + 0.1f, world, context);
        if (world.GetBlock(x, y, z) != BlockType.Air)
        {
            throw new Exception("Miner should break stone bare-handed, just slower.");
        }

        world.SetBlock(x, y, z, BlockType.Stone);
        for (int i = 0; i < miner.Inventory.SlotCount; i++)
        {
            miner.Inventory.SetSlot(i, ItemStack.Empty);
        }

        var pickaxe = ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood);
        int initialDurability = pickaxe.Durability;
        village.Storage.AddItem(pickaxe);

        if (!villages.TryAssignJob(village, miner, JobType.Mine, target))
        {
            throw new Exception("Mine job reassignment failed.");
        }

        miner.Position = target + new Vector3(0f, 0f, 1.5f);
        miner.SetAiPhase(VillagerAiPhase.Working);
        miner.BreakProgress = 0f;

        var skills = new PlayerSkills();
        float breakTime = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, pickaxe, skills);
        float bareHands = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, ItemStack.Empty, skills);
        if (breakTime >= bareHands)
        {
            throw new Exception("Pickaxe should reduce stone break time for villagers.");
        }

        miner.Update(breakTime + 0.05f, world, context);
        if (world.GetBlock(x, y, z) != BlockType.Air)
        {
            throw new Exception("Miner with pickaxe did not break stone in expected time.");
        }

        if (miner.Inventory.CountBlock(BlockType.Stone) != 1)
        {
            throw new Exception("Miner should carry mined stone.");
        }

        if (miner.CurrentJob != JobType.Mine)
        {
            throw new Exception("Miner should continue mining after a single stone break.");
        }

        if (miner.EquippedTool.Durability != initialDurability - 1)
        {
            throw new Exception("Pickaxe durability should decrease after mining.");
        }

        pickaxe = ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood);
        pickaxe.Durability = 10;
        pickaxe.MaxDurability = ToolRegistry.Get(pickaxe.ToolId).MaxDurability;
        village.Storage.AddItem(pickaxe);
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 4));

        if (!VillageWorkshopCrafting.TrySmithWork(village.Storage))
        {
            throw new Exception("Smith should repair damaged pickaxe.");
        }

        bool foundRepaired = false;
        for (int i = 0; i < village.Storage.SlotCount; i++)
        {
            var stack = village.Storage.GetSlot(i);
            if (stack.IsTool() && stack.ToolId == ItemId.WoodPickaxe && stack.Durability > 10)
            {
                foundRepaired = true;
                break;
            }
        }

        if (!foundRepaired)
        {
            throw new Exception("Repaired pickaxe not returned to storage.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageAiToolsMock()
    {
        Console.Write("Running Village AI Tools Test... ");
        var villagers = new Entities.VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(999);
        int ax = 16;
        int az = 16;
        TryFoundTestVillage(villages, world, "AI Test", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        var (ok, msg) = VillageAiTools.ExecuteTool("get_village_summary", "{}", villages, villagers, village, world);
        if (!ok || string.IsNullOrEmpty(msg))
        {
            throw new Exception("Summary tool failed.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageNumericGoals()
    {
        Console.Write("Running Village Numeric Goals Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(4243);
        int ax = 16;
        int az = 16;
        TryFoundTestVillage(villages, world, "Goals Test", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        EnsureFlatVillagePad(world, village, 12);

        if (!VillageGoalParser.TryParseDescription("Stock 64 Cobblestone", out var stockParsed) ||
            stockParsed.Kind != VillageGoalKind.Stock ||
            stockParsed.StockBlock != BlockType.Cobblestone ||
            stockParsed.TargetCount != 64)
        {
            throw new Exception("Stock goal parser failed.");
        }

        if (!VillageGoalParser.TryParseDescription("Build peasant house", out var buildParsed) ||
            buildParsed.Kind != VillageGoalKind.Build ||
            buildParsed.BlueprintId != "peasant_house")
        {
            throw new Exception("Build goal parser failed.");
        }

        var (stockOk, _) = VillageAiTools.ExecuteTool(
            "set_village_goal",
            """{"kind":"stock","block_type":"Cobblestone","target_count":64}""",
            villages,
            villagers,
            village,
            world);
        if (!stockOk)
        {
            throw new Exception("set_village_goal stock failed.");
        }

        var topStock = village.Scheduler.GetTopOpenGoal();
        if (topStock == null || topStock.Kind != VillageGoalKind.Stock || topStock.TargetCount != 64)
        {
            throw new Exception("Stock goal not registered.");
        }

        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Cobblestone, 64));
        village.Scheduler.CheckGoalProgress(village);
        if (!topStock.Completed)
        {
            throw new Exception("Stock goal should complete at target count.");
        }

        village.Storage.TryConsumeBlock(BlockType.OakPlank, village.Storage.CountBlock(BlockType.OakPlank));
        village.Storage.TryConsumeBlock(BlockType.Cobblestone, village.Storage.CountBlock(BlockType.Cobblestone));
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 24));
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Cobblestone, 8));

        var (buildOk, _) = VillageAiTools.ExecuteTool(
            "set_village_goal",
            """{"description":"Build peasant house","priority":5}""",
            villages,
            villagers,
            village,
            world);
        if (!buildOk)
        {
            throw new Exception("set_village_goal build failed.");
        }

        var buildGoal = village.Scheduler.GetTopOpenGoal();
        if (buildGoal == null || buildGoal.Kind != VillageGoalKind.Build || buildGoal.BlueprintId != "peasant_house")
        {
            throw new Exception("Build goal not registered.");
        }

        if (!village.Scheduler.TryApplyGoal(village, world, villages, buildGoal))
        {
            throw new Exception("Build goal should queue peasant house when materials are available.");
        }

        if (!buildGoal.BuildQueued || village.BuildingSites.Count == 0)
        {
            throw new Exception("Peasant house site not queued.");
        }

        var exported = villages.ExportVillages();
        if (exported.Count == 0 || exported[0].Goals.Count < 2)
        {
            throw new Exception("Goals not exported.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunPlayerWorkQueue()
    {
        Console.Write("Running Player Work Queue Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(8181);
        world.UpdateChunksAround(null, new System.Numerics.Vector3(16.5f, 64f, 16.5f), 2);

        int ax = 16;
        int az = 16;
        TryFoundTestVillage(villages, world, "Work Test", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        var lumberjack = villagers.Spawn(village.Id, village.Center, 11);
        lumberjack.Role = VillagerRole.Lumberjack;
        village.RegisterVillager(lumberjack.Id);

        int minY = int.MaxValue;
        int maxY = int.MinValue;
        for (int dx = 1; dx <= 3; dx++)
        {
            for (int dz = 1; dz <= 3; dz++)
            {
                int cx = ax + dx;
                int cz = az + dz;
                int cy = world.GetHighestSolidY(cx, cz);
                world.SetBlock(cx, cy, cz, BlockType.OakLog);
                minY = Math.Min(minY, cy);
                maxY = Math.Max(maxY, cy);
            }
        }

        int markX = ax + 2;
        int markZ = az + 2;
        int markY = world.GetHighestSolidY(markX, markZ);
        if (!villages.TryMarkWorkBlock(world, markX, markY, markZ, out _))
        {
            throw new Exception("Mark work block failed.");
        }

        if (village.WorkQueue.Count != 1)
        {
            throw new Exception("Expected one queued block.");
        }

        if (lumberjack.CurrentJob != JobType.Lumber)
        {
            throw new Exception("Expected lumberjack assigned to marked block.");
        }

        if (!villages.TryMarkWorkZone(world, village, ax + 1, minY, az + 1, ax + 3, maxY, az + 3, out string zoneMessage))
        {
            throw new Exception($"Mark work zone failed: {zoneMessage}");
        }

        if (village.WorkQueue.Count < 2)
        {
            throw new Exception("Work zone should queue additional blocks.");
        }

        var exported = villages.ExportVillages();
        if (exported[0].WorkQueue.Count == 0)
        {
            throw new Exception("Work queue not exported.");
        }

        var reloadedVillagers = new VillagerManager();
        var reloadedVillages = new VillageManager(reloadedVillagers);
        reloadedVillages.LoadFromSave(exported, villagers.ExportVillagers());
        var loaded = reloadedVillages.GetPrimaryVillage();
        if (loaded == null || loaded.WorkQueue.Count == 0)
        {
            throw new Exception("Work queue not restored from save.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageScreenInputLayout()
    {
        Console.Write("Running Village Screen Input Layout Test... ");
        const float panelWidth = 900f;
        const float panelHeight = 600f;
        const float buttonWidth = 148f;
        const float buttonHeight = 34f;
        const float contentTop = 98f;
        const float footerHeight = 74f;

        var layout = new Autonocraft.Engine.UiLayout(1280, 720);
        float panelW = layout.S(panelWidth);
        float panelH = layout.S(panelHeight);
        float panelX = layout.CenterX - panelW / 2f;
        float panelY = layout.CenterY - panelH / 2f;
        float left = panelX + layout.S(20f);
        float footerY = panelY + panelH - layout.S(footerHeight);
        float buttonW = layout.S(buttonWidth);
        float buttonH = layout.S(buttonHeight);

        var recruitRect = new Microsoft.Xna.Framework.Rectangle((int)left, (int)footerY, (int)buttonW, (int)buttonH);
        if (!recruitRect.Contains(recruitRect.Center.X, recruitRect.Center.Y))
        {
            throw new Exception("Recruit button center must be inside recruit hit box.");
        }

        var closeRect = new Microsoft.Xna.Framework.Rectangle(
            (int)(panelX + panelW - layout.S(20f) - buttonW),
            (int)(panelY + panelH - layout.S(30f)),
            (int)buttonW,
            (int)buttonH);
        if (!closeRect.Contains(closeRect.Center.X, closeRect.Center.Y))
        {
            throw new Exception("Close button center must be inside close hit box.");
        }

        float contentY = panelY + layout.S(contentTop);
        float listW = layout.S(300f);
        float detailX = left + listW + layout.S(14f);
        float pad = layout.S(16f);
        float talkY = contentY + pad + layout.S(152f);
        var talkRect = new Microsoft.Xna.Framework.Rectangle(
            (int)(detailX + pad),
            (int)talkY,
            (int)layout.S(96f),
            (int)layout.S(buttonHeight));
        if (!talkRect.Contains(talkRect.Center.X, talkRect.Center.Y))
        {
            throw new Exception("Talk button center must be inside talk hit box.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageSaveRoundTripV7(AutonocraftGame game)
    {
        Console.Write("Running Village Save Round-Trip V7 Test... ");
        var session = game.Session;
        int ax = (int)session.Player.Position.X + 12;
        int az = (int)session.Player.Position.Z + 12;
        session.Villages.TryFoundVillage(session.Grid, "V7 Test", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Failed to found village for v7 test.");
        }

        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));
        session.Villages.TryRecruit(village, session.Grid);
        if (village.VillagerIds.Count > 0 &&
            session.Villagers.TryGet(village.VillagerIds[0], out var villager))
        {
            villager.AssignHaulJob(village.Center, 1, null);
            villager.SetEquippedTool(ToolRegistry.CreateStack(ToolType.Axe, ToolTier.Wood));
        }

        village.Radius = 40f;
        var exported = session.Villages.ExportVillages().Find(v => v.Id == village.Id);
        if (exported == null || MathF.Abs(exported.Radius - 40f) > 0.01f)
        {
            throw new Exception("V7 export missing radius.");
        }

        var exportedVillager = session.Villagers.ExportVillagers().Find(v => v.VillageId == village.Id);
        if (exportedVillager == null || exportedVillager.HaulSourceChestId != 1)
        {
            throw new Exception("V7 export missing haul state.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunRepairMissingCitizens()
    {
        Console.Write("Running Repair Missing Citizens Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(6060);

        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage();
        if (village == null)
        {
            throw new Exception("Starter village missing.");
        }

        while (village.VillagerIds.Count > 0)
        {
            village.UnregisterVillager(village.VillagerIds[0]);
        }

        villagers.LoadVillagers(Array.Empty<VillagerSaveData>());

        if (!villages.RepairVillageCitizens(village, world))
        {
            throw new Exception("Repair should spawn missing citizens.");
        }

        if (VillageSettlementHealth.GetLivePopulation(village, villagers) < 2)
        {
            throw new Exception("Expected at least 2 citizens after repair.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillagerLumberChopping()
    {
        Console.Write("Running Villager Lumber Chopping Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(9090);
        int ax = 48;
        int az = 48;
        world.UpdateChunksAround(null, new Vector3(ax + 0.5f, 64f, az + 0.5f), 2);
        TryFoundTestVillage(villages, world, "Lumber Test", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        villagers.Update(0f, world, new[] { village });

        int x = ax + 3;
        int z = az + 3;
        int groundY = world.GetHighestSolidY(x, z);
        int logY = groundY + 1;
        world.SetBlock(x, logY, z, BlockType.OakLog);
        world.SetBlock(x, logY + 1, z, BlockType.OakLeaves);

        var lumberjack = villagers.Spawn(village.Id, village.Center, 21);
        lumberjack.Role = VillagerRole.Lumberjack;
        village.RegisterVillager(lumberjack.Id);

        var target = new Vector3(x + 0.5f, logY, z + 0.5f);
        if (!villages.TryAssignJob(village, lumberjack, JobType.Lumber, target))
        {
            throw new Exception("Lumber job assignment failed.");
        }

        lumberjack.Position = new Vector3(x + 0.5f, groundY + 1f, z + 1.6f);
        lumberjack.SetAiPhase(VillagerAiPhase.Working);
        lumberjack.BreakProgress = 0f;

        var context = new VillageContext
        {
            Village = village,
            VillageCenter = village.Center,
            StoragePosition = village.StoragePosition,
            Storage = village.Storage
        };

        for (int i = 0; i < 120; i++)
        {
            lumberjack.Update(0.25f, world, context);
            if (world.GetBlock(x, logY, z) == BlockType.Air)
            {
                break;
            }
        }

        if (world.GetBlock(x, logY, z) != BlockType.Air)
        {
            throw new Exception("Lumberjack did not break oak log.");
        }

        if (lumberjack.Inventory.CountBlock(BlockType.OakLog) != 1)
        {
            throw new Exception("Lumberjack should carry chopped oak log.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunAdoptOrphanedCitizens()
    {
        Console.Write("Running Adopt Orphaned Citizens Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(9191);

        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage();
        if (village == null)
        {
            throw new Exception("Starter village missing.");
        }

        foreach (var villager in villagers.All)
        {
            villager.VillageId = village.Id + 99;
        }

        villages.SyncCitizensForVillage(village);
        if (VillageSettlementHealth.GetLivePopulation(village, villagers) < 2)
        {
            throw new Exception("Nearby settlers should be adopted into the active village.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageRegistryDesyncLiveChop()
    {
        Console.Write("Running Village Registry Desync Live Chop Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(9292);
        world.UpdateChunksAround(null, new Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f), 2);
        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage();
        if (village == null)
        {
            throw new Exception("Starter village missing.");
        }

        while (village.VillagerIds.Count > 0)
        {
            village.UnregisterVillager(village.VillagerIds[0]);
        }

        if (VillageSettlementHealth.GetLivePopulation(village, villagers) < 2)
        {
            throw new Exception("Expected live starter citizens even with empty registry.");
        }

        int x = village.AnchorX + 5;
        int z = village.AnchorZ + 2;
        int groundY = world.GetHighestSolidY(x, z);
        int logY = groundY + 1;
        world.SetBlock(x, logY, z, BlockType.OakLog);

        Villager? lumberjack = null;
        foreach (var villager in villagers.All)
        {
            if (villager.VillageId == village.Id)
            {
                lumberjack = villager;
                break;
            }
        }

        if (lumberjack == null)
        {
            throw new Exception("No lumberjack found for village.");
        }

        if (!villages.TryAssignJob(village, lumberjack, JobType.Lumber, new Vector3(x + 0.5f, logY, z + 0.5f)))
        {
            throw new Exception("Failed to assign lumber job.");
        }

        lumberjack.Position = new Vector3(x + 0.5f, groundY + 1f, z + 1.6f);
        lumberjack.IsGrounded = true;

        for (int i = 0; i < 240; i++)
        {
            villages.Update(0.25f, world, DayNightCycle.Noon, new AnimalManager(world.Seed));
            if (world.GetBlock(x, logY, z) == BlockType.Air)
            {
                break;
            }
        }

        if (world.GetBlock(x, logY, z) != BlockType.Air)
        {
            throw new Exception("Lumberjack did not chop log during live village simulation with desynced registry.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageGuidanceHints()
    {
        Console.Write("Running Village Guidance Hints Test... ");
        var villagers = new Autonocraft.Entities.VillagerManager();
        var village = new Autonocraft.Village.Village("Hint Test", 0, 64, 0);
        string hint = Autonocraft.Village.VillageGuidance.GetNextBestAction(village, villagers);
        if (!hint.Contains("settlement", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Expected founding hint for empty village.");
        }

        village.RegisterVillager(villagers.Spawn(village.Id, new System.Numerics.Vector3(0.5f, 65f, 0.5f), 1).Id);
        village.FoodStock = 6f;
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));
        hint = Autonocraft.Village.VillageGuidance.GetNextBestAction(village, villagers);
        if (!hint.Contains("idle", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Expected idle-assignment hint once a settler exists, got: {hint}");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageEventsNotifier()
    {
        Console.Write("Running Village Events Notifier Test... ");
        string? last = null;
        var events = new Autonocraft.Village.VillageEvents { ShowToast = msg => last = msg };
        events.OnRecruit(new Autonocraft.Entities.Villager(1, System.Numerics.Vector3.Zero, 42, "Test"));
        if (string.IsNullOrEmpty(last) || !last.Contains("joined", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Recruit event did not fire toast.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunStarvationConsequences()
    {
        Console.Write("Running Starvation Consequences Test... ");
        var villagers = new Autonocraft.Entities.VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(12345);

        var village = new Autonocraft.Village.Village("Starve Test", 0, 64, 0);
        villages.RegisterVillageForTest(village);

        var v = villagers.Spawn(village.Id, new System.Numerics.Vector3(0f, 65f, 0f), 1);
        village.RegisterVillager(v.Id);

        if (village.Population != 1)
        {
            throw new Exception("Expected population of 1.");
        }

        var haulCoordinator = new HaulCoordinator(villagers);
        var dispatcher = new JobDispatcher(villagers, haulCoordinator);
        var simulation = new VillageSimulation(villagers, dispatcher, haulCoordinator);
        string? lastToast = null;
        var events = new Autonocraft.Village.VillageEvents { ShowToast = msg => lastToast = msg };
        simulation.SetVillageEvents(events);

        village.FoodStock = 0f;
        var finalizedSites = new HashSet<int>();

        // Day 1
        simulation.Update(new[] { village }, finalizedSites, 120f, world, DayNightCycle.Noon, null!);
        if (village.ConsecutiveDaysWithoutFood != 1)
        {
            throw new Exception($"Expected 1 day without food, got {village.ConsecutiveDaysWithoutFood}");
        }
        if (village.Population != 1)
        {
            throw new Exception("Villager should not despawn on day 1.");
        }

        // Day 2
        simulation.Update(new[] { village }, finalizedSites, 120f, world, DayNightCycle.Noon, null!);
        if (village.ConsecutiveDaysWithoutFood != 2)
        {
            throw new Exception("Expected 2 days without food.");
        }
        float speed = village.GetWorkSpeedMultiplier();
        if (speed > 0.5f)
        {
            throw new Exception($"Expected work speed drop due to starvation, got {speed}");
        }

        // Day 3
        simulation.Update(new[] { village }, finalizedSites, 120f, world, DayNightCycle.Noon, null!);

        // Day 4 - Departure
        simulation.Update(new[] { village }, finalizedSites, 120f, world, DayNightCycle.Noon, null!);
        if (village.Population != 0)
        {
            throw new Exception("Expected villager to despawn/leave due to starvation.");
        }
        if (string.IsNullOrEmpty(lastToast) || !lastToast.Contains("left", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Expected starvation departure toast notification.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageRename()
    {
        Console.Write("Running Village Rename Test... ");
        var village = new Autonocraft.Village.Village("Old Name", 0, 64, 0);
        if (village.Name != "Old Name")
        {
            throw new Exception("Expected village name to be Old Name.");
        }
        village.Name = "New Name";
        if (village.Name != "New Name")
        {
            throw new Exception("Expected renamed village name to be New Name.");
        }
        Console.WriteLine("PASSED");
    }
}
