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
using Autonocraft.Domain.Items;
using Autonocraft.Domain.Village;
using Autonocraft.Domain.World;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Tests.Integration;

public static partial class VillageTests
{
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

        if (village.Storage.CountBlock(BlockType.Dirt) < 16)
        {
            throw new Exception("Starter settlement is missing dirt for the queued farm plot.");
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

        bool farmStarted = false;
        for (int tick = 0; tick < 240 && !farmStarted; tick++)
        {
            session.Villages.Update(0.05f, world, 0.3f, session.Animals);
            var activeFarmSite = village.BuildingSites.FirstOrDefault(site => site.BlueprintId == "farm_plot");
            farmStarted = activeFarmSite == null
                ? village.HasCompletedBuilding("farm_plot")
                : activeFarmSite.CompletionRatio > 0f;
        }

        if (!farmStarted)
        {
            throw new Exception("Expected starter farm plot to make build progress shortly after workers begin.");
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

    public static void RunVillagerOnboardingReopenKeepsLatestState()
    {
        Console.Write("Running Villager Onboarding Reopen Keeps Latest State Test... ");
        var (villagers, villages, world, village) = CreateOnboardingVillage("Reopen", 6465);
        var villager = SpawnOnboardingVillager(villagers, village, 64);
        village.PopulationCap = 3;

        var screen = new Autonocraft.UI.VillageScreen(null!, villagers);
        screen.Open(village, villages, world, village.Center, null, playerCreative: false, playWithAi: true);
        var vm = ReadVillageScreenViewModel(screen);
        AssertOnboardingState(vm, "planks", expectedBlocked: true);

        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, VillageEntity.RecruitFoodCost));
        villager.AssignJob(JobType.Lumber, new Vector3(village.AnchorX + 5.5f, village.AnchorY, village.AnchorZ + 5.5f), null);
        screen.RefreshAfterVillageAction();
        vm = ReadVillageScreenViewModel(screen);
        if (vm.IsBlocked)
        {
            throw new Exception($"Expected refreshed board to clear material blocker, got: {vm.BlockedReason}");
        }

        screen.Close();
        screen.Open(village, villages, world, village.Center, null, playerCreative: false, playWithAi: true);
        vm = ReadVillageScreenViewModel(screen);
        if (vm.Population != 1 || vm.Villagers.Count != 1)
        {
            throw new Exception($"Expected reopened board to keep exactly one linked villager, got pop={vm.Population}, rows={vm.Villagers.Count}.");
        }

        if (vm.IsBlocked)
        {
            throw new Exception("Reopened board should retain the cleared recruit blocker.");
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

    public static void RunTownHeartPlacementExcavatesUnevenTerrain()
    {
        Console.Write("Running Town Heart Uneven Terrain Placement Test... ");
        if (!PlayerStructureRegistry.TryGet("town_heart", out var heart))
        {
            throw new Exception("Town Heart blueprint missing.");
        }

        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(9801);
        villages.SetWorldSeed(world.Seed);

        int ax = 96;
        int ay = 68;
        int az = 96;
        PrepareTownHeartTerrain(world, heart, ax, ay, az);

        int bumpX = ax + 1;
        int bumpZ = az + 1;
        world.SetBlock(bumpX, ay, bumpZ, BlockType.Grass);
        world.SetBlock(bumpX, ay + 1, bumpZ, BlockType.Stone);

        var payer = TownHeartStarterKit();
        if (!villages.CanPlaceTownHeart(world, ax, ay, az, payer))
        {
            throw new Exception("Town Heart should accept uneven natural terrain.");
        }

        if (!villages.TryFoundVillage(world, "Uneven Founding", ax, az, out var village, ay) || village == null)
        {
            throw new Exception("Town Heart founding failed on excavatable terrain.");
        }

        if (world.GetBlock(bumpX, ay, bumpZ) != BlockType.Air ||
            world.GetBlock(bumpX, ay + 1, bumpZ) != BlockType.Air)
        {
            throw new Exception("Town Heart founding did not excavate terrain bumps inside the footprint.");
        }

        if (village.AnchorY != ay)
        {
            throw new Exception($"Town Heart founded at wrong height: expected {ay}, got {village.AnchorY}.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunTownHeartPlacementRejectsBuiltObjects()
    {
        Console.Write("Running Town Heart Built Object Rejection Test... ");
        if (!PlayerStructureRegistry.TryGet("town_heart", out var heart))
        {
            throw new Exception("Town Heart blueprint missing.");
        }

        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(9802);

        int ax = 112;
        int ay = 68;
        int az = 112;
        PrepareTownHeartTerrain(world, heart, ax, ay, az);
        world.SetBlock(ax + 1, ay, az + 1, BlockType.Chest);

        if (villages.CanPlaceTownHeart(world, ax, ay, az, TownHeartStarterKit()))
        {
            throw new Exception("Town Heart should reject built objects inside the excavation volume.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunTownHeartFoundingUsesSurfaceAnchor()
    {
        Console.Write("Running Town Heart Surface Anchor Founding Test... ");
        var session = new GameSession(5151);
        session.Grid.UpdateChunksAround(null, session.Player.Position, 2);
        int ax = (int)MathF.Floor(session.Player.Position.X);
        int az = (int)MathF.Floor(session.Player.Position.Z);

        if (!session.Villages.TryFoundVillage(session.Grid, "Surface Anchor", ax, az, out var village) || village == null)
        {
            throw new Exception("Town Heart founding failed at normal spawn terrain.");
        }

        int surfaceY = session.Grid.GetHighestSolidY(ax, az);
        if (village.AnchorY < surfaceY - 1)
        {
            throw new Exception($"Town Heart founded underground at Y={village.AnchorY}; surface is {surfaceY}.");
        }

        Console.WriteLine("PASSED");
    }

    private static Inventory TownHeartStarterKit()
    {
        var inventory = new Inventory(9);
        inventory.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));
        inventory.AddItem(ItemStack.CreateBlock(BlockType.Cobblestone, 4));
        return inventory;
    }

    private static void PrepareTownHeartTerrain(VoxelWorld world, BuildingBlueprint heart, int ax, int ay, int az)
    {
        world.UpdateChunksAround(null, new Vector3(ax + 0.5f, ay + 2f, az + 0.5f), 2);
        BlueprintPlacementHelper.GetWorldBounds(heart, ax, ay, az, out int minX, out _, out int minZ, out int maxX, out _, out int maxZ);

        for (int x = minX - 2; x <= maxX + 2; x++)
        {
            for (int z = minZ - 2; z <= maxZ + 2; z++)
            {
                for (int y = 1; y < Chunk.Height; y++)
                {
                    world.SetBlock(x, y, z, BlockType.Air);
                }

                for (int y = Math.Max(1, ay - 4); y < ay; y++)
                {
                    world.SetBlock(x, y, z, BlockType.Dirt);
                }

                world.SetBlock(x, ay - 1, z, BlockType.Grass);
            }
        }
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
        if (!session.Villages.TryAssignJob(village, worker, JobType.Lumber).Success)
        {
            throw new Exception("Live-style Lumber assignment failed without explicit target.");
        }

        if (worker.JobTarget == null)
        {
            throw new Exception("Lumber assignment did not resolve a target.");
        }

        Console.WriteLine("PASSED");
    }

}
