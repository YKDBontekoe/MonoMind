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
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.UI;
using Autonocraft.UI.Village;
using Autonocraft.UI.VillagePanels;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Tests.Integration;

public static partial class VillageTests
{
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

        // Verify the shared Talk-button / Job-grid calculator produces Y values inside
        // the visible content area (not clipped into the footer).
        float contentY = panelY + layout.S(contentTop);
        float contentBottom = panelY + panelH - layout.S(footerHeight);
        float listW = layout.S(PeoplePanel.ListWidth);
        float detailX = left + listW + layout.S(14f);
        float pad = layout.S(16f);

        // Create a minimal villager/village pair so the calculator can run.
        var testVillage = new Autonocraft.Village.Village("Layout Test", 0, 64, 0);
        var testVillager = new Autonocraft.Entities.Villager(1, System.Numerics.Vector3.Zero, 42, "Tester");

        PeoplePanel.GetDetailButtonYs(
            layout, panelY, testVillager, testVillage,
            hasFeedback: false,
            out float talkButtonY, out float jobGridY);

        if (talkButtonY < contentY || talkButtonY >= contentBottom)
        {
            throw new Exception($"Talk button Y ({talkButtonY:F0}) must be inside content area [{contentY:F0}, {contentBottom:F0}).");
        }

        float jobGridBottom = jobGridY + 3f * layout.S(buttonHeight) + 2f * layout.S(10f);
        if (jobGridBottom > contentBottom + layout.S(4f))   // 4 px tolerance for rounding
        {
            throw new Exception($"Job grid bottom ({jobGridBottom:F0}) clips past content bottom ({contentBottom:F0}).");
        }

        // Verify the hit rect for the Talk button is self-consistent.
        var talkRect = new Microsoft.Xna.Framework.Rectangle(
            (int)(detailX + pad),
            (int)talkButtonY,
            (int)layout.S(96f),
            (int)layout.S(buttonHeight));
        if (!talkRect.Contains(talkRect.Center.X, talkRect.Center.Y))
        {
            throw new Exception("Talk button hit rect must contain its own center.");
        }

        float overviewCtaY = contentY + layout.S(44f);
        var ctaRect = new Microsoft.Xna.Framework.Rectangle(
            (int)left,
            (int)overviewCtaY,
            (int)layout.S(140f),
            (int)layout.S(28f));
        if (ctaRect.Bottom > panelY + panelH - layout.S(footerHeight))
        {
            throw new Exception("Overview next-action CTA must not clip into footer.");
        }

