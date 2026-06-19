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

public static partial class VillageTests
{
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

    private static void WaitForAgentReady(HttpClient http, int maxAttempts = 50)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var health = GetJson(http, "/health");
                if (health.RootElement.GetProperty("ready").GetBoolean())
                {
                    return;
                }
            }
            catch
            {
            }

            Thread.Sleep(10 + attempt * 2);
        }

        throw new Exception("Agent HTTP server did not become ready in time.");
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
        private readonly object _actionLock = new();
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
            lock (_actionLock)
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
    }

}
