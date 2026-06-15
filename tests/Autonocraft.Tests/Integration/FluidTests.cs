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

public static class FluidTests
{
    public static void RunSwimThroughWater(Player player, VoxelWorld world)
    {
        Console.Write("Running Swim Through Water Test... ");

        const int x = 112;
        const int z = 112;
        const int y = 50;

        world.UpdateChunksAround(null, new Vector3(x + 0.5f, y, z + 0.5f), 1);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 2; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    world.SetBlock(x + dx, y + dy, z + dz, BlockType.Air);
                }
            }
        }

        for (int dy = 0; dy <= 2; dy++)
        {
            world.SetBlock(x, y + dy, z, BlockType.Water);
        }

        world.SetBlock(x, y - 1, z, BlockType.Stone);

        player.Position = new Vector3(x + 0.5f, y + 0.1f, z + 0.5f);
        player.Velocity = Vector3.Zero;
        player.CreativeMode = false;

        float startY = player.Position.Y;
        float dt = 0.016f;
        for (int i = 0; i < 60; i++)
        {
            player.Update(dt, world, Vector3.Zero, swimUp: true);
        }

        if (player.Position.Y <= startY + 0.05f)
        {
            throw new Exception($"Player should swim upward through water; Y stayed at {player.Position.Y}.");
        }

        if (!player.InWater)
        {
            throw new Exception("Player should be marked InWater while inside water column.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunDrowning(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Drowning Test... ");

        const int x = 128;
        const int z = 128;
        const int y = 52;

        world.UpdateChunksAround(null, new Vector3(x + 0.5f, y, z + 0.5f), 1);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 3; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    world.SetBlock(x + dx, y + dy, z + dz, BlockType.Air);
                }
            }
        }

        for (int dy = 0; dy <= 3; dy++)
        {
            world.SetBlock(x, y + dy, z, BlockType.Water);
        }

        world.SetBlock(x, y - 1, z, BlockType.Stone);

        player.Health = 20f;
        player.CreativeMode = false;
        player.Position = new Vector3(x + 0.5f, y + 0.2f, z + 0.5f);
        player.Velocity = Vector3.Zero;
        player.ClearInvulnerability();

        float dt = 0.016f;
        for (int i = 0; i < (int)(Player.MaxOxygen / dt) + 120; i++)
        {
            player.Update(dt, world, Vector3.Zero);
            game.Combat.Update(
                dt,
                world,
                player,
                game.Animals,
                game.BlockInteraction,
                game.Particles,
                game.InteractionAnimator,
                game.Camera.Position,
                game.Camera.Front,
                leftHeld: false,
                leftPressed: false);
        }

        if (player.Oxygen > 0f)
        {
            throw new Exception($"Expected oxygen to deplete underwater, got {player.Oxygen}.");
        }

        if (player.Health >= 20f)
        {
            throw new Exception("Expected drowning damage after oxygen depleted.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunFallDamageInWater(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Fall Damage In Water Test... ");

        const int targetY = 34;
        const int x = 24;
        const int z = 24;

        world.UpdateChunksAround(null, new Vector3(x + 0.5f, targetY + 6f, z + 0.5f), 1);

        for (int y = targetY - 1; y <= targetY + 12; y++)
        {
            world.SetBlock(x, y, z, BlockType.Air);
        }

        for (int y = targetY; y <= targetY + 4; y++)
        {
            world.SetBlock(x, y, z, BlockType.Water);
        }

        player.CreativeMode = false;
        player.Health = 20f;
        player.Velocity = Vector3.Zero;
        player.Position = new Vector3(x + 0.5f, targetY + 11f, z + 0.5f);
        player.ResetFallTracking();

        float dt = 0.016f;
        bool landed = false;
        for (int i = 0; i < 600; i++)
        {
            player.Update(dt, world, Vector3.Zero);
            if (player.JustLanded)
            {
                landed = true;
                break;
            }
        }

        if (!landed)
        {
            throw new Exception("Player did not land during water fall damage test.");
        }

        float healthBefore = player.Health;
        player.ClearInvulnerability();
        game.Combat.Update(
            dt,
            world,
            player,
            game.Animals,
            game.BlockInteraction,
            game.Particles,
            game.InteractionAnimator,
            game.Camera.Position,
            game.Camera.Front,
            leftHeld: false,
            leftPressed: false);

        float damageTaken = healthBefore - player.Health;
        if (damageTaken > 2.1f)
        {
            throw new Exception($"Expected at most 2 HP fall damage in water, took {damageTaken}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunFluidSpread(AutonocraftGame game, VoxelWorld world)
    {
        Console.Write("Running Fluid Spread Test... ");

        const int x = 144;
        const int z = 144;
        const int y = 40;

        world.UpdateChunksAround(null, new Vector3(x + 0.5f, y, z + 0.5f), 1);

        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dz = -2; dz <= 2; dz++)
            {
                for (int dy = 0; dy <= 4; dy++)
                {
                    world.SetBlock(x + dx, y + dy, z + dz, BlockType.Air);
                }
            }
        }

        world.SetBlock(x, y - 1, z, BlockType.Stone);
        world.Fluids.PlaceSource(world, x, y + 2, z, null);

        float dt = 0.25f;
        for (int i = 0; i < 40; i++)
        {
            world.Fluids.Update(world, dt, null, maxUpdatesPerTick: 64);
        }

        if (world.GetBlock(x, y, z) != BlockType.Water)
        {
            throw new Exception("Expected placed water source to spread downward.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunBucketPlaceAndPickup(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Bucket Place And Pickup Test... ");

        const int x = 160;
        const int z = 160;
        const int y = 44;

        world.UpdateChunksAround(null, new Vector3(x + 0.5f, y, z + 0.5f), 1);
        world.SetBlock(x, y, z, BlockType.Air);
        world.SetBlock(x, y - 1, z, BlockType.Stone);

        player.Hotbar[0] = ItemStack.CreateFluidContainer(ItemId.WaterBucket);
        player.SelectedSlot = 0;
        world.Fluids.PlaceSource(world, x, y, z, null);

        if (world.GetBlock(x, y, z) != BlockType.Water)
        {
            throw new Exception("Bucket place failed to create water block.");
        }

        if (!world.Fluids.TryPickup(world, x, y, z, null))
        {
            throw new Exception("Bucket pickup failed to remove water block.");
        }

        if (world.GetBlock(x, y, z) != BlockType.Air)
        {
            throw new Exception("Bucket pickup left water behind.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunFluidSaveRoundTrip(AutonocraftGame game, VoxelWorld world)
    {
        Console.Write("Running Fluid Save Round-Trip Test... ");

        world.Fluids.Clear();
        world.Fluids.RegisterSource(48, 50, 48);

        var exported = world.Fluids.ExportModifications();
        var loaded = new FluidSystem();
        loaded.ApplySaveData(exported);

        var roundTrip = loaded.ExportModifications();
        if (roundTrip.Count != 1 || !roundTrip[0].IsSource || roundTrip[0].Level != FluidSystem.MaxLevel)
        {
            throw new Exception("Fluid metadata did not round-trip correctly.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunNoWalkOnWater(Player player, VoxelWorld world)
    {
        Console.Write("Running No Walk On Water Test... ");

        const int baseX = 176;
        const int z = 176;
        const int baseY = 48;

        world.UpdateChunksAround(null, new Vector3(baseX + 0.5f, baseY, z + 0.5f), 1);

        for (int dx = 0; dx <= 6; dx++)
        {
            for (int dy = 0; dy <= 4; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    world.SetBlock(baseX + dx, baseY + dy, z + dz, BlockType.Air);
                }
            }
        }

        for (int dx = 0; dx <= 5; dx++)
        {
            for (int dy = 0; dy <= dx; dy++)
            {
                world.SetBlock(baseX + dx, baseY + dy, z, BlockType.Water);
            }
        }

        player.Position = new Vector3(baseX + 0.5f, baseY + 0.2f, z + 0.5f);
        player.Velocity = Vector3.Zero;
        player.CreativeMode = false;

        float startY = player.Position.Y;
        float dt = 0.016f;
        for (int i = 0; i < 200; i++)
        {
            player.Update(dt, world, new Vector3(1f, 0f, 0f));
        }

        float climb = player.Position.Y - startY;
        if (climb > 1.0f)
        {
            throw new Exception($"Player walked up water staircase by {climb:F2} blocks (expected <= 1.0).");
        }

        int footX = (int)MathF.Floor(player.Position.X);
        int footY = (int)MathF.Floor(player.Position.Y);
        int footZ = (int)MathF.Floor(player.Position.Z);
        if (player.IsGrounded
            && world.GetBlock(footX, footY, footZ).IsWater()
            && !world.GetBlock(footX, footY - 1, footZ).IsCollidable())
        {
            throw new Exception("Player became grounded on water without solid support.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunWaterFillsExcavatedGap(VoxelWorld world)
    {
        Console.Write("Running Water Fills Excavated Gap Test... ");

        const int x = 192;
        const int z = 192;
        const int y = 42;

        world.UpdateChunksAround(null, new Vector3(x + 0.5f, y, z + 0.5f), 1);

        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dz = -2; dz <= 2; dz++)
            {
                for (int dy = 0; dy <= 3; dy++)
                {
                    world.SetBlock(x + dx, y + dy, z + dz, BlockType.Air);
                }
            }
        }

        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dz = -2; dz <= 2; dz++)
            {
                world.SetBlock(x + dx, y - 1, z + dz, BlockType.Stone);
            }
        }

        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dz = -2; dz <= 2; dz++)
            {
                world.SetBlock(x + dx, y, z + dz, BlockType.Water);
            }
        }

        world.SetBlock(x, y, z, BlockType.Air);

        float dt = 0.12f;
        for (int i = 0; i < 40; i++)
        {
            world.Fluids.Update(world, dt, null, maxUpdatesPerTick: 128);
        }

        if (world.GetBlock(x, y, z) != BlockType.Water)
        {
            throw new Exception("Adjacent water did not fill excavated gap.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
}
