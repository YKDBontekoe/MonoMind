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

public static class SaveTests
{
public static void RunWorldSaveRoundTrip(AutonocraftGame game, Player player, VoxelWorld world)
{
    Console.Write("Running World Save/Load Round-Trip Test... ");

    const string slotId = "test-world";
    const string slotName = "Test World";

    player.Position = new Vector3(20.5f, 40f, 18.5f);
    player.Velocity = new Vector3(1f, 0f, -2f);
    player.Yaw = 45f;
    player.Pitch = -10f;
    player.FlyingMode = true;
    player.SelectedSlot = 2;
    player.Hotbar[2] = ItemStack.CreateBlock(BlockType.Stone, 12);
    var savedTool = ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood);
    savedTool.Durability = 37;
    player.Hotbar[4] = savedTool;
    player.Skills.Mining.Level = 3;
    player.Skills.Mining.Xp = 12f;

    world.SetBlock(20, 35, 18, BlockType.Air);
    world.SetBlock(21, 35, 18, BlockType.Dirt);
    world.SetBlock(22, 36, 19, BlockType.Stone);

    game.SetTimeOfDay(0.42f);
    game.TimeScale = 0.02f;
    game.TimePaused = true;

    game.Crafting.Journal.Unlock("sigil:bench");
    game.Crafting.Journal.Unlock("recipe:plank");

    var snapshot = game.Session.BuildSaveSnapshot(
        slotId, slotName, game.TimeOfDay, game.TimeScale, game.TimePaused,
        GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
    var saveData = WorldSaveManager.BuildFromSnapshot(snapshot);
    WorldSaveManager.Save(saveData);

    using var loadedWorld = new VoxelWorld(saveData.Seed);
    var loadedSave = WorldSaveManager.Load(slotId);
    loadedWorld.ApplySaveData(loadedSave);

    loadedWorld.UpdateChunksAround(null, new Vector3(loadedSave.Player.PosX, loadedSave.Player.PosY, loadedSave.Player.PosZ), 2);

    var loadedPlayer = new Player(Vector3.Zero);
    WorldSaveManager.ApplyPlayerSaveData(loadedPlayer, loadedSave.Player);

    if (loadedWorld.GetBlock(20, 35, 18) != BlockType.Air)
    {
        throw new Exception("Expected mined block at (20,35,18) to remain Air after load.");
    }

    if (loadedWorld.GetBlock(21, 35, 18) != BlockType.Dirt)
    {
        throw new Exception("Expected placed Dirt at (21,35,18) after load.");
    }

    if (loadedWorld.GetBlock(22, 36, 19) != BlockType.Stone)
    {
        throw new Exception("Expected placed Stone at (22,36,19) after load.");
    }

    if (MathF.Abs(loadedPlayer.Position.X - 20.5f) > 0.001f ||
        MathF.Abs(loadedPlayer.Position.Y - 40f) > 0.001f ||
        MathF.Abs(loadedPlayer.Position.Z - 18.5f) > 0.001f)
    {
        throw new Exception($"Loaded player position mismatch: {loadedPlayer.Position}");
    }

    if (MathF.Abs(loadedPlayer.Velocity.X - 1f) > 0.001f ||
        MathF.Abs(loadedPlayer.Velocity.Z + 2f) > 0.001f)
    {
        throw new Exception($"Loaded player velocity mismatch: {loadedPlayer.Velocity}");
    }

    if (!loadedPlayer.Hotbar[2].IsBlock() || loadedPlayer.Hotbar[2].BlockType != BlockType.Stone || loadedPlayer.Hotbar[2].Count != 12)
    {
        throw new Exception("Loaded hotbar slot 3 did not match saved inventory.");
    }

    if (!loadedPlayer.Hotbar[4].IsTool() || loadedPlayer.Hotbar[4].ToolId != ItemId.WoodPickaxe || loadedPlayer.Hotbar[4].Durability != 37)
    {
        throw new Exception("Loaded tool durability did not match saved inventory.");
    }

    if (loadedPlayer.Skills.Mining.Level != 3 || MathF.Abs(loadedPlayer.Skills.Mining.Xp - 12f) > 0.001f)
    {
        throw new Exception("Loaded mining skill did not match saved values.");
    }

    if (MathF.Abs(loadedSave.Time.TimeOfDay - 0.42f) > 0.001f || !loadedSave.Time.TimePaused)
    {
        throw new Exception("Loaded time state did not match saved values.");
    }

    if (!loadedSave.UnlockedCraftingIds.Contains("sigil:bench") ||
        !loadedSave.UnlockedCraftingIds.Contains("recipe:plank"))
    {
        throw new Exception("Loaded crafting journal did not match saved discoveries.");
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("PASSED");
    Console.ResetColor();
}

public static void RunCorruptSaveSelectedSlotClamped()
{
    Console.Write("Running Corrupt Save SelectedSlot Sanitization Test... ");

    var save = WorldSaveManager.CreateNewWorldSaveData("sanitize-test", "Sanitize", 1337, 16, 16);
    save.Player.SelectedSlot = 99;
    save.Player.Health = 999;
    save.Player.MaxHealth = 20;
    save.Modifications.Add(new BlockModification { X = 16, Y = -1, Z = 16, Block = 255 });
    save.Modifications.Add(new BlockModification { X = 16, Y = 64, Z = 16, Block = (byte)BlockType.Dirt });

    WorldSaveManager.ValidateAndSanitize(save);

    if (save.Player.SelectedSlot != 8)
    {
        throw new Exception($"Expected SelectedSlot clamped to 8, got {save.Player.SelectedSlot}.");
    }

    if (save.Player.Health != 20)
    {
        throw new Exception($"Expected health clamped to max, got {save.Player.Health}.");
    }

    if (save.Modifications.Count != 1 || save.Modifications[0].Block != (byte)BlockType.Dirt)
    {
        throw new Exception("Expected invalid block modifications to be removed during sanitization.");
    }

    var player = new Player(Vector3.Zero);
    WorldSaveManager.ApplyPlayerSaveData(player, save.Player);
    _ = player.Hotbar[player.SelectedSlot];

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("PASSED");
    Console.ResetColor();
}

public static void RunSyncSaveFailureDoesNotThrow()
{
    Console.Write("Running Sync Save Failure Handling Test... ");

    var save = WorldSaveManager.CreateNewWorldSaveData("sync-fail", "Fail", 1337, 16, 16);
    string hostSaves = WorldSaveManager.GetSavesDirectory();
    string blockedPath = Path.Combine(Path.GetTempPath(), "autonocraft-blocked-save-" + Guid.NewGuid().ToString("N"));
    File.WriteAllText(blockedPath, "not a directory");

    try
    {
        WorldSaveManager.SetSavesDirectoryForTests(blockedPath);

        try
        {
            WorldSaveManager.Save(save);
            throw new Exception("Expected save to fail against blocked saves path.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Expected failure mode for invalid save root.
        }
    }
    finally
    {
        WorldSaveManager.SetSavesDirectoryForTests(hostSaves);
        try
        {
            File.Delete(blockedPath);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("PASSED");
    Console.ResetColor();
}

public static void RunLoadFailureForMissingSlot()
{
    Console.Write("Running Missing Save Load Failure Test... ");

    try
    {
        WorldSaveManager.Load("missing-slot-" + Guid.NewGuid().ToString("N"));
        throw new Exception("Expected FileNotFoundException for missing save slot.");
    }
    catch (FileNotFoundException)
    {
        // Expected.
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("PASSED");
    Console.ResetColor();
}
}
