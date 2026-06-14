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

public static class CraftingTests
{
    public static void RunSigilBenchActivation(AutonocraftGame game, VoxelWorld world)
    {
        Console.Write("Running Sigil Bench Activation Test... ");

        const int cx = 32;
        const int cy = 40;
        const int cz = 32;

        for (int dx = -1; dx <= 1; dx++)
        {
            world.SetBlock(cx + dx, cy, cz, BlockType.OakLog);
            world.SetBlock(cx + dx, cy - 1, cz, BlockType.Stone);
        }

        var result = game.Crafting.TryActivateSigil(world, cx, cy, cz, null!);
        if (!result.Success || result.Pattern?.OutputStation != BlockType.StationBench)
        {
            throw new Exception("Bench sigil activation failed.");
        }

        if (world.GetBlock(cx, cy, cz) != BlockType.StationBench)
        {
            throw new Exception("Center block was not converted to StationBench.");
        }

        if (world.GetBlock(cx - 1, cy, cz) != BlockType.Air || world.GetBlock(cx + 1, cy - 1, cz) != BlockType.Air)
        {
            throw new Exception("Sigil peripheral blocks were not consumed.");
        }

        if (!game.Crafting.Journal.IsUnlocked("sigil:bench"))
        {
            throw new Exception("Bench sigil was not added to discovery journal.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunCruciblePlankRecipe(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Crucible Plank Recipe Test... ");

        for (int i = 0; i < 9; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        game.Crafting.OpenCrucible(40, 40, 40, BlockType.StationBench);
        game.Crafting.Crucible.InputSlots[0] = BlockType.OakLog;

        var craftResult = game.Crafting.TryTransmute(world, player, 0.5f);
        if (!craftResult.Succeeded || craftResult.Recipe?.Output != BlockType.OakPlank)
        {
            throw new Exception($"Plank transmutation failed: {craftResult.Message}");
        }

        bool hasPlanks = false;
        for (int i = 0; i < 9; i++)
        {
            if (player.Hotbar[i].IsBlock() && player.Hotbar[i].BlockType == BlockType.OakPlank && player.Hotbar[i].Count >= 2)
            {
                hasPlanks = true;
                break;
            }
        }

        if (!hasPlanks)
        {
            throw new Exception("Player did not receive OakPlank output from crucible.");
        }

        if (!game.Crafting.Journal.IsUnlocked("recipe:plank"))
        {
            throw new Exception("Plank recipe was not unlocked in journal.");
        }

        game.Crafting.CloseCrucible();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunNewCraftRecipes(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running New Craft Recipes Test... ");

        for (int i = 0; i < 9; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        game.Crafting.OpenCrucible(40, 40, 40, BlockType.StationBench);
        game.Crafting.Crucible.InputSlots[0] = BlockType.BirchLog;

        var birchResult = game.Crafting.TryTransmute(world, player, 0.5f);
        if (!birchResult.Succeeded || birchResult.Recipe?.Output != BlockType.BirchPlank)
        {
            throw new Exception($"Birch plank transmutation failed: {birchResult.Message}");
        }

        game.Crafting.Crucible.InputSlots[0] = BlockType.Stone;
        var cobbleResult = game.Crafting.TryTransmute(world, player, 0.5f);
        if (!cobbleResult.Succeeded || cobbleResult.Recipe?.Output != BlockType.Cobblestone)
        {
            throw new Exception($"Cobblestone transmutation failed: {cobbleResult.Message}");
        }

        game.Crafting.CloseCrucible();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
}
