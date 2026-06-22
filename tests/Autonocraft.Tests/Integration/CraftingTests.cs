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
        game.Crafting.Crucible.InputSlots[0] = ItemStack.CreateBlock(BlockType.OakLog, 1);

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
        game.Crafting.Crucible.InputSlots[0] = ItemStack.CreateBlock(BlockType.BirchLog, 1);

        var birchResult = game.Crafting.TryTransmute(world, player, 0.5f);
        if (!birchResult.Succeeded || birchResult.Recipe?.Output != BlockType.BirchPlank)
        {
            throw new Exception($"Birch plank transmutation failed: {birchResult.Message}");
        }

        game.Crafting.Crucible.InputSlots[0] = ItemStack.CreateBlock(BlockType.Stone, 1);
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

    public static void RunPlayerCraftGrid(AutonocraftGame game, Player player)
    {
        Console.Write("Running Player 2x2 Craft Grid Test... ");

        for (int i = 0; i < 9; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        game.Crafting.PlayerCraftGrid.SetSize(CraftGridSize.TwoByTwo);
        game.Crafting.PlayerCraftGrid.Clear();
        game.Crafting.PlayerCraftGrid.SetSlot(0, ItemStack.CreateBlock(BlockType.OakLog, 1));

        var preview = game.Crafting.GetPlayerCraftPreview();
        if (!preview.HasMatch || preview.Result.BlockType != BlockType.OakPlank)
        {
            throw new Exception("Expected oak log in 2x2 grid to preview planks.");
        }

        var craftResult = game.Crafting.TryPlayerCraft(player);
        if (!craftResult.Succeeded || craftResult.Recipe?.Output != BlockType.OakPlank)
        {
            throw new Exception($"Player craft failed: {craftResult.Message}");
        }

        int plankCount = 0;
        for (int i = 0; i < 9; i++)
        {
            if (player.Hotbar[i].IsBlock() && player.Hotbar[i].BlockType == BlockType.OakPlank)
            {
                plankCount += player.Hotbar[i].Count;
            }
        }

        for (int i = 0; i < Player.StorageSlotCount; i++)
        {
            var slot = player.Storage.GetSlot(i);
            if (slot.IsBlock() && slot.BlockType == BlockType.OakPlank)
            {
                plankCount += slot.Count;
            }
        }

        if (plankCount < 2)
        {
            throw new Exception($"Expected at least 2 planks after player craft, got {plankCount}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunShapedToolBenchCraft(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Shaped Tool Bench Craft Test... ");

        for (int i = 0; i < 9; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        game.Crafting.OpenCrucible(40, 40, 40, BlockType.StationBench);
        game.Crafting.Crucible.InputSlots[0] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        game.Crafting.Crucible.InputSlots[1] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        game.Crafting.Crucible.InputSlots[2] = ItemStack.CreateBlock(BlockType.OakPlank, 1);
        game.Crafting.Crucible.InputSlots[4] = ItemStack.CreateMaterial(ItemId.Stick, 1);
        game.Crafting.Crucible.InputSlots[7] = ItemStack.CreateMaterial(ItemId.Stick, 1);

        var preview = game.Crafting.Crucible.GetPreview(game.Crafting.Journal, game.Crafting.GetCurrentEnvironment(world, 0.5f));
        if (!preview.HasMatch || !preview.Result.IsTool() || preview.Result.ToolId != ItemId.WoodPickaxe)
        {
            throw new Exception("Expected wood pickaxe preview from shaped bench grid.");
        }

        var craftResult = game.Crafting.TryTransmute(world, player, 0.5f);
        if (!craftResult.Succeeded || craftResult.Recipe?.OutputItem != ItemId.WoodPickaxe)
        {
            throw new Exception($"Shaped tool craft failed: {craftResult.Message}");
        }

        bool hasPickaxe = false;
        for (int i = 0; i < 9; i++)
        {
            if (player.Hotbar[i].IsTool() && player.Hotbar[i].ToolId == ItemId.WoodPickaxe)
            {
                hasPickaxe = true;
                break;
            }
        }

        if (!hasPickaxe)
        {
            for (int i = 0; i < Player.StorageSlotCount; i++)
            {
                var slot = player.Storage.GetSlot(i);
                if (slot.IsTool() && slot.ToolId == ItemId.WoodPickaxe)
                {
                    hasPickaxe = true;
                    break;
                }
            }
        }

        if (!hasPickaxe)
        {
            throw new Exception("Player did not receive wood pickaxe from shaped bench craft.");
        }

        game.Crafting.CloseCrucible();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunStorageInventory(Player player)
    {
        Console.Write("Running Storage Inventory Test... ");

        for (int i = 0; i < 9; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        for (int i = 0; i < Player.StorageSlotCount; i++)
        {
            player.Storage.SetSlot(i, ItemStack.Empty);
        }

        for (int i = 0; i < 9; i++)
        {
            player.Hotbar[i] = ItemStack.CreateBlock(BlockType.Dirt, 1);
        }

        player.AddToInventory(BlockType.Stone);

        bool storedInBackpack = false;
        for (int i = 0; i < Player.StorageSlotCount; i++)
        {
            var slot = player.Storage.GetSlot(i);
            if (slot.IsBlock() && slot.BlockType == BlockType.Stone)
            {
                storedInBackpack = true;
                break;
            }
        }

        if (!storedInBackpack)
        {
            throw new Exception("Expected overflow blocks to land in storage inventory.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunSticksCrafting(AutonocraftGame game, Player player)
    {
        Console.Write("Running Sticks Crafting Test... ");

        game.Crafting.PlayerCraftGrid.SetSize(CraftGridSize.TwoByTwo);
        game.Crafting.PlayerCraftGrid.Clear();
        game.Crafting.PlayerCraftGrid.SetSlot(0, ItemStack.CreateBlock(BlockType.OakPlank, 1));
        game.Crafting.PlayerCraftGrid.SetSlot(2, ItemStack.CreateBlock(BlockType.OakPlank, 1));

        var preview = game.Crafting.GetPlayerCraftPreview();
        if (!preview.HasMatch || !preview.Result.IsMaterial() || preview.Result.MaterialId != ItemId.Stick)
        {
            throw new Exception("Expected stick preview from plank pattern.");
        }

        var craftResult = game.Crafting.TryPlayerCraft(player);
        if (!craftResult.Succeeded || craftResult.Recipe?.Id != "recipe:sticks")
        {
            throw new Exception($"Stick craft failed: {craftResult.Message}");
        }

        if (!game.Crafting.Journal.IsUnlocked("recipe:sticks"))
        {
            throw new Exception("Stick recipe was not unlocked after crafting.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunRecipeUnlockOnDiscovery(AutonocraftGame game, Player player)
    {
        Console.Write("Running Recipe Unlock On Discovery Test... ");

        var journal = new DiscoveryJournal();
        RecipeDiscovery.OnItemAcquired(journal, ItemStack.CreateBlock(BlockType.OakPlank, 1));

        if (!journal.IsUnlocked("recipe:sticks"))
        {
            throw new Exception("Acquiring planks should unlock stick recipe.");
        }

        journal = new DiscoveryJournal();
        RecipeDiscovery.OnItemAcquired(journal, ItemStack.CreateMaterial(ItemId.Stick, 1));

        if (!journal.IsUnlocked("recipe:sticks"))
        {
            throw new Exception("Acquiring sticks should unlock stick recipe.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunRecipeBookToolResolve(Player player)
    {
        Console.Write("Running Recipe Book Tool Resolve Test... ");

        var journal = new DiscoveryJournal();
        journal.Unlock("recipe:wood_pickaxe");

        for (int i = 0; i < player.Hotbar.Length; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        player.Hotbar[0] = ItemStack.CreateBlock(BlockType.OakPlank, 4);
        player.Hotbar[1] = ItemStack.CreateMaterial(ItemId.Stick, 2);

        var recipe = CraftRecipeRegistry.All.Single(r => r.Id == "recipe:wood_pickaxe");
        var inventory = new PlayerInventoryAdapter(player);
        var grid = new CraftingGrid();
        grid.SetSize(CraftGridSize.ThreeByThree);

        if (!RecipeBookResolver.CanCraftWithInventory(recipe, CraftGridSize.ThreeByThree, inventory))
        {
            throw new Exception("Recipe book should detect craftable wood pickaxe ingredients.");
        }

        if (!RecipeBookResolver.TryFillGrid(recipe, grid, inventory))
        {
            throw new Exception("Recipe book failed to fill bench grid for wood pickaxe.");
        }

        var match = GridCrafting.FindMatch(grid, BlockType.StationBench, journal);
        if (match?.Id != "recipe:wood_pickaxe")
        {
            throw new Exception("Filled grid did not resolve to wood pickaxe recipe.");
        }

        var visible = RecipeBookResolver.GetVisibleRecipes(BlockType.StationBench, CraftGridSize.ThreeByThree, new DiscoveryJournal());
        if (!visible.Any(r => r.Id == "recipe:stone_pickaxe"))
        {
            throw new Exception("Recipe book should show locked stone tools as progression guidance.");
        }

        for (int i = 0; i < player.Hotbar.Length; i++)
        {
            player.Hotbar[i] = ItemStack.Empty;
        }

        for (int i = 0; i < Player.StorageSlotCount; i++)
        {
            player.Storage.SetSlot(i, ItemStack.Empty);
        }

        player.Hotbar[0] = ItemStack.CreateBlock(BlockType.Cobblestone, 4);
        player.Hotbar[1] = ItemStack.CreateBlock(BlockType.OakLog, 2);

        var smokerRecipe = CraftRecipeRegistry.All.Single(r => r.Id == "recipe:station_smoker");
        inventory = new PlayerInventoryAdapter(player);
        grid.Clear();

        if (!RecipeBookResolver.CanCraftWithInventory(smokerRecipe, CraftGridSize.ThreeByThree, inventory))
        {
            throw new Exception("Recipe book should count tag-based wood ingredients.");
        }

        if (!RecipeBookResolver.TryFillGrid(smokerRecipe, grid, inventory))
        {
            throw new Exception("Recipe book failed to fill a shapeless recipe with tag-based wood ingredients.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
}
