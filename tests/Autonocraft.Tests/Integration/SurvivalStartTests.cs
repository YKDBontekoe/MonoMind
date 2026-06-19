using System;
using System.Linq;
using Autonocraft.Core;
using Autonocraft.Crafting;
using Autonocraft.Domain.Core;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;
using Autonocraft.World;

namespace Autonocraft.Tests.Integration;

public static class SurvivalStartTests
{
    public static void RunEmptySurvivalStart()
    {
        Console.Write("Running Empty Survival Start Test... ");

        var player = GameSession.CreateDefaultPlayer();

        for (int i = 0; i < player.Hotbar.Length; i++)
        {
            if (!player.Hotbar[i].IsEmpty)
            {
                throw new Exception($"Expected empty hotbar slot {i}, got {player.Hotbar[i].GetDisplayName()}");
            }
        }

        for (int i = 0; i < Player.StorageSlotCount; i++)
        {
            if (!player.Storage.GetSlot(i).IsEmpty)
            {
                throw new Exception($"Expected empty storage slot {i}.");
            }
        }

        Console.WriteLine("PASSED");
    }

    public static void RunBareHandLogProgression(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Bare-Hand Log Progression Test... ");

        for (int i = 0; i < player.Hotbar.Length; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        player.AddItem(ItemStack.CreateBlock(BlockType.OakLog, 1));

        game.Crafting.OpenCrucible(40, 40, 40, BlockType.StationBench);
        game.Crafting.Crucible.InputSlots[0] = ItemStack.CreateBlock(BlockType.OakLog, 1);

        var result = game.Crafting.TryTransmute(world, player, 0.5f);
        if (!result.Succeeded || result.Recipe?.Output != BlockType.OakPlank)
        {
            throw new Exception($"Expected plank craft from log without pre-unlocks: {result.Message}");
        }

        game.Crafting.CloseCrucible();
        Console.WriteLine("PASSED");
    }

    public static void RunEarlyGuideEmptyInventoryHints()
    {
        Console.Write("Running Early Guide Empty Inventory Hints Test... ");

        var player = GameSession.CreateDefaultPlayer();
        var village = new Village.Village("Test", 0, 64, 0, 8);

        string hint = EarlyGameGuide.GetGuidanceHint(player, village, new VillagerManager());
        if (!hint.Contains("Punch trees", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Expected gather-first hint, got '{hint}'.");
        }

        player.Hotbar[0] = ItemStack.CreateBlock(BlockType.OakLog, 1);
        hint = EarlyGameGuide.GetGuidanceHint(player, village, new VillagerManager());
        if (!hint.Contains("craft", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Expected craft hint after gathering, got '{hint}'.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunSurvivalMilestoneSaveRoundTrip(AutonocraftGame game, Player player)
    {
        Console.Write("Running Survival Milestone Save Round-Trip Test... ");

        const string slotId = "test-survival-milestones";
        player.Stats.HasGatheredResource = true;
        player.Stats.HasCraftedPlank = true;
        player.Stats.HasCraftedTool = false;
        player.Stats.HasSecuredFood = true;

        var snapshot = game.Session.BuildSaveSnapshot(
            slotId, "Milestone Test", game.TimeOfDay, game.TimeScale, game.TimePaused,
            GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var saveData = WorldSaveManager.BuildFromSnapshot(snapshot);
        WorldSaveManager.Save(saveData);

        var loaded = WorldSaveManager.Load(slotId);
        var loadedPlayer = new Player(System.Numerics.Vector3.Zero);
        WorldSaveManager.ApplyPlayerSaveData(loadedPlayer, loaded.Player);

        if (!loadedPlayer.Stats.HasGatheredResource || !loadedPlayer.Stats.HasCraftedPlank || !loadedPlayer.Stats.HasSecuredFood)
        {
            throw new Exception("Survival milestone flags did not round-trip.");
        }

        if (loadedPlayer.Stats.HasCraftedTool)
        {
            throw new Exception("HasCraftedTool should remain false after round-trip.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunRespawnNoStarterLoot(Player player)
    {
        Console.Write("Running Respawn No Starter Loot Test... ");

        for (int i = 0; i < player.Hotbar.Length; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        DeathConsequences.ApplyOnRespawn(player);

        for (int i = 0; i < player.Hotbar.Length; i++)
        {
            if (!player.Hotbar[i].IsEmpty)
            {
                throw new Exception("Respawn should not restock starter loot.");
            }
        }

        Console.WriteLine("PASSED");
    }
}
