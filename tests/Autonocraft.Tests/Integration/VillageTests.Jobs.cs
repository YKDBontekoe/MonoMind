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

        if (!villages.TryAssignJob(village, farmer, JobType.Farm, FarmCropHelper.GetBlockCenter(ax, ay, az), buildingId: 1).Success)
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

        if (!session.Villages.TryRecruit(village, world).Success)
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

        if (!villages.TryAssignJob(village, farmer, JobType.Farm, buildingId: 3).Success)
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

        if (!villages.TryAssignJob(village, smith, JobType.Craft).Success)
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
        if (!villages.TryAssignJob(village, lumberjack, JobType.Lumber, buildingId: 1).Success)
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
        if (!villages.TryAssignJob(village, miner, JobType.Mine, target).Success)
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

        if (!villages.TryAssignJob(village, miner, JobType.Mine, target).Success)
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

}