        if (contentY + layout.S(200f) > panelY + panelH - layout.S(footerHeight))
        {
            throw new Exception("Overview dashboard content must not clip at 1280x720.");
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
        if (!villages.TryAssignJob(village, lumberjack, JobType.Lumber, target).Success)
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

    public static void RunLumberJobsIgnoreLeaves()
    {
        Console.Write("Running Lumber Jobs Ignore Leaves Test... ");
        if (GatherBlockClassifier.GetCategory(BlockType.OakLeaves).HasValue ||
            GatherBlockClassifier.GetCategory(BlockType.BirchLeaves).HasValue ||
            GatherBlockClassifier.GetCategory(BlockType.PineLeaves).HasValue)
        {
            throw new Exception("Leaves should decay naturally and must not be assigned as lumber targets.");
        }

        if (GatherBlockClassifier.GetCategory(BlockType.OakLog) != GatherCategory.Lumber)
        {
            throw new Exception("Logs should still be valid lumber targets.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunGatherJobsIgnoreVillageStructures()
    {
        Console.Write("Running Gather Jobs Ignore Village Structures Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(9190);
        int ax = 48;
        int az = 48;
        world.UpdateChunksAround(null, new Vector3(ax + 0.5f, 64f, az + 0.5f), 2);
        TryFoundTestVillage(villages, world, "Protected Gather", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        if (!PlayerStructureRegistry.TryGet("lumber_camp", out var lumberCampBlueprint))
        {
            throw new Exception("lumber_camp blueprint missing.");
        }

        int campX = village.AnchorX + 8;
        int campZ = village.AnchorZ;
        int campY = StructureFingerprint.FindSurfaceAnchorY(world, campX, campZ);
        StampTemplate(world, campX, campY, campZ, lumberCampBlueprint.Template);
        village.RegisterClaimedBuilding(lumberCampBlueprint, campX, campY, campZ);

        int naturalLogX = campX + 6;
        int naturalLogZ = campZ;
        int naturalLogY = world.GetHighestSolidY(naturalLogX, naturalLogZ) + 1;
        world.SetBlock(naturalLogX, naturalLogY, naturalLogZ, BlockType.OakLog);

        var lumberCamp = village.GetNearestBuilding(BuildingKind.LumberCamp, village.Center);
        var lumberTarget = JobTargetScanner.FindNearbyLumberTarget(world, village, lumberCamp, village.Center);
        if (!lumberTarget.HasValue ||
            (int)MathF.Floor(lumberTarget.Value.X) != naturalLogX ||
            (int)MathF.Floor(lumberTarget.Value.Y) != naturalLogY ||
            (int)MathF.Floor(lumberTarget.Value.Z) != naturalLogZ)
        {
            throw new Exception("Lumber target selection should ignore village structure logs and pick the natural tree.");
        }

        village.WorkQueue.Enqueue(campX - 1, campY + 1, campZ - 1);
        village.WorkQueue.Enqueue(naturalLogX, naturalLogY, naturalLogZ);
        if (!village.WorkQueue.TryGetNextForRole(VillagerRole.Lumberjack, world, village, out int queuedLogX, out int queuedLogY, out int queuedLogZ) ||
            queuedLogX != naturalLogX ||
            queuedLogY != naturalLogY ||
            queuedLogZ != naturalLogZ)
        {
            throw new Exception("Queued lumber work should skip protected village structure logs.");
        }

        if (!PlayerStructureRegistry.TryGet("quarry", out var quarryBlueprint))
        {
            throw new Exception("quarry blueprint missing.");
        }

        int quarryX = village.AnchorX;
        int quarryZ = village.AnchorZ + 8;
        int quarryY = StructureFingerprint.FindSurfaceAnchorY(world, quarryX, quarryZ);
        StampTemplate(world, quarryX, quarryY, quarryZ, quarryBlueprint.Template);
        village.RegisterClaimedBuilding(quarryBlueprint, quarryX, quarryY, quarryZ);

        int naturalStoneX = quarryX + 6;
        int naturalStoneZ = quarryZ;
        int naturalStoneY = world.GetHighestSolidY(naturalStoneX, naturalStoneZ) + 1;
        world.SetBlock(naturalStoneX, naturalStoneY, naturalStoneZ, BlockType.Stone);

        var quarry = village.GetNearestBuilding(BuildingKind.Quarry, village.Center);
        var mineTarget = quarry == null ? null : JobTargetScanner.FindNearbyMineTarget(world, village, quarry);
        if (!mineTarget.HasValue)
        {
            throw new Exception("Expected a mine target near the quarry.");
        }

        int mineX = (int)MathF.Floor(mineTarget.Value.X);
        int mineY = (int)MathF.Floor(mineTarget.Value.Y);
        int mineZ = (int)MathF.Floor(mineTarget.Value.Z);
        if (village.IsProtectedStructureBlock(mineX, mineY, mineZ, world.GetBlock(mineX, mineY, mineZ)))
        {
            throw new Exception("Mine target selection should ignore village structure stone.");
        }

        village.WorkQueue.Enqueue(quarryX, quarryY, quarryZ);
        village.WorkQueue.Enqueue(naturalStoneX, naturalStoneY, naturalStoneZ);
        if (!village.WorkQueue.TryGetNextForRole(VillagerRole.Miner, world, village, out int queuedStoneX, out int queuedStoneY, out int queuedStoneZ) ||
            queuedStoneX != naturalStoneX ||
            queuedStoneY != naturalStoneY ||
            queuedStoneZ != naturalStoneZ)
        {
            throw new Exception("Queued mining work should skip protected village structure blocks.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillagersCollectNearbySaplingDrops()
    {
        Console.Write("Running Villager Sapling Drop Collection Test... ");
        var session = new GameSession(9321);
        var world = session.Grid;
        int ax = GameConstants.DefaultSpawnX + 18;
        int az = GameConstants.DefaultSpawnZ + 18;
        world.UpdateChunksAround(null, new Vector3(ax + 0.5f, 68f, az + 0.5f), 2);

        if (!TryFoundTestVillage(session.Villages, world, "Drop Pickup", ax, az, out var village) || village == null)
        {
            throw new Exception("Village missing.");
        }

        var worker = session.Villagers.Spawn(village.Id, village.Center + new Vector3(0f, 1f, 0f), 47);
        worker.Role = VillagerRole.Lumberjack;
        worker.AssignJob(JobType.Lumber, worker.Position, null);
        village.RegisterVillager(worker.Id);
        session.Villagers.Update(0f, world, session.Villages.Villages);

        int before = village.Storage.CountBlock(BlockType.OakSapling);
        session.SpawnItemDrop(ItemStack.CreateBlock(BlockType.OakSapling, 1), worker.Position + new Vector3(0.15f, 0.45f, 0.15f));
        for (int i = 0; i < 8; i++)
        {
            session.UpdateItemDrops(0.1f);
        }

        if (village.Storage.CountBlock(BlockType.OakSapling) <= before)
        {
            throw new Exception("Nearby working villager did not collect sapling drop into village storage.");
        }

        if (session.ItemEntities.Count != 0)
        {
            throw new Exception("Collected sapling drop should be removed from the world.");
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

        if (!villages.TryAssignJob(village, lumberjack, JobType.Lumber, new Vector3(x + 0.5f, logY, z + 0.5f)).Success)
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

    public static void RunVillageOrganicFamilyGrowth()
    {
        Console.Write("Running Village Organic Family Growth Test... ");
        var villagers = new VillagerManager();
        var village = new VillageEntity("Family Growth", 0, 64, 0);
        var founder = villagers.Spawn(village.Id, new Vector3(0.5f, 65f, 0.5f), 12);
        village.RegisterVillager(founder.Id);
        village.PopulationCap = 3;
        village.FoodStock = 30f;
        village.Happiness = 1f;
        village.RegisterClaimedBuilding(new BuildingBlueprint
        {
            Id = "peasant_house",
            DisplayName = "Peasant House",
            Kind = BuildingKind.House,
            PopulationCapBonus = 2
        }, 2, 64, 0);
        village.RegisterClaimedBuilding(new BuildingBlueprint
        {
            Id = "farm_plot",
            DisplayName = "Farm Plot",
            Kind = BuildingKind.FarmPlot
        }, 4, 64, 0);

        var world = new VoxelWorld(4040);
        var haulCoordinator = new HaulCoordinator(villagers);
        var dispatcher = new JobDispatcher(villagers, haulCoordinator);
        var simulation = new VillageSimulation(villagers, dispatcher, haulCoordinator);
        string? lastToast = null;
        simulation.SetVillageEvents(new Autonocraft.Village.VillageEvents { ShowToast = msg => lastToast = msg });
        var finalizedSites = new HashSet<int>();

        for (int i = 0; i < 4 && VillageSettlementHealth.GetLivePopulation(village, villagers) < 2; i++)
        {
            simulation.Update(new[] { village }, finalizedSites, 120f, world, DayNightCycle.Noon, null!);
        }

        if (VillageSettlementHealth.GetLivePopulation(village, villagers) < 2)
        {
            throw new Exception("Expected surplus food and housing to organically attract a new family.");
        }

        if (village.FoodStock >= 30f)
        {
            throw new Exception("Expected family growth to consume food comfort.");
        }

        if (string.IsNullOrWhiteSpace(lastToast) ||
            !lastToast.Contains("family", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Expected family arrival toast.");
        }

        village.PopulationCap = VillageSettlementHealth.GetLivePopulation(village, villagers);
        int cappedPopulation = village.PopulationCap;
        for (int i = 0; i < 3; i++)
        {
            simulation.Update(new[] { village }, finalizedSites, 120f, world, DayNightCycle.Noon, null!);
        }

        if (VillageSettlementHealth.GetLivePopulation(village, villagers) != cappedPopulation)
        {
            throw new Exception("Organic family growth must respect population cap.");
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

    public static void RunSettlementGuidancePriority()
    {
        Console.Write("Running Settlement Guidance Priority Test... ");
        var villagers = new VillagerManager();
        var village = new VillageEntity("Priority Test", 0, 64, 0);
        var v1 = villagers.Spawn(village.Id, new Vector3(0.5f, 65f, 0.5f), 1);
        var v2 = villagers.Spawn(village.Id, new Vector3(1.5f, 65f, 0.5f), 2);
        village.RegisterVillager(v1.Id);
        village.RegisterVillager(v2.Id);
        v1.AssignJob(JobType.Idle, null, null);
        v2.AssignJob(JobType.Idle, null, null);
        village.FoodStock = 0f;
        village.ConsecutiveDaysWithoutFood = 2;

        var guidance = SettlementGuidance.Compute(village, villagers);
        if (guidance.FoodRisk != FoodRiskLevel.Critical)
        {
            throw new Exception($"Expected critical food risk, got {guidance.FoodRisk}");
        }

        if (!guidance.Detail.Contains("critical", StringComparison.OrdinalIgnoreCase) &&
            !guidance.Detail.Contains("Food", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Expected food-critical guidance, got: {guidance.Detail}");
        }

        village.FoodStock = 20f;
        village.ConsecutiveDaysWithoutFood = 0;
        var idleGuidance = SettlementGuidance.Compute(village, villagers);
        if (!idleGuidance.Detail.Contains("idle", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Expected idle guidance after food restored, got: {idleGuidance.Detail}");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunSettlementDashboardFields()
    {
        Console.Write("Running Settlement Dashboard Fields Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(5151);
        TryFoundTestVillage(villages, world, "Dashboard", 32, 32, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        var v = villagers.Spawn(village.Id, village.Center + new Vector3(1f, 1f, 0f), 3);
        village.RegisterVillager(v.Id);
        v.AssignJob(JobType.Idle, null, null);
        village.PopulationCap = 4;
        village.FoodStock = 0f;
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, VillageEntity.RecruitFoodCost));
        if (PlayerStructureRegistry.TryGet("farm_plot", out var farmBlueprint))
        {
            village.QueueBuild(farmBlueprint, village.AnchorX + 4, village.AnchorY, village.AnchorZ);
        }

        var vm = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        if (vm.IdleWorkerCount < 1)
        {
            throw new Exception("Expected idle worker count on dashboard.");
        }

        if (vm.FoodRiskLevel == FoodRiskLevel.Ok)
        {
            throw new Exception("Expected non-ok food risk with low food stock.");
        }

        if (vm.PendingBuildCount < 1)
        {
            throw new Exception("Expected pending build count.");
        }

        if (string.IsNullOrWhiteSpace(vm.NextAction))
        {
            throw new Exception("Expected next action text.");
        }

        AssertOnboardingState(vm, "secure food", expectedBlocked: false);

        Console.WriteLine("PASSED");
    }

    public static void RunVillagerOnboardingStateFields()
    {
        Console.Write("Running Villager Onboarding State Fields Test... ");
        var (villagers, villages, _, village) = CreateOnboardingVillage("Onboarding");

        var unestablished = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        AssertOnboardingState(unestablished, "found", expectedBlocked: true);

        var establishedVillagers = new VillagerManager();
        var establishedVillages = new VillageManager(establishedVillagers);
        var world = new VoxelWorld(7071);
        establishedVillages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var establishedVillage = establishedVillages.GetPrimaryVillage()
            ?? throw new Exception("Starter settlement missing.");
        while (establishedVillage.VillagerIds.Count > 0)
        {
            establishedVillage.UnregisterVillager(establishedVillage.VillagerIds[0]);
        }

        establishedVillagers.LoadVillagers(Array.Empty<VillagerSaveData>());
        var emptyEstablished = Autonocraft.UI.Village.VillageViewModel.Build(
            establishedVillage,
            establishedVillages,
            establishedVillagers,
            false,
            establishedVillage.Center);
        AssertOnboardingState(emptyEstablished, "repair", expectedBlocked: true);
        if (emptyEstablished.NextActionKind != SettlementActionKind.RepairRoster)
        {
            throw new Exception($"Expected repair-roster next action, got {emptyEstablished.NextActionKind}.");
        }

        SpawnOnboardingVillager(villagers, village, 20);
        village.PopulationCap = 1;
        var capped = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        AssertOnboardingState(capped, "housing", expectedBlocked: true);

        village.PopulationCap = 4;
        var missingMaterials = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        AssertOnboardingState(missingMaterials, "planks", expectedBlocked: true);
        if (!missingMaterials.RecruitPreview.Contains("Need more planks", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Expected recruit preview to explain missing planks, got: {missingMaterials.RecruitPreview}");
        }

        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, VillageEntity.RecruitFoodCost));
        var ready = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        if (ready.IsBlocked)
        {
            throw new Exception($"Expected ready onboarding state, got block: {ready.BlockedReason}");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillagePulseEvolvesDynamically()
    {
        Console.Write("Running Village Pulse Dynamic Flow Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var village = new VillageEntity("Pulse", 20, 64, 20);
        villages.RegisterVillageForTest(village);

        var empty = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        if (!empty.Pulse.Focus.Contains("Town Heart", StringComparison.OrdinalIgnoreCase) ||
            empty.Pulse.CanTrade ||
            empty.Pulse.CanDelegate)
        {
            throw new Exception("Expected empty village pulse to point at founding without trade/delegation.");
        }

        for (int i = 0; i < 4; i++)
        {
            var villager = villagers.Spawn(village.Id, village.Center + new Vector3(i + 0.5f, 1f, 0.5f), 900 + i);
            village.RegisterVillager(villager.Id);
            villager.AssignJob(i == 0 ? JobType.Lumber : JobType.Idle, null, null);
        }

        village.FoodStock = 14f;
        village.PopulationCap = 6;
        village.RegisterClaimedBuilding(new BuildingBlueprint
        {
            Id = "peasant_house",
            DisplayName = "Peasant House",
            Kind = BuildingKind.House
        }, 22, 64, 20);
        village.RegisterClaimedBuilding(new BuildingBlueprint
        {
            Id = "farm_plot",
            DisplayName = "Farm Plot",
            Kind = BuildingKind.FarmPlot
        }, 24, 64, 20);
        village.RegisterClaimedBuilding(new BuildingBlueprint
        {
            Id = "market",
            DisplayName = "Market",
            Kind = BuildingKind.Market
        }, 26, 64, 20);
        village.Favor = 4;

        var trading = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        if (!trading.Pulse.CanTrade ||
            trading.Pulse.CanDelegate ||
            trading.Pulse.FavorBalance <= 0 ||
            !trading.Pulse.CanGrowFamily ||
            !trading.Pulse.GrowthHook.Contains("family", StringComparison.OrdinalIgnoreCase) ||
            !trading.Pulse.TradeHook.Contains("favor", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Expected market village pulse to expose favor trading but not delegation yet.");
        }

        village.RegisterClaimedBuilding(new BuildingBlueprint
        {
            Id = "workshop",
            DisplayName = "Workshop",
            Kind = BuildingKind.Workshop
        }, 28, 64, 20);
        village.Favor = 20;

        var delegated = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        if (!delegated.Pulse.CanDelegate ||
            delegated.Pulse.AgentWorkOrderCost <= 0 ||
            !delegated.Pulse.DelegationHook.Contains("favor", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Expected workshop village pulse to unlock favor-priced delegation.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageFavorAndContracts()
    {
        Console.Write("Running Village Favor and Contracts Test... ");
        var villagers = new VillagerManager();
        var village = new VillageEntity("Contracts", 20, 64, 20);
        for (int i = 0; i < 4; i++)
        {
            var villager = villagers.Spawn(village.Id, village.Center + new Vector3(i + 0.5f, 1f, 0.5f), 700 + i);
            village.RegisterVillager(villager.Id);
        }

        village.PopulationCap = 4;
        village.FoodStock = 20f;
        village.Happiness = 1f;
        village.RegisterClaimedBuilding(new BuildingBlueprint
        {
            Id = "market",
            DisplayName = "Market",
            Kind = BuildingKind.Market
        }, 22, 64, 20);

        int beforeFavor = village.Favor;
        village.UpdateSimulation(120f, 0.5f);
        if (village.Favor <= beforeFavor)
        {
            throw new Exception("Expected market village to earn favor from daily surplus.");
        }

        village.Favor = 20;
        if (!VillageAgentContracts.TryAccept(village, villagers, "housing", out string message))
        {
            throw new Exception($"Expected housing contract to be accepted: {message}");
        }

        if (village.Favor >= 20)
        {
            throw new Exception("Expected contract to spend favor.");
        }

        var goal = village.Scheduler.GetTopOpenGoal();
        if (goal == null || goal.Kind != VillageGoalKind.Build || goal.BlueprintId != "peasant_house")
        {
            throw new Exception("Expected housing contract to create a Peasant House build goal.");
        }

        int favorAfterAccept = village.Favor;
        if (VillageAgentContracts.TryAccept(village, villagers, "housing", out message))
        {
            throw new Exception("Expected duplicate housing contract to be blocked.");
        }

        if (village.Favor != favorAfterAccept)
        {
            throw new Exception("Duplicate contract should not spend favor.");
        }

        var housingContract = VillageAgentContracts.Suggest(village, villagers)[0];
        if (!housingContract.AlreadyActive ||
            !housingContract.StatusText.Contains("Queued", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Expected active housing contract to show queued status.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageFoundingCostIsExplicit()
    {
        Console.Write("Running Village Founding Cost Test... ");
        if (!PlayerStructureRegistry.TryGet("town_heart", out var heart))
        {
            throw new Exception("Town Heart blueprint missing.");
        }

        var empty = new Inventory(9);
        if (heart.CanAfford(empty))
        {
            throw new Exception("Town Heart should require an explicit starter kit.");
        }

        empty.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));
        empty.AddItem(ItemStack.CreateBlock(BlockType.Cobblestone, 4));
        if (!heart.CanAfford(empty))
        {
            throw new Exception("Town Heart should be affordable with 8 planks and 4 cobblestone.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunBuildCatalogRecommendationsFollowVillageNeeds()
    {
        Console.Write("Running Build Catalog Recommendation Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var village = new VillageEntity("Build Flow", 20, 64, 20);
        villages.RegisterVillageForTest(village);
        var worker = villagers.Spawn(village.Id, village.Center + new Vector3(1f, 1f, 0f), 42);
        village.RegisterVillager(worker.Id);
        village.PopulationCap = 1;
        village.FoodStock = 10f;

        var vm = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        var context = new VillagePanelContext
        {
            Ui = null!,
            UiLayout = new UiLayout(1280, 720),
            Village = village,
            ViewModel = vm,
            Villagers = villagers,
            PlayerPosition = village.Center,
            PlayerCreative = false,
            PlayWithAi = true
        };

        var first = BuildPanel.GetOrderedBlueprints(context)[0];
        if (first.Kind != BuildingKind.House)
        {
            throw new Exception($"Expected housing recommendation first at cap, got {first.Id}.");
        }

        village.PopulationCap = 6;
        village.FoodStock = 0f;
        vm = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        context = new VillagePanelContext
        {
            Ui = null!,
            UiLayout = new UiLayout(1280, 720),
            Village = village,
            ViewModel = vm,
            Villagers = villagers,
            PlayerPosition = village.Center,
            PlayerCreative = false,
            PlayWithAi = true
        };

        first = BuildPanel.GetOrderedBlueprints(context)[0];
        if (first.Kind != BuildingKind.FarmPlot)
        {
            throw new Exception($"Expected farm recommendation first when food is low, got {first.Id}.");
        }

        for (int i = 0; i < 3; i++)
        {
            var extra = villagers.Spawn(village.Id, village.Center + new Vector3(i + 2f, 1f, 0f), 100 + i);
            village.RegisterVillager(extra.Id);
        }

        village.FoodStock = 20f;
        village.RegisterClaimedBuilding(new BuildingBlueprint
        {
            Id = "market",
            DisplayName = "Market",
            Kind = BuildingKind.Market
        }, 24, 64, 20);
        village.RegisterClaimedBuilding(new BuildingBlueprint
        {
            Id = "workshop",
            DisplayName = "Workshop",
            Kind = BuildingKind.Workshop
        }, 28, 64, 20);

        vm = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        context = new VillagePanelContext
        {
            Ui = null!,
            UiLayout = new UiLayout(1280, 720),
            Village = village,
            ViewModel = vm,
            Villagers = villagers,
            PlayerPosition = village.Center,
            PlayerCreative = false,
            PlayWithAi = true
        };

        first = BuildPanel.GetOrderedBlueprints(context)[0];
        if (first.Kind == BuildingKind.Market || first.Kind == BuildingKind.Workshop)
        {
            throw new Exception("Existing market/workshop should not remain the top recommendation.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunJobAssignmentBlockedReasons()
    {
        Console.Write("Running Job Assignment Blocked Reasons Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var village = new VillageEntity("Blocked", 40, 64, 40);
        villages.RegisterVillageForTest(village);

        var worker = villagers.Spawn(village.Id, village.Center + new Vector3(1f, 1f, 0f), 4);
        village.RegisterVillager(worker.Id);

        var mineResult = villages.TryAssignJob(village, worker, JobType.Mine);
        if (mineResult.Success || mineResult.ReasonCode != JobAssignmentReasonCodes.NoQuarry)
        {
            throw new Exception($"Expected NoQuarry, got {mineResult.ReasonCode}");
        }

        var farmResult = villages.TryAssignJob(village, worker, JobType.Farm);
        if (farmResult.Success || farmResult.ReasonCode != JobAssignmentReasonCodes.NoFarmPlot)
        {
            throw new Exception($"Expected NoFarmPlot, got {farmResult.ReasonCode}");
        }

        var buildResult = villages.TryAssignJob(village, worker, JobType.Build);
        if (buildResult.Success || buildResult.ReasonCode != JobAssignmentReasonCodes.NoPendingSite)
        {
            throw new Exception($"Expected NoPendingSite, got {buildResult.ReasonCode}");
        }

        if (string.IsNullOrWhiteSpace(mineResult.PlayerMessage))
        {
            throw new Exception("Expected player message on blocked mine assign.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillagerActivityTextContext()
    {
        Console.Write("Running Villager Activity Text Context Test... ");
        var villagers = new VillagerManager();
        var village = new VillageEntity("Activity", 10, 64, 10);
        var v = villagers.Spawn(village.Id, new Vector3(12.5f, 65f, 14.5f), 5);
        v.AssignJob(JobType.Lumber, new Vector3(20.5f, 64f, 18.5f), null);

        string activity = VillagerActivityText.Describe(v, village, null);
        if (!activity.Contains("20", StringComparison.Ordinal))
        {
            throw new Exception($"Expected coordinates in activity text, got: {activity}");
        }

        if (PlayerStructureRegistry.TryGet("peasant_house", out var houseBlueprint))
        {
            village.QueueBuild(houseBlueprint, 12, 64, 14);
            var site = village.BuildingSites[0];
            v.AssignJob(JobType.Build, null, site.Id);
        }

        string buildActivity = VillagerActivityText.Describe(v, village, null);
        if (!buildActivity.Contains("peasant", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Expected site name in build activity, got: {buildActivity}");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunRecruitPreviewBlockedReason()
    {
        Console.Write("Running Recruit Preview Blocked Reason Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var village = new VillageEntity("Recruit", 50, 64, 50);
        villages.RegisterVillageForTest(village);

        var v = villagers.Spawn(village.Id, village.Center + new Vector3(1f, 1f, 0f), 6);
        village.RegisterVillager(v.Id);
        village.PopulationCap = 1;

        var capResult = villages.TryRecruit(village, new VoxelWorld(5353));
        if (capResult.Success || capResult.ReasonCode != RecruitReasonCodes.HousingCap)
        {
            throw new Exception($"Expected housing cap recruit block, got {capResult.ReasonCode}");
        }

        village.PopulationCap = 4;
        var materialResult = villages.TryRecruit(village, new VoxelWorld(5353));
        if (materialResult.Success || materialResult.ReasonCode != RecruitReasonCodes.MissingMaterials)
        {
            throw new Exception($"Expected missing materials recruit block, got {materialResult.ReasonCode}");
        }

        var vm = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        if (!vm.RecruitPreview.Contains("plank", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Expected recruit preview to mention plank cost.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunSettlementWellBeingWarnings()
    {
        Console.Write("Running Settlement Well-Being Warnings Test... ");
        var villagers = new VillagerManager();
        var village = new VillageEntity("Wellbeing", 0, 64, 0);
        for (int i = 0; i < 3; i++)
        {
            var v = villagers.Spawn(village.Id, new Vector3(i + 0.5f, 65f, 0.5f), 10 + i);
            village.RegisterVillager(v.Id);
            v.AssignJob(JobType.Idle, null, null);
        }

        village.FoodStock = 0f;
        village.ConsecutiveDaysWithoutFood = 2;
        var critical = SettlementGuidance.Compute(village, villagers);
        if (critical.FoodRisk != FoodRiskLevel.Critical)
        {
            throw new Exception("Expected critical food risk for well-being test.");
        }

        village.FoodStock = 20f;
        village.ConsecutiveDaysWithoutFood = 0;
        var idle = SettlementGuidance.Compute(village, villagers);
        if (idle.NextActionKind != SettlementActionKind.AssignJobs)
        {
            throw new Exception("Expected assign-jobs next action for idle crisis.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunPeopleTabCitizenDifferentiation()
    {
        Console.Write("Running People Tab Citizen Differentiation Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var village = new VillageEntity("People", 0, 64, 0);
        var a = villagers.Spawn(village.Id, new Vector3(0.5f, 65f, 0.5f), 20001);
        var b = villagers.Spawn(village.Id, new Vector3(1.5f, 65f, 1.5f), 90007);
        village.RegisterVillager(a.Id);
        village.RegisterVillager(b.Id);
        a.AssignJob(JobType.Lumber, new Vector3(5.5f, 64f, 5.5f), null);
        b.AssignJob(JobType.Idle, null, null);
        a.Role = VillagerRole.Lumberjack;
        b.Role = VillagerRole.Peasant;

        var vm = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, village.Center);
        if (vm.Villagers.Count < 2)
        {
            throw new Exception("Expected two villager rows.");
        }

        if (vm.Villagers[0].Id == vm.Villagers[1].Id)
        {
            throw new Exception("Expected distinct villager ids.");
        }

        if (vm.Villagers[0].Role == vm.Villagers[1].Role && vm.Villagers[0].Activity == vm.Villagers[1].Activity)
        {
            throw new Exception("Expected distinguishable role or activity.");
        }

        bool hasAttention = false;
        bool hasDistinctActivity = false;
        foreach (var row in vm.Villagers)
        {
            if (row.NeedsAttention)
            {
                hasAttention = true;
            }
        }

        if (vm.Villagers[0].Activity != vm.Villagers[1].Activity)
        {
            hasDistinctActivity = true;
        }

        if (!hasAttention)
        {
            throw new Exception("Expected at least one villager flagged needs attention.");
        }

        if (!hasDistinctActivity)
        {
            throw new Exception("Expected distinct activity summaries per villager.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunAgentStateGuidanceParity()
    {
        Console.Write("Running Agent State Guidance Parity Test... ");
        var session = new GameSession(5151);
        var villages = session.Villages;
        var villagers = session.Villagers;
        session.Grid.UpdateChunksAround(null, new Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f), 3);
        villages.InitializeStarterSettlement(session.Grid, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage() ?? throw new Exception("Starter village missing.");
        session.Player.Position = village.Center + new Vector3(0f, 4f, 0f);
        session.Player.Stats.EarlyGuideStage = 5;
        session.Player.Hunger = SurvivalConstants.MaxHunger;

        foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, villagers))
        {
            villager.AssignJob(JobType.Idle, null, null);
        }

        var expected = SettlementGuidance.Compute(village, villagers, session.Player.Position);
        string hint = EarlyGameGuide.GetGuidanceHint(session.Player, village, villagers);
        if (hint != expected.Headline)
        {
            throw new Exception($"guidanceHint headline mismatch: '{hint}' vs '{expected.Headline}'");
        }

        if (expected.NextActionKind != SettlementActionKind.AssignJobs)
        {
            throw new Exception("Expected assign-jobs priority for idle crisis parity test.");
        }

        int port = GetFreeTcpPort();
        using var bridge = new TestAgentBridge(session);
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        try
        {
            AgentHttpServer.Start(bridge, port);
            WaitForAgentReady(http);
            var state = GetJson(http, "/state");
            string guidanceHint = state.RootElement.GetProperty("guidanceHint").GetString() ?? "";
            string nextAction = state.RootElement.GetProperty("village").GetProperty("nextAction").GetString() ?? "";
            if (guidanceHint != expected.Headline)
            {
                throw new Exception($"/state guidanceHint mismatch: '{guidanceHint}'");
            }

            if (nextAction != expected.Detail)
            {
                throw new Exception($"/state nextAction mismatch: '{nextAction}'");
            }
        }
        finally
        {
            AgentHttpServer.Stop();
        }

        session.Player.Stats.EarlyGuideStage = 2;
        session.Player.Hunger = SurvivalConstants.MaxHunger;
        string? contextNote = EarlyGameGuide.GetTownBoardHudContextNote(session.Player, village, villagers);
        if (string.IsNullOrEmpty(contextNote) || !contextNote.Contains("HUD tip:", StringComparison.Ordinal))
        {
            throw new Exception("Expected Town Board HUD context note during early-guide override.");
        }

        var vm = Autonocraft.UI.Village.VillageViewModel.Build(village, villages, villagers, false, session.Player.Position, session.Player);
        if (string.IsNullOrEmpty(vm.HudContextNote))
        {
            throw new Exception("Expected Overview HudContextNote when HUD and settlement guidance diverge.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunRelinkStrandedCitizens()
    {
        Console.Write("Running Relink Stranded Citizens Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(6161);
        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage() ?? throw new Exception("Starter village missing.");

        foreach (var villager in villagers.All)
        {
            villager.VillageId = village.Id + 50;
        }

        village.ReconcileVillagerRegistry(villagers.All);
        if (VillageSettlementHealth.GetLivePopulation(village, villagers) != 0)
        {
            throw new Exception("Expected zero linked citizens before relink.");
        }

        int linked = VillageSettlementHealth.RelinkStrandedCitizens(village, villagers, new[] { village });
        if (linked < 2)
        {
            throw new Exception($"Expected to relink 2 settlers, got {linked}.");
        }

        if (VillageSettlementHealth.GetLivePopulation(village, villagers) < 2)
        {
            throw new Exception("Expected linked citizens to appear in People tab population.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageScreenOpenRelinksStarterCitizens()
    {
        Console.Write("Running Village Screen Open Relinks Starter Citizens Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(6262);
        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage() ?? throw new Exception("Starter village missing.");

        foreach (var villager in villagers.All)
        {
            villager.VillageId = village.Id + 100;
        }

        village.ReconcileVillagerRegistry(villagers.All);
        if (VillageSettlementHealth.GetLivePopulation(village, villagers) != 0)
        {
            throw new Exception("Expected starter citizens to be temporarily unlinked before opening the board.");
        }

        var screen = new VillageScreen(null!, villagers);
        screen.Open(village, villages, world, village.Center, null, playerCreative: false, playWithAi: true);

        int livePopulation = VillageSettlementHealth.GetLivePopulation(village, villagers);
        if (livePopulation < 2)
        {
            throw new Exception($"Expected board open to relink starter citizens, got {livePopulation}.");
        }

        var viewModelField = typeof(VillageScreen).GetField(
            "_viewModel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (viewModelField?.GetValue(screen) is not VillageViewModel vm || vm.Population < 2)
        {
            throw new Exception("Expected village screen view model to expose relinked starter citizens.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunStarterVillageAutoRestoresZeroPopulation()
    {
        Console.Write("Running Starter Village Auto-Restores Zero Population Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(6363);
        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage() ?? throw new Exception("Starter village missing.");

        while (village.VillagerIds.Count > 0)
        {
            village.UnregisterVillager(village.VillagerIds[0]);
        }

        villagers.LoadVillagers(Array.Empty<VillagerSaveData>());
        if (VillageSettlementHealth.GetLivePopulation(village, villagers) != 0)
        {
            throw new Exception("Expected zero linked citizens before summon.");
        }

        var result = villages.EnsureStarterCitizens(village, world);
        if (!result.Success)
        {
            throw new Exception($"Expected starter village auto-restore to succeed, got {result.PlayerMessage} {result.Remediation}");
        }

        int livePopulation = VillageSettlementHealth.GetLivePopulation(village, villagers);
        if (livePopulation < 2)
        {
            throw new Exception($"Expected starter village auto-restore to link at least 2 citizens, got {livePopulation}.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunStarterSettlementSpawnsAssignedCitizens()
    {
        Console.Write("Running Starter Settlement Spawns Assigned Citizens Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(6363);
        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage() ?? throw new Exception("Starter village missing.");

        int livePopulation = VillageSettlementHealth.GetLivePopulation(village, villagers);
        if (livePopulation < 2)
        {
            throw new Exception($"Starter settlement should spawn assigned citizens immediately, got {livePopulation}.");
        }

        foreach (var villager in villagers.All)
        {
            if (villager.VillageId != village.Id)
            {
                throw new Exception("Starter villager was not assigned to the first village.");
            }
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageAutomationRetasksIdleWorkers()
    {
        Console.Write("Running Village Automation Retasks Idle Workers Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(9393);
        int ax = 40;
        int az = 40;
        world.UpdateChunksAround(null, new Vector3(ax + 0.5f, 64f, az + 0.5f), 2);
        TryFoundTestVillage(villages, world, "Automation", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        EnsureFlatVillagePad(world, village, 12);
        var farm = AddCompletedBuilding(village, "farm_plot", BuildingKind.FarmPlot, 611, village.AnchorX, village.AnchorY, village.AnchorZ + 8);
        PlaceFarmBlocks(world, farm);
        world.SetBlock(farm.AnchorX + 1, farm.AnchorY, farm.AnchorZ, BlockType.Wheat);

        if (!PlayerStructureRegistry.TryGet("peasant_house", out var houseBlueprint))
        {
            throw new Exception("peasant_house blueprint missing.");
        }

        int houseX = village.AnchorX + 8;
        int houseZ = village.AnchorZ;
        ClearBlueprintArea(world, "peasant_house", houseX, village.AnchorY, houseZ);
        village.QueueBuild(houseBlueprint, houseX, village.AnchorY, houseZ);

        var lumberjack = villagers.Spawn(village.Id, village.Center + new Vector3(0f, 1f, 0f), 9011);
        lumberjack.Role = VillagerRole.Lumberjack;
        lumberjack.AssignJob(JobType.Idle, null, null);
        village.RegisterVillager(lumberjack.Id);

        var miner = villagers.Spawn(village.Id, village.Center + new Vector3(1f, 1f, 0f), 9012);
        miner.Role = VillagerRole.Miner;
        miner.AssignJob(JobType.Idle, null, null);
        village.RegisterVillager(miner.Id);

        var smith = villagers.Spawn(village.Id, village.Center + new Vector3(-1f, 1f, 0f), 9013);
        smith.Role = VillagerRole.Smith;
        smith.AssignJob(JobType.Idle, null, null);
        village.RegisterVillager(smith.Id);

        village.FoodStock = 0f;
        villages.AutoAssignIdleWorkers(village, world);

        int farming = 0;
        int building = 0;
        foreach (var citizen in villagers.All)
        {
            if (citizen.VillageId != village.Id)
            {
                continue;
            }

            if (citizen.CurrentJob == JobType.Farm)
            {
                farming++;
            }

            if (citizen.CurrentJob == JobType.Build)
            {
                building++;
            }
        }

        if (farming < 1)
        {
            throw new Exception("Automation should retask at least one idle villager into farming during a food crisis.");
        }

        if (building < 1)
        {
            throw new Exception("Automation should retask at least one idle villager into building when a site is queued.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillagerPathfinderRoutesAroundObstacle()
    {
        Console.Write("Running Villager Pathfinder Obstacle Route Test... ");
        var world = new VoxelWorld(6464);
        const int y = 65;
        world.EnsureChunksLoaded(new Vector3(2f, y, 0f), chunkRadius: 2);
        for (int x = -2; x <= 6; x++)
        {
            for (int z = -3; z <= 3; z++)
            {
                world.SetBlock(x, y - 1, z, BlockType.Stone);
                world.SetBlock(x, y, z, BlockType.Air);
                world.SetBlock(x, y + 1, z, BlockType.Air);
                world.SetBlock(x, y + 2, z, BlockType.Air);
            }
        }

        for (int z = -1; z <= 1; z++)
        {
            world.SetBlock(2, y, z, BlockType.Stone);
            world.SetBlock(2, y + 1, z, BlockType.Stone);
        }

        var start = new Vector3(0.5f, y, 0.5f);
        var target = new Vector3(4.5f, y, 0.5f);
        if (!VoxelPathfinder.TryFindPath(world, start, target, 12, out var waypoints))
        {
            throw new Exception("Expected pathfinder to route around a simple wall.");
        }

        if (waypoints.Count < 3)
        {
            throw new Exception("Expected obstacle route to include intermediate waypoints.");
        }

        foreach (var wp in waypoints)
        {
            int wx = (int)MathF.Floor(wp.X);
            int wy = (int)MathF.Floor(wp.Y);
            int wz = (int)MathF.Floor(wp.Z);
            if (world.GetBlock(wx, wy, wz).IsCollidable() ||
                world.GetBlock(wx, wy + 1, wz).IsCollidable())
            {
                throw new Exception($"Pathfinder returned blocked waypoint {wp}.");
            }
        }

        Console.WriteLine("PASSED");
    }
}
