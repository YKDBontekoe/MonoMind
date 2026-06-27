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
    private static (VillagerManager Villagers, VillageManager Villages, VoxelWorld World, VillageEntity Village)
        CreateOnboardingVillage(string name, int seed = 7070)
    {
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(seed);
        var village = new VillageEntity(name, 40, 64, 40);
        villages.RegisterVillageForTest(village);
        return (villagers, villages, world, village);
    }

    private static Villager SpawnOnboardingVillager(
        VillagerManager villagers,
        VillageEntity village,
        int seed = 1,
        JobType job = JobType.Idle)
    {
        var villager = villagers.Spawn(village.Id, village.Center + new Vector3(seed * 0.25f, 1f, 0f), seed);
        villager.AssignJob(job, null, null);
        village.RegisterVillager(villager.Id);
        return villager;
    }

    private static void AssertOnboardingState(
        Autonocraft.UI.Village.VillageViewModel vm,
        string expectedStep,
        bool expectedBlocked)
    {
        if (!vm.StarterStep.Contains(expectedStep, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Expected starter step to contain '{expectedStep}', got '{vm.StarterStep}'.");
        }

        if (vm.IsBlocked != expectedBlocked)
        {
            throw new Exception($"Expected IsBlocked={expectedBlocked}, got {vm.IsBlocked}.");
        }

        if (expectedBlocked && (string.IsNullOrWhiteSpace(vm.BlockedReason) || string.IsNullOrWhiteSpace(vm.Remediation)))
        {
            throw new Exception("Blocked onboarding state must include reason and remediation.");
        }
    }

    private static Autonocraft.UI.Village.VillageViewModel ReadVillageScreenViewModel(Autonocraft.UI.VillageScreen screen)
    {
        var viewModelField = typeof(Autonocraft.UI.VillageScreen).GetField(
            "_viewModel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return viewModelField?.GetValue(screen) as Autonocraft.UI.Village.VillageViewModel
            ?? throw new Exception("Village screen view model was not available.");
    }

    private static void BuildHouseThroughSimulation(
        VillageManager villages,
        VillagerManager villagers,
        VoxelWorld world,
        AnimalManager animals,
        VillageEntity village)
    {
        if (!PlayerStructureRegistry.TryGet("peasant_house", out var houseBlueprint))
        {
            throw new Exception("Peasant house blueprint missing.");
        }

        int houseX = 0;
        int houseZ = 0;
        int houseY = 0;
        bool foundHouseSpot = false;
        for (int radius = 8; radius <= 28 && !foundHouseSpot; radius += 2)
        {
            for (int dx = -radius; dx <= radius && !foundHouseSpot; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) > radius)
                    {
                        continue;
                    }

                    int candidateX = village.AnchorX + dx;
                    int candidateZ = village.AnchorZ + dz;
                    int candidateY = StructureFingerprint.FindSurfaceAnchorY(world, candidateX, candidateZ);
                    ClearBlueprintArea(world, "peasant_house", candidateX, candidateY, candidateZ);
                    if (!villages.CanPlaceBlueprint(world, village, houseBlueprint, candidateX, candidateZ, village.Storage, candidateY))
                    {
                        continue;
                    }

                    houseX = candidateX;
                    houseY = candidateY;
                    houseZ = candidateZ;
                    foundHouseSpot = true;
                    break;
                }
            }
        }

        if (!foundHouseSpot)
        {
            throw new Exception("Could not find valid peasant house anchor.");
        }

        ClearBlueprintArea(world, "peasant_house", houseX, houseY, houseZ);
        if (!villages.TryQueueBlueprint(world, village, "peasant_house", houseX, houseZ, village.Storage, houseY))
        {
            throw new Exception("Could not queue peasant house.");
        }

        BuildingSite? site = null;
        foreach (var pending in village.BuildingSites)
        {
            if (!pending.IsComplete &&
                pending.BlueprintId == "peasant_house" &&
                pending.AnchorX == houseX &&
                pending.AnchorY == houseY &&
                pending.AnchorZ == houseZ)
            {
                site = pending;
                break;
            }
        }

        if (site == null)
        {
            throw new Exception("Peasant house site not queued.");
        }

        var builder = villagers.Spawn(village.Id, new Vector3(houseX + 4.5f, houseY + 1f, houseZ + 4.5f), 7101);
        builder.Role = VillagerRole.Builder;
        builder.IsGrounded = true;
        village.RegisterVillager(builder.Id);
        if (!villages.TryAssignJob(village, builder, JobType.Build, buildingSiteId: site.Id).Success)
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

    private static void ExerciseFarmJob(
        VillageManager villages,
        VillagerManager villagers,
        VoxelWorld world,
        AnimalManager animals,
        VillageEntity village)
    {
        var farm = AddCompletedBuilding(village, "farm_plot", BuildingKind.FarmPlot, 201, village.AnchorX, village.AnchorY, village.AnchorZ + 8);
        PlaceFarmBlocks(world, farm);
        world.SetBlock(farm.AnchorX + 1, farm.AnchorY, farm.AnchorZ, BlockType.Wheat);

        var farmer = villagers.Spawn(village.Id, new Vector3(farm.AnchorX + 1.5f, farm.AnchorY + 1f, farm.AnchorZ + 1.5f), 7201);
        farmer.Role = VillagerRole.Farmer;
        farmer.IsGrounded = true;
        village.RegisterVillager(farmer.Id);
        float foodBefore = village.FoodStock;
        if (!villages.TryAssignJob(village, farmer, JobType.Farm, buildingId: farm.Id).Success)
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
        if (!villages.TryAssignJob(village, miner, JobType.Mine, new Vector3(stoneX + 0.5f, stoneY, stoneZ + 0.5f), buildingId: quarry.Id).Success)
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
        if (!villages.TryAssignJob(village, mason, JobType.Mason, new Vector3(masonX + 0.5f, stoneY, masonZ + 0.5f), buildingId: quarry.Id).Success)
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
        if (!villages.TryAssignJob(village, smith, JobType.Craft, buildingId: workshop.Id).Success)
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
        if (!villages.TryAssignJob(village, cook, JobType.Cook, buildingId: kitchen.Id).Success)
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

        Animal? sheep = null;
        Vector3 sheepPos = village.Center + new Vector3(3f, 1f, 3f);
        for (int radius = 3; radius <= 10 && sheep == null; radius += 2)
        {
            for (int dx = -radius; dx <= radius && sheep == null; dx += 2)
            {
                for (int dz = -radius; dz <= radius; dz += 2)
                {
                    sheepPos = village.Center + new Vector3(dx, 1f, dz);
                    sheep = animals.SpawnAt(AnimalType.Sheep, sheepPos, world);
                    if (sheep != null)
                    {
                        break;
                    }
                }
            }
        }

        if (sheep == null)
        {
            throw new Exception("Could not spawn animal for hunter.");
        }

        var hunter = villagers.Spawn(village.Id, village.Center + new Vector3(2f, 1f, 2f), 7503);
        hunter.Role = VillagerRole.Hunter;
        hunter.IsGrounded = true;
        village.RegisterVillager(hunter.Id);
        if (!villages.TryAssignJob(village, hunter, JobType.Hunt).Success)
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

}
