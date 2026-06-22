using System;
using System.Linq;
using Autonocraft.Core;
using Autonocraft.Crafting;
using Autonocraft.Domain.Core;
using Autonocraft.Domain.Core;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Tests.Integration;

public static class SurvivalTests
{
    public static void RunNewPlayerStartsWithoutStarterItems()
    {
        Console.Write("Running New Player Starts Without Starter Items Test... ");
        var player = new Player(System.Numerics.Vector3.Zero);

        if (player.Hotbar.Any(stack => !stack.IsEmpty))
        {
            throw new Exception("Expected new survival player to start with an empty hotbar.");
        }

        for (int i = 0; i < player.Storage.SlotCount; i++)
        {
            if (!player.Storage.GetSlot(i).IsEmpty)
            {
                throw new Exception("Expected new survival player storage to start empty.");
            }
        }

        Console.WriteLine("PASSED");
    }

    public static void RunHungerDrain(Player player)
    {
        Console.Write("Running Hunger Drain Test... ");
        player.CreativeMode = false;
        player.Hunger = SurvivalConstants.MaxHunger;
        float start = player.Hunger;

        for (int i = 0; i < 120; i++)
        {
            player.UpdateHunger(1f);
        }

        if (player.Hunger >= start)
        {
            throw new Exception($"Expected hunger to decrease. start={start}, now={player.Hunger}");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunEatFood(Player player)
    {
        Console.Write("Running Eat Food Test... ");
        player.CreativeMode = false;
        player.Hunger = 4f;
        player.Hotbar[0] = ItemStack.CreateFood(ItemId.CookedMeat, 1);
        player.SelectedSlot = 0;

        if (!FoodConsumption.TryEatFromHotbar(player))
        {
            throw new Exception("Failed to eat cooked meat.");
        }

        if (player.Hunger <= 4f)
        {
            throw new Exception($"Expected hunger restore. hunger={player.Hunger}");
        }

        if (!player.Hotbar[0].IsEmpty)
        {
            throw new Exception("Food stack should be consumed.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunAnimalLoot(Player player, AnimalManager animals, VoxelWorld world)
    {
        Console.Write("Running Animal Loot Test... ");
        int surfaceY = world.GetHighestSolidY(20, 20);
        var pig = animals.SpawnAt(AnimalType.Pig, new System.Numerics.Vector3(20.5f, surfaceY + 1f, 20.5f), world);
        if (pig == null)
        {
            throw new Exception("Failed to spawn pig.");
        }

        for (int i = 0; i < player.Hotbar.Length; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        while (pig.IsAlive)
        {
            pig.TakeDamage(20f, player.Position);
        }

        AnimalLoot.GrantKillLoot(player, pig.Type);

        int rawMeat = 0;
        for (int i = 0; i < player.Hotbar.Length; i++)
        {
            if (player.Hotbar[i].IsFood() && player.Hotbar[i].FoodId == ItemId.RawMeat)
            {
                rawMeat += player.Hotbar[i].Count;
            }
        }

        if (rawMeat < 2)
        {
            throw new Exception($"Expected 2 raw meat from pig, got {rawMeat}.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageRations(Player player, Village.Village village)
    {
        Console.Write("Running Village Rations Test... ");
        player.CreativeMode = false;
        player.Hunger = 2f;
        village.FoodStock = 4f;
        float stockBefore = village.FoodStock;

        if (!FoodConsumption.TryTakeRations(player, village))
        {
            throw new Exception("Failed to take rations.");
        }

        if (village.FoodStock >= stockBefore)
        {
            throw new Exception("Food stock should decrease after rations.");
        }

        if (player.Hunger <= 2f)
        {
            throw new Exception("Player hunger should increase after rations.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunNightSpawn(VoxelWorld world, Player player, AnimalManager animals)
    {
        Console.Write("Running Night Spawn Test... ");
        var spawner = new NightThreatSpawner();
        player.CreativeMode = false;
        player.Position = new System.Numerics.Vector3(24.5f, world.GetHighestSolidY(24, 24) + 1f, 24.5f);

        spawner.Update(0f, 0.9f, false, world, player, animals);
        int wolvesAtNight = animals.Animals.Count(a => a.Type == AnimalType.Wolf);

        spawner.Update(0f, DayNightCycle.Noon, false, world, player, animals);
        int wolvesAtNoon = animals.Animals.Count(a => a.Type == AnimalType.Wolf);

        if (wolvesAtNight <= 0)
        {
            throw new Exception("Expected wolf spawn at night.");
        }

        if (wolvesAtNoon > 0)
        {
            throw new Exception("Wolves should despawn at noon.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunDeathPenalty(Player player)
    {
        Console.Write("Running Death Penalty Test... ");
        player.Hotbar[0] = ItemStack.CreateBlock(BlockType.Dirt, 8);
        player.Hotbar[1] = ItemStack.CreateBlock(BlockType.Stone, 8);
        player.Hotbar[2] = ItemStack.CreateFood(ItemId.Bread, 3);
        int before = player.Hotbar.Count(s => !s.IsEmpty);

        DeathConsequences.ApplyOnDeath(player);
        int after = player.Hotbar.Count(s => !s.IsEmpty);

        if (after >= before)
        {
            throw new Exception($"Expected fewer hotbar slots after death. before={before}, after={after}");
        }

        DeathConsequences.ApplyOnRespawn(player);
        if (player.Hunger < SurvivalConstants.MaxHunger * 0.5f)
        {
            throw new Exception($"Expected partial hunger on respawn. hunger={player.Hunger}");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunHungerSaveRoundTrip(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Hunger Save Round-Trip Test... ");
        const string slotId = "test-hunger";
        player.Hunger = 11.5f;
        player.MaxHunger = SurvivalConstants.MaxHunger;
        player.CreativeMode = false;

        var snapshot = game.Session.BuildSaveSnapshot(
            slotId, "Hunger Test", game.TimeOfDay, game.TimeScale, game.TimePaused,
            GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var saveData = WorldSaveManager.BuildFromSnapshot(snapshot);
        WorldSaveManager.Save(saveData);

        var loaded = WorldSaveManager.Load(slotId);
        var loadedPlayer = new Player(System.Numerics.Vector3.Zero);
        WorldSaveManager.ApplyPlayerSaveData(loadedPlayer, loaded.Player);

        if (MathF.Abs(loadedPlayer.Hunger - 11.5f) > 0.01f)
        {
            throw new Exception($"Hunger mismatch after save/load: {loadedPlayer.Hunger}");
        }

        Console.WriteLine("PASSED");
    }
}
