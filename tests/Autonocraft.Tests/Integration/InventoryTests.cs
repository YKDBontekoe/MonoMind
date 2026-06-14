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
}
