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

public static class InventoryTests
{
    public static void RunInventory(Player player)
    {
        Console.Write("Running Inventory Collection Test... ");

        // Clear inventory
        for (int i = 0; i < 9; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        player.AddToInventory(BlockType.Grass);
        if (!player.Hotbar[0].IsBlock() || player.Hotbar[0].BlockType != BlockType.Grass || player.Hotbar[0].Count != 1)
        {
            throw new Exception($"Expected slot 1 to contain Grass x1, got {player.Hotbar[0].GetDisplayName()} x{player.Hotbar[0].Count}");
        }

        player.AddToInventory(BlockType.Grass);
        if (player.Hotbar[0].Count != 2)
        {
            throw new Exception($"Expected slot 1 to stack to Grass x2, got count {player.Hotbar[0].Count}");
        }

        player.AddToInventory(BlockType.Stone);
        if (!player.Hotbar[1].IsBlock() || player.Hotbar[1].BlockType != BlockType.Stone || player.Hotbar[1].Count != 1)
        {
            throw new Exception($"Expected slot 2 to contain Stone x1, got {player.Hotbar[1].GetDisplayName()} x{player.Hotbar[1].Count}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunToolMiningSpeed(Player player)
    {
        Console.Write("Running Tool Mining Speed Test... ");

        float bareHands = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, ItemStack.Empty, player.Skills);
        var pickaxe = ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood);
        float withPickaxe = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, pickaxe, player.Skills);

        if (withPickaxe >= bareHands)
        {
            throw new Exception($"Expected wood pickaxe to mine stone faster than bare hands ({withPickaxe} >= {bareHands}).");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunToolDurability(Player player)
    {
        Console.Write("Running Tool Durability Test... ");

        player.CreativeMode = false;
        player.Hotbar[0] = ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood);
        player.Hotbar[0].Durability = 1;
        player.SelectedSlot = 0;

        bool broke = player.DamageSelectedTool(1);
        if (!broke || !player.Hotbar[0].IsEmpty)
        {
            throw new Exception("Tool with 1 durability should break and clear the slot.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunSkillProgression(Player player)
    {
        Console.Write("Running Skill Progression Test... ");

        player.Skills.Mining = SkillProgress.Default;
        float slowTime = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood), player.Skills);

        player.Skills.AddXp(PlayerSkill.Mining, 500f);
        float fastTime = MiningCalculator.GetEffectiveBreakTime(BlockType.Stone, ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood), player.Skills);

        if (player.Skills.Mining.Level <= 1)
        {
            throw new Exception("Mining skill should level up after gaining XP.");
        }

        if (fastTime >= slowTime)
        {
            throw new Exception("Higher mining level should reduce break time.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunDropItem(Player player, GameSession session)
    {
        Console.Write("Running Item Drop Test... ");

        player.SelectedSlot = 0;

        // Set up player hotbar slot
        player.Hotbar[player.SelectedSlot] = ItemStack.CreateBlock(BlockType.Grass, 5);

        // Verify initial count
        if (player.GetSelectedStack().Count != 5)
        {
            throw new Exception("Expected 5 Grass in selected slot.");
        }

        // Drop 1 item
        ItemStack dropped = player.DropOneFromSelectedSlot();
        if (dropped.IsEmpty || dropped.BlockType != BlockType.Grass || dropped.Count != 1)
        {
            throw new Exception("Expected dropped stack to contain 1 Grass.");
        }

        // Verify remaining count in hotbar slot
        if (player.GetSelectedStack().Count != 4)
        {
            throw new Exception("Expected 4 Grass remaining in selected slot.");
        }

        // Drop remaining items one by one
        for (int i = 0; i < 4; i++)
        {
            player.DropOneFromSelectedSlot();
        }

        // Verify slot is now empty
        if (!player.GetSelectedStack().IsEmpty)
        {
            throw new Exception("Expected selected slot to be empty after dropping all items.");
        }

        // Verify we can spawn item drop
        var dropPos = new Vector3(16.5f, 65f, 16.5f);
        var entity = session.SpawnItemDrop(dropped, dropPos);
        if (entity == null || entity.Item.BlockType != BlockType.Grass || entity.Item.Count != 1)
        {
            throw new Exception("Expected spawned ItemEntity to contain the dropped item.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
}
