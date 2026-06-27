using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Crafting;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Tests.Integration;

public static class InteractionTests
{
    public static void RunMiningAndPlacing(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Mining & Placing Test... ");

        int surfaceY = world.GetHighestSolidY(16, 16);
        if (surfaceY < 0)
        {
            throw new Exception("Could not find terrain surface for mining test.");
        }

        const int targetY = 30;
        for (int y = targetY + 1; y <= targetY + 3; y++)
        {
            world.SetBlock(16, y, 16, BlockType.Air);
        }

        player.CreativeMode = true;
        player.Position = new Vector3(16.5f, targetY + 1.2f, 16.5f);
        player.Velocity = Vector3.Zero;

        world.SetBlock(16, targetY, 16, BlockType.Stone);

        player.Yaw = -90f;
        player.Pitch = -89f;
        game.Camera.Position = player.Position + new Vector3(0f, Player.EyeHeight, 0f);
        game.Camera.Yaw = player.Yaw;
        game.Camera.Pitch = player.Pitch;

        // Mine block below
        var originalOnSpawnItemDrop = game.Session.BlockInteraction.OnSpawnItemDrop;
        game.Session.BlockInteraction.OnSpawnItemDrop = null;
        try
        {
            game.SimulateClick(MouseButton.Left);
        }
        finally
        {
            game.Session.BlockInteraction.OnSpawnItemDrop = originalOnSpawnItemDrop;
        }

        BlockType minedBlock = world.GetBlock(16, targetY, 16);
        if (minedBlock != BlockType.Air)
        {
            throw new Exception($"Expected block at (16,{targetY},16) to be mined (Air), but is {minedBlock}");
        }

        // Verify player got Stone in inventory
        bool hasStone = false;
        for (int i = 0; i < 9; i++)
        {
            if (player.Hotbar[i].IsBlock() && player.Hotbar[i].BlockType == BlockType.Stone && player.Hotbar[i].Count > 0)
            {
                hasStone = true;
                // Select this slot for placing
                player.SelectedSlot = i;
                break;
            }
        }

        if (!hasStone)
        {
            var hotbarStr = string.Join(", ", player.Hotbar.Select((item, idx) => $"[{idx}]={item.GetDisplayName()}({item.Count})"));
            var storageStr = string.Join(", ", Enumerable.Range(0, player.Storage.SlotCount).Select(idx => $"[{idx}]={player.Storage.GetSlot(idx).GetDisplayName()}({player.Storage.GetSlot(idx).Count})"));
            throw new Exception($"Player did not collect Stone block in inventory after mining. PlayerHashCode={player.GetHashCode()}. Hotbar: {hotbarStr}. Storage: {storageStr}");
        }

        // Now place the block back on top of the block at Y=39 (which is the terrain grass floor)
        int initialCount = player.Hotbar[player.SelectedSlot].Count;

        game.SimulateClick(MouseButton.Right);

        BlockType placedBlock = world.GetBlock(16, targetY, 16);
        if (placedBlock != BlockType.Stone)
        {
            throw new Exception($"Expected block at (16,{targetY},16) to be placed (Stone), but is {placedBlock}");
        }

        int finalCount = player.Hotbar[player.SelectedSlot].Count;
        if (player.CreativeMode)
        {
            if (finalCount != initialCount)
            {
                throw new Exception($"Creative placement should not consume blocks, went from {initialCount} to {finalCount}");
            }
        }
        else if (finalCount != initialCount - 1)
        {
            throw new Exception($"Expected inventory count to decrease by 1, went from {initialCount} to {finalCount}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunClickPriority(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Click Priority Test... ");

        const int targetY = 30;
        for (int y = targetY + 1; y <= targetY + 3; y++)
        {
            world.SetBlock(16, y, 16, BlockType.Air);
        }

        player.CreativeMode = true;
        player.Position = new Vector3(16.5f, targetY + 1.2f, 16.5f);
        IntegrationTestHelpers.SyncCamera(game, player);

        world.SetBlock(16, targetY, 20, BlockType.Stone);

        for (int y = targetY + 1; y <= targetY + 3; y++)
        {
            world.SetBlock(16, y, 18, BlockType.Air);
        }

        for (int z = 17; z <= 19; z++)
        {
            for (int y = targetY + 1; y <= targetY + 3; y++)
            {
                world.SetBlock(16, y, z, BlockType.Air);
            }
        }

        var sheep = game.Animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, targetY + 1f, 18.5f), world);
        if (sheep == null)
        {
            throw new Exception("Failed to spawn sheep for click priority test.");
        }

        IntegrationTestHelpers.AimAt(player, sheep.Position + new Vector3(0f, sheep.Stats.Height * 0.5f, 0f));
        IntegrationTestHelpers.SyncCamera(game, player);

