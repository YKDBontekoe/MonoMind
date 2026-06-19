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
        village.FoodStock = 1f;
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
}
