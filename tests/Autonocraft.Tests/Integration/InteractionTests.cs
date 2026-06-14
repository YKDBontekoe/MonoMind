using System;
using System.IO;
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
        game.SimulateClick(MouseButton.Left);

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
            throw new Exception("Player did not collect Stone block in inventory after mining.");
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
}