        float healthBefore = sheep.Health;
        game.SimulateClick(MouseButton.Left);

        if (world.GetBlock(16, targetY, 20) != BlockType.Stone)
        {
            throw new Exception("Block behind sheep was mined instead of attacking the closer animal.");
        }

        if (MathF.Abs(sheep.Health - (healthBefore - CombatSystem.BareHandDamage)) > 0.001f)
        {
            throw new Exception($"Expected sheep to take damage. Health: {healthBefore} -> {sheep.Health}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunSwordMissDoesNotMineBlock(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Sword Miss Does Not Mine Block Test... ");

        const int targetY = 30;
        for (int y = targetY + 1; y <= targetY + 3; y++)
        {
            world.SetBlock(16, y, 16, BlockType.Air);
            world.SetBlock(16, y, 18, BlockType.Air);
            world.SetBlock(16, y, 19, BlockType.Air);
            world.SetBlock(16, y, 20, BlockType.Air);
        }

        player.CreativeMode = false;
        player.Position = new Vector3(16.5f, targetY + 1.2f, 16.5f);
        player.Velocity = Vector3.Zero;

        world.SetBlock(16, targetY, 22, BlockType.Stone);

        player.Hotbar[0] = ToolRegistry.CreateStack(ItemId.WoodSword);
        player.SelectedSlot = 0;

        var sheep = game.Animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, targetY + 1f, 20.2f), world);
        if (sheep == null)
        {
            throw new Exception("Failed to spawn sheep for sword miss test.");
        }

        IntegrationTestHelpers.AimAt(player, sheep.Position + new Vector3(0f, sheep.Stats.Height * 0.5f, 0f));
        IntegrationTestHelpers.SyncCamera(game, player);

        int durabilityBefore = player.Hotbar[0].Durability;
        float sheepHealthBefore = sheep.Health;
        game.SimulateClick(MouseButton.Left);

        if (world.GetBlock(16, targetY, 22) != BlockType.Stone)
        {
            throw new Exception("Sword miss mined the block behind the animal.");
        }

        if (MathF.Abs(sheep.Health - sheepHealthBefore) > 0.001f)
        {
            throw new Exception($"Expected out-of-range sheep to take no damage. Health: {sheepHealthBefore} -> {sheep.Health}");
        }

        if (player.Hotbar[0].Durability != durabilityBefore)
        {
            throw new Exception($"Sword miss should not spend durability. Durability: {durabilityBefore} -> {player.Hotbar[0].Durability}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunLeafDecay(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Leaf Decay Test... ");

        // Set up a simple vertical log with adjacent leaves
        int x = 40;
        int y = 40;
        int z = 40;

        world.SetBlock(x, y, z, BlockType.OakLog);
        world.SetBlock(x, y + 1, z, BlockType.OakLeaves);
        world.SetBlock(x + 1, y + 1, z, BlockType.OakLeaves);

        // Verify they are placed
        if (world.GetBlock(x, y, z) != BlockType.OakLog ||
            world.GetBlock(x, y + 1, z) != BlockType.OakLeaves ||
            world.GetBlock(x + 1, y + 1, z) != BlockType.OakLeaves)
        {
            throw new Exception("Failed to set up blocks for leaf decay test.");
        }

        // Break the log
        world.SetBlock(x, y, z, BlockType.Air);

        // Verify log is Air
        if (world.GetBlock(x, y, z) != BlockType.Air)
        {
            throw new Exception("Log block did not break.");
        }

        // Verify leaves decayed
        if (world.GetBlock(x, y + 1, z) != BlockType.Air ||
            world.GetBlock(x + 1, y + 1, z) != BlockType.Air)
        {
            throw new Exception("Leaves did not decay after log was broken.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunSaplingGrowth(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Sapling Growth Test... ");

        int x = 50;
        int y = 40;
        int z = 50;

        // Place grass underneath to be valid surface
        world.SetBlock(x, y - 1, z, BlockType.Grass);
        world.SetBlock(x, y, z, BlockType.OakSapling);

        // Verify sapling is placed
        if (world.GetBlock(x, y, z) != BlockType.OakSapling)
        {
            throw new Exception("Sapling block was not placed.");
        }

        // Trigger updates manually to simulate time passage
        // We will call UpdateSaplings with 35 seconds of delta time
        game.Session.UpdateSaplings(35f);

        // Verify that the sapling has grown into an OakLog
        BlockType grownBlock = world.GetBlock(x, y, z);
        if (grownBlock != BlockType.OakLog)
        {
            throw new Exception($"Expected sapling to grow into an OakLog, but found {grownBlock}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
}
