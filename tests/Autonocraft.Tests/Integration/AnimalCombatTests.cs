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

public static class AnimalCombatTests
{
    public static void RunAnimalGravity(VoxelWorld world)
    {
        Console.Write("Running Animal Gravity Test... ");

        world.UpdateChunksAround(null, new Vector3(16.5f, 64f, 16.5f), 4);

        var animals = new AnimalManager(world.Seed);
        int surfaceY = world.GetHighestSolidY(16, 16);
        if (surfaceY < 0)
        {
            throw new Exception("Could not find surface for animal gravity test.");
        }

        var animal = animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, surfaceY + 10f, 16.5f), world);
        if (animal == null)
        {
            throw new Exception("Failed to spawn test sheep.");
        }

        float dt = 0.016f;
        for (int i = 0; i < 240 && !animal.IsGrounded; i++)
        {
            animals.Update(dt, world);
        }

        if (!animal.IsGrounded)
        {
            throw new Exception($"Animal did not land after falling. Final Y: {animal.Position.Y}");
        }

        if (!animal.IsGrounded || animal.Position.Y < surfaceY + 0.5f)
        {
            throw new Exception($"Animal did not land on solid ground. Grounded={animal.IsGrounded}, Y={animal.Position.Y}, surface={surfaceY}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunAnimalWanderCollision(VoxelWorld world)
    {
        Console.Write("Running Animal Wander Collision Test... ");

        var animals = new AnimalManager(world.Seed);
        int surfaceY = world.GetHighestSolidY(16, 16);
        if (surfaceY < 0)
        {
            throw new Exception("Could not find surface for animal collision test.");
        }

        for (int y = surfaceY + 1; y <= surfaceY + 5; y++)
        {
            world.SetBlock(16, y, 16, BlockType.Air);
        }

        for (int y = surfaceY + 1; y <= surfaceY + 3; y++)
        {
            world.SetBlock(17, y, 16, BlockType.Stone);
        }

        var animal = animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, surfaceY + 4f, 16.5f), world);
        if (animal == null)
        {
            throw new Exception("Failed to spawn test sheep for collision.");
        }

        animal.WanderDirection = Vector3.Normalize(new Vector3(1f, 0f, 0f));
        animal.WanderDistanceRemaining = 10f;

        float dt = 0.016f;
        for (int i = 0; i < 180; i++)
        {
            animals.Update(dt, world);
        }

        if (animal.Position.X >= 17.1f)
        {
            throw new Exception($"Animal walked through wall. Final X: {animal.Position.X}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunAnimalSpawnCap(VoxelWorld world)
    {
        Console.Write("Running Animal Spawn Cap Test... ");

        var animals = new AnimalManager(world.Seed);
        VoxelWorld.GetChunkCoords(16, 16, out int cx, out int cz, out _, out _);

        animals.TryPopulateChunk(cx, cz, world);
        int countAfterFirst = animals.Count;
        animals.TryPopulateChunk(cx, cz, world);
        int countAfterSecond = animals.Count;

        if (countAfterSecond != countAfterFirst)
        {
            throw new Exception($"Chunk was populated twice: {countAfterFirst} -> {countAfterSecond}");
        }

        if (countAfterFirst > AnimalManager.MaxAnimalsPerChunk)
        {
            throw new Exception($"Chunk spawned too many animals: {countAfterFirst}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunPlayerTakeDamage(Player player)
    {
        Console.Write("Running Player Take Damage Test... ");

        player.Health = 20f;
        player.MaxHealth = 20f;

        if (!player.TakeDamage(5f))
        {
            throw new Exception("Expected first TakeDamage call to succeed.");
        }

        if (MathF.Abs(player.Health - 15f) > 0.001f)
        {
            throw new Exception($"Expected health 15 after damage, got {player.Health}.");
        }

        if (player.TakeDamage(5f))
        {
            throw new Exception("Expected i-frames to block immediate second damage.");
        }

        player.UpdateInvulnerability(Player.InvulnerabilityDuration + 0.1f);

        if (!player.TakeDamage(5f))
        {
            throw new Exception("Expected damage after invulnerability expired.");
        }

        if (MathF.Abs(player.Health - 10f) > 0.001f)
        {
            throw new Exception($"Expected health 10 after second damage, got {player.Health}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunEntityRaycast(VoxelWorld world)
    {
        Console.Write("Running Entity Raycast Test... ");

        var animals = new AnimalManager(world.Seed);
        int surfaceY = world.GetHighestSolidY(16, 16);
        if (surfaceY < 0)
        {
            throw new Exception("Could not find surface for entity raycast test.");
        }

        for (int y = surfaceY + 1; y <= surfaceY + 5; y++)
        {
            world.SetBlock(16, y, 16, BlockType.Air);
            world.SetBlock(16, y, 20, BlockType.Air);
        }

        var sheep = animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, surfaceY + 1f, 20.5f), world);
        if (sheep == null)
        {
            throw new Exception("Failed to spawn sheep for raycast test.");
        }

        var origin = new Vector3(16.5f, surfaceY + 2f, 16.5f);
        var direction = Vector3.Normalize(new Vector3(0f, 0f, 1f));
        var (hit, distance) = animals.RaycastTarget(origin, direction, BlockInteractionSystem.RaycastRange);

        if (hit != sheep)
        {
            throw new Exception("Raycast did not hit the spawned sheep.");
        }

        if (distance <= 0f || distance > BlockInteractionSystem.RaycastRange)
        {
            throw new Exception($"Unexpected raycast distance: {distance}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunMeleeKillAnimal(AutonocraftGame game, Player player, VoxelWorld world)
    {
        Console.Write("Running Melee Kill Animal Test... ");

        int surfaceY = world.GetHighestSolidY(16, 16);
        if (surfaceY < 0)
        {
            throw new Exception("Could not find surface for melee kill test.");
        }

        for (int y = surfaceY + 1; y <= surfaceY + 5; y++)
        {
            world.SetBlock(16, y, 16, BlockType.Air);
            world.SetBlock(16, y, 18, BlockType.Air);
        }

        for (int z = 17; z <= 18; z++)
        {
            for (int y = surfaceY + 1; y <= surfaceY + 3; y++)
            {
                world.SetBlock(16, y, z, BlockType.Air);
            }
        }

        player.CreativeMode = true;
        player.Position = new Vector3(16.5f, surfaceY + 1.2f, 16.5f);

        var sheep = game.Animals.SpawnAt(AnimalType.Sheep, new Vector3(16.5f, surfaceY + 1f, 18.5f), world);
        if (sheep == null)
        {
            throw new Exception("Failed to spawn sheep for melee kill test.");
        }

        IntegrationTestHelpers.AimAt(player, sheep.Position + new Vector3(0f, sheep.Stats.Height * 0.5f, 0f));
        IntegrationTestHelpers.SyncCamera(game, player);

        int startCount = game.Animals.Count;
        for (int i = 0; i < 8; i++)
        {
            if (!game.Combat.TryInstantAttack(world, player, game.Animals, game.BlockInteraction, game.Particles, game.InteractionAnimator, game.Camera.Position, game.Camera.Front))
            {
                throw new Exception($"Attack {i + 1} failed to connect with sheep.");
            }
        }

        for (int frame = 0; frame < 20; frame++)
        {
            game.Animals.Update(0.02f, world);
        }

        if (game.Animals.Count != startCount - 1)
        {
            throw new Exception($"Expected sheep to be removed. Animals: {startCount} -> {game.Animals.Count}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
}
