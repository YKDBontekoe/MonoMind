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

public static class PhysicsTests
{
    public static void RunGravityAndCollision(Player player, VoxelWorld world)
    {
        Console.WriteLine("Debugging blocks at X=16, Z=16:");
        for (int y = 20; y <= 45; y++)
        {
            BlockType bt = world.GetBlock(16, y, 16);
            if (bt != BlockType.Air)
            {
                Console.WriteLine($"  Y={y}: {bt}");
            }
        }

        Console.Write("Running Gravity & Collision Test... ");

        int surfaceY = world.GetHighestSolidY(16, 16);
        if (surfaceY < 0)
        {
            throw new Exception("Expected solid ground at spawn for gravity test.");
        }

        float spawnY = surfaceY + 10f;
        while (!EntityCollision.IsSpaceClearAt(world, new Vector3(16.5f, spawnY, 16.5f), Player.Width, Player.Height) && spawnY < Chunk.Height - 4)
        {
            spawnY += 1f;
        }

        player.Position = new Vector3(16.5f, spawnY, 16.5f);
        player.Velocity = Vector3.Zero;
        player.CreativeMode = false;

        if (player.IsGrounded)
        {
            throw new Exception("Player should not be grounded when spawning high in the air.");
        }

        // Let them fall
        float dt = 0.016f; // ~60fps step
        bool fell = false;

        // Maximum of 3 seconds of simulation (180 frames)
        for (int i = 0; i < 180 && !player.IsGrounded; i++)
        {
            player.Update(dt, world, Vector3.Zero);
            if (player.Velocity.Y < 0) fell = true;
        }

        if (!fell)
        {
            throw new Exception("Player velocity did not accelerate downwards under gravity.");
        }

        if (!player.IsGrounded)
        {
            throw new Exception($"Player did not land on the ground after falling. Final Y: {player.Position.Y}");
        }

        // Verify they are sitting on a solid block
        int feetX = (int)MathF.Floor(player.Position.X);
        int feetY = (int)MathF.Floor(player.Position.Y - 0.05f); // just below feet
        int feetZ = (int)MathF.Floor(player.Position.Z);

        BlockType blockBelow = world.GetBlock(feetX, feetY, feetZ);
        if (blockBelow == BlockType.Air)
        {
            throw new Exception($"Player is grounded but block below is Air at ({feetX}, {feetY}, {feetZ}).");
        }

        // Verify velocity.Y has reset to 0
        if (MathF.Abs(player.Velocity.Y) > 0.001f)
        {
            throw new Exception($"Grounded player should have vertical velocity ~0, but has {player.Velocity.Y}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunJumping(Player player, VoxelWorld world)
    {
        Console.Write("Running Jumping Test... ");

        int surfaceY = world.GetHighestSolidY(16, 16);
        if (surfaceY < 0)
        {
            throw new Exception("Expected solid ground for jumping test.");
        }
        player.Position = new Vector3(16.5f, surfaceY + 1.001f, 16.5f);
        player.Velocity = Vector3.Zero;
        player.CreativeMode = false;

        // Settle player on ground
        for (int i = 0; i < 30; i++)
        {
            player.Update(0.016f, world, Vector3.Zero);
        }

        if (!player.IsGrounded)
        {
            throw new Exception("Player must be grounded to start the jump test.");
        }

        float initialY = player.Position.Y;
        player.Jump();

        if (player.IsGrounded)
        {
            throw new Exception("Player should not be grounded immediately after jumping.");
        }

        if (MathF.Abs(player.Velocity.Y - Player.JumpForce) > 0.001f)
        {
            throw new Exception($"Player jump force velocity should be {Player.JumpForce}, but got {player.Velocity.Y}");
        }

        float dt = 0.016f;
        float groundY = player.Position.Y;
        for (int i = 0; i < 8; i++)
        {
            player.Update(dt, world, Vector3.Zero);
            if (player.IsGrounded)
            {
                throw new Exception($"Player was grounded too early during jump at frame {i}, y={player.Position.Y:F3}.");
            }

            if (player.Position.Y <= groundY + 0.01f)
            {
                throw new Exception($"Player was snapped down during jump at frame {i}, y={player.Position.Y:F3}.");
            }
        }

        // Tick a few times to verify they rise
        player.Update(dt, world, Vector3.Zero);

        if (player.Position.Y <= initialY)
        {
            throw new Exception($"Player height should have increased after jumping. Initial: {initialY}, Current: {player.Position.Y}");
        }

        // Fall back down
        for (int i = 0; i < 180 && !player.IsGrounded; i++)
        {
            player.Update(dt, world, Vector3.Zero);
        }

        if (!player.IsGrounded)
        {
            throw new Exception("Player should have landed back on the ground after jumping.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunSlabStairWalking(Player player, VoxelWorld world)
    {
        Console.Write("Running Slab Stair Walking Test... ");

        const int baseX = 24;
        const int baseY = 80;
        const int baseZ = 24;

        world.UpdateChunksAround(null, new Vector3(baseX + 0.5f, baseY + 1f, baseZ + 0.5f), 3);

        for (int x = baseX - 2; x <= baseX + 6; x++)
        {
            for (int z = baseZ - 2; z <= baseZ + 2; z++)
            {
                for (int y = Math.Max(0, baseY - 4); y <= baseY + 6; y++)
                {
                    world.SetBlock(x, y, z, BlockType.Air);
                }

                world.SetBlock(x, baseY - 1, z, BlockType.Stone);
                world.SetBlock(x, baseY, z, BlockType.Stone);
            }
        }

        world.SetBlock(baseX + 2, baseY + 1, baseZ, BlockType.StoneSlab);
        world.SetBlock(baseX + 3, baseY + 1, baseZ, BlockType.Stone);
        world.SetBlock(baseX + 4, baseY + 1, baseZ, BlockType.Stone);

        player.CreativeMode = false;
        player.Position = new Vector3(baseX + 0.5f, baseY + 1.001f, baseZ + 0.5f);
        player.Velocity = Vector3.Zero;

        float dt = 1f / 60f;
        float lastY = player.Position.Y;
        int groundedFrames = 0;
        int airFrames = 0;
        int oscillations = 0;

        for (int i = 0; i < 360; i++)
        {
            player.Update(dt, world, new Vector3(1f, 0f, 0f));
            if (!player.IsGrounded)
            {
                airFrames++;
                if (airFrames > 15)
                {
                    throw new Exception($"Player left the ground too long while climbing slab stairs at frame {i}, y={player.Position.Y:F3}.");
                }
            }
            else
            {
                airFrames = 0;
                groundedFrames++;
            }
            if (MathF.Abs(player.Position.Y - lastY) > 1.05f)
            {
                throw new Exception($"Player Y jumped too far between frames ({lastY:F3} -> {player.Position.Y:F3}).");
            }

            if (i > 0 && MathF.Abs(player.Position.Y - lastY) > 0.02f && MathF.Abs(player.Position.Y - lastY) < 0.35f)
            {
                oscillations++;
            }

            lastY = player.Position.Y;

            if (player.Position.X >= baseX + 3.5f)
            {
                break;
            }
        }

        if (player.Position.X < baseX + 3.5f)
        {
            throw new Exception($"Player did not walk across the slab stair (x={player.Position.X:F2}).");
        }

        if (player.Position.Y < baseY + 1.9f)
        {
            throw new Exception($"Player did not reach the top of the slab stair (y={player.Position.Y:F3}).");
        }

        if (oscillations > 24)
        {
            throw new Exception($"Player Y oscillated too often on slab stairs ({oscillations} frames).");
        }

        if (groundedFrames < 30)
        {
            throw new Exception("Player was not grounded for most of the slab stair walk.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunFallDamage(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Fall Damage Test... ");

        const int targetY = 30;
        for (int y = targetY + 1; y <= targetY + 12; y++)
        {
            world.SetBlock(16, y, 16, BlockType.Air);
        }

        world.SetBlock(16, targetY, 16, BlockType.Stone);

        player.CreativeMode = false;
        player.Health = 20f;
        player.Velocity = Vector3.Zero;
        player.Position = new Vector3(16.5f, targetY + 1.2f, 16.5f);

        float dt = 0.016f;
        for (int i = 0; i < 30; i++)
        {
            player.Update(dt, world, Vector3.Zero);
        }

        player.ResetFallTracking();
        player.Position = new Vector3(16.5f, targetY + 11f, 16.5f);
        player.Velocity = Vector3.Zero;
        IntegrationTestHelpers.SyncCamera(game, player);

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
            throw new Exception("Player did not land during fall damage test.");
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

        if (player.Health >= healthBefore)
        {
            throw new Exception($"Expected fall damage. Health stayed at {player.Health}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunPassableBlocks(Player player, VoxelWorld world)
    {
        Console.Write("Running Passable Blocks Test... ");

        const int x = 96;
        const int z = 96;
        const int y = 48;

        world.UpdateChunksAround(null, new Vector3(x + 0.5f, y, z + 0.5f), 1);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 2; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0 && dy < 2)
                    {
                        world.SetBlock(x + dx, y + dy, z + dz, BlockType.Air);
                        continue;
                    }

                    world.SetBlock(x + dx, y + dy, z + dz, BlockType.OakLeaves);
                }
            }
        }

        world.SetBlock(x, y - 1, z, BlockType.Stone);

        player.Position = new Vector3(x + 0.5f, y, z + 0.5f);
        player.Velocity = Vector3.Zero;
        player.CreativeMode = false;

        float startX = player.Position.X;
        float dt = 0.016f;
        for (int i = 0; i < 30; i++)
        {
            player.Update(dt, world, new Vector3(6f, 0f, 0f));
        }

        if (player.Position.X <= startX + 0.1f)
        {
            throw new Exception($"Player should walk through leaves; X moved from {startX} to {player.Position.X}.");
        }

        if (!BlockType.OakLeaves.IsPassable())
        {
            throw new Exception("OakLeaves should be marked passable.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
}
