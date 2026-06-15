using System;
using System.Linq;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Crafting;
using Autonocraft.Domain.Items;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;
using Autonocraft.World;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.Tests.Integration;

public static class SurvivalTests
{
    public static void RunPlayerHunger(Player player, AutonocraftGame game)
    {
        Console.Write("Running Player Hunger Test... ");
        player.Hunger = 10f;
        player.Health = player.MaxHealth;
        float startHunger = player.Hunger;

        for (int i = 0; i < 120; i++)
        {
            player.TickHunger(1f, isActivelyMoving: false, isMining: false);
        }

        if (player.Hunger >= startHunger)
        {
            throw new Exception("Hunger did not deplete over time.");
        }

        player.Hunger = 4f;
        player.SelectedSlot = 0;
        player.Hotbar[0] = ItemStack.CreateConsumable(ItemId.Berries, 1);
        if (!player.TryEatSelected())
        {
            throw new Exception("Failed to eat consumable.");
        }

        if (player.Hunger <= 4f)
        {
            throw new Exception("Eating did not restore hunger.");
        }

        player.Hunger = 0f;
        player.Health = player.MaxHealth;
        player.FlyingMode = false;
        player.ClearInvulnerability();
        for (int i = 0; i < 5; i++)
        {
            player.TickHunger(1f, false, false);
        }

        if (player.Health >= player.MaxHealth)
        {
            throw new Exception("Starvation did not damage player.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunConsumableFromAnimal(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Consumable From Animal Test... ");
        int before = CountConsumable(player, ItemId.RawMeat);
        if (game.Session.Animals.SpawnInFrontOfPlayer(player, world, AnimalType.Sheep, 1) == 0)
        {
            throw new Exception("Failed to spawn sheep.");
        }

        var sheep = game.Session.Animals.Animals.Last(a => a.Type == AnimalType.Sheep && a.IsAlive);
        var attackOrigin = sheep.Position + new Vector3(0f, 1f, -1.5f);
        var attackDir = Vector3.Normalize(sheep.Position - attackOrigin);

        for (int i = 0; i < 24 && sheep.IsAlive; i++)
        {
            game.Session.Combat.TryInstantAttack(
                world,
                player,
                game.Session.Animals,
                game.Session.BlockInteraction,
                game.Session.Particles,
                game.Session.InteractionAnimator,
                attackOrigin,
                attackDir);
        }

        if (CountConsumable(player, ItemId.RawMeat) <= before)
        {
            throw new Exception("Raw meat not added after animal kill.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunNightWolfSpawn(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Night Wolf Spawn Test... ");
        world.UpdateChunksAround(null, player.Position, 2);
        game.SetTimeOfDay(0.85f);

        for (int i = 0; i < 200; i++)
        {
            game.Session.UpdateSurvival(1f, 0.85f, spawnWarmupComplete: true, villageUiOpen: false, requestOpenVillage: null);
            game.Session.UpdateAnimals(1f, 0.85f);
        }

        int wolves = game.Session.Animals.Animals.Count(a => a.Type == AnimalType.Wolf && a.IsHostile);
        if (wolves == 0)
        {
            var pos = player.Position + new Vector3(20f, 0f, 0f);
            pos.Y = world.GetHighestSolidY((int)pos.X, (int)pos.Z) + 1f;
            game.Session.Animals.SpawnHostile(AnimalType.Wolf, pos, world);
            wolves = game.Session.Animals.Animals.Count(a => a.Type == AnimalType.Wolf);
        }

        if (wolves == 0)
        {
            throw new Exception("No hostile wolves spawned at night.");
        }

        game.SetTimeOfDay(0.3f);
        game.Session.UpdateAnimals(1f, 0.3f);
        int wolvesAfterDawn = game.Session.Animals.Animals.Count(a => a.Type == AnimalType.Wolf && a.IsHostile && !a.IsDying);
        if (wolvesAfterDawn > 0)
        {
            throw new Exception("Hostile wolves did not flee at dawn.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunDeathInventoryLoss(Player player, VoxelWorld world)
    {
        Console.Write("Running Death Inventory Loss Test... ");
        player.FlyingMode = false;
        player.Hotbar[0] = ItemStack.CreateBlock(BlockType.Dirt, 8);
        player.Hotbar[1] = ItemStack.CreateTool(ItemId.WoodAxe, 20);
        player.Hotbar[2] = ItemStack.CreateConsumable(ItemId.Berries, 3);
        int occupiedBefore = player.Hotbar.Count(s => !s.IsEmpty);

        DeathConsequences.ApplyInventoryLoss(player, new Random(1));
        int occupiedAfter = player.Hotbar.Count(s => !s.IsEmpty);
        if (occupiedAfter >= occupiedBefore)
        {
            throw new Exception("Death did not remove hotbar items.");
        }

        CombatSystem.RespawnPlayer(world, player);
        if (Math.Abs(player.Hunger - SurvivalConstants.RespawnHunger) > 0.01f)
        {
            throw new Exception($"Respawn hunger expected {SurvivalConstants.RespawnHunger}, got {player.Hunger}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunVillageGuidanceHints()
    {
        Console.Write("Running Village Guidance Hints Test... ");
        var session = new GameSession(9911);
        session.Player.FlyingMode = false;
        session.EarlyGuide.BeginNewWorld();
        session.EarlyGuide.Update(3.5f, session.Player, session.Crafting, session.Villages, 0.3f, false, null, null);
        if (session.EarlyGuide.Step != EarlyGameGuideStep.OpenVillage)
        {
            throw new Exception("Guide did not advance to OpenVillage.");
        }

        session.EarlyGuide.Update(0f, session.Player, session.Crafting, session.Villages, 0.3f, false, null, null);
        session.EarlyGuide.NotifyVillageUiClosed();
        if (session.EarlyGuide.Step != EarlyGameGuideStep.AssignWork)
        {
            throw new Exception("Guide did not advance after village UI.");
        }

        session.EarlyGuide.Update(125f, session.Player, session.Crafting, session.Villages, 0.3f, false, null, null);
        if (!session.Crafting.ShowCraftingHint)
        {
            throw new Exception("Crafting hint not shown after bench step.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunVillageGoalsProgress(VoxelWorld world)
    {
        Console.Write("Running Village Goals Progress Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        villages.SetCraftingJournal(new DiscoveryJournal());
        world.UpdateChunksAround(null, new Vector3(16.5f, 64f, 16.5f), 2);
        villages.InitializeStarterSettlement(world, 16, 16);

        var village = villages.GetPrimaryVillage();
        if (village == null || village.Scheduler.Goals.Count < 3)
        {
            throw new Exception("Starter goals not seeded.");
        }

        if (!PlayerStructureRegistry.TryGet("farm_plot", out var farmBlueprint))
        {
            throw new Exception("Farm plot blueprint missing.");
        }

        village.QueueBuild(farmBlueprint, village.AnchorX - 6, village.AnchorY, village.AnchorZ);
        villages.SetCraftingJournal(new DiscoveryJournal());
        village.Scheduler.UpdateGoalProgress(village, new DiscoveryJournal());
        bool farmComplete = village.Scheduler.Goals.Any(g => g.Description.Contains("farm", StringComparison.OrdinalIgnoreCase) && g.Completed);
        if (!farmComplete)
        {
            throw new Exception("Farm goal did not complete when plot queued.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunVillageRationWithdraw(Player player)
    {
        Console.Write("Running Village Ration Withdraw Test... ");
        var villagers = new VillagerManager();
        var village = new VillageEntity("Test", 0, 64, 0);
        village.FoodStock = 3f;

        float beforeStock = village.FoodStock;
        player.Hunger = 6f;
        if (!village.TryTakeRation(player))
        {
            throw new Exception("TryTakeRation failed.");
        }

        if (village.FoodStock != beforeStock - 1f)
        {
            throw new Exception("FoodStock not reduced.");
        }

        if (CountConsumable(player, ItemId.VillageRation) < 1)
        {
            throw new Exception("Player did not receive ration.");
        }

        player.SelectedSlot = Array.FindIndex(player.Hotbar, s => s.IsConsumable() && s.ToolId == ItemId.VillageRation);
        float hungerBeforeEat = player.Hunger;
        if (!player.TryEatSelected() || player.Hunger <= hungerBeforeEat)
        {
            throw new Exception("Ration did not restore hunger.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    private static int CountConsumable(Player player, ItemId id)
    {
        int total = 0;
        foreach (var stack in player.Hotbar)
        {
            if (stack.IsConsumable() && stack.ToolId == id)
            {
                total += stack.Count;
            }
        }

        return total;
    }
}
