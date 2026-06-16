using System;
using System.Numerics;
using Autonocraft.World;

namespace Autonocraft.Tests.Integration;

public static class ChunkStreamingTests
{
    public static void RunInitialLoadWaitsForInFlightGeneration()
    {
        Console.Write("Running Initial Load In-Flight Wait Test... ");

        using var world = new VoxelWorld(1337, WorldGenParams.ForType(WorldType.Default));
        var spawn = new Vector3(16f, 64f, 16f);
        world.BeginInitialLoad(spawn, renderDistance: 2);

        for (int i = 0; i < 3; i++)
        {
            world.ProcessPendingWork(null, spawn, 2, maxTerrainPerFrame: 1, maxMeshPerFrame: 0);
        }

        if (world.InFlightGenerationCount > 0 && world.IsInitialLoadComplete())
        {
            throw new Exception("Initial load reported complete while terrain generation was still in flight.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunFaultedChunkGenerationDoesNotCrash()
    {
        Console.Write("Running Faulted Chunk Generation Test... ");

        using var world = new VoxelWorld(1337, WorldGenParams.ForType(WorldType.Default));
        var pos = new Vector3(16f, 64f, 16f);
        world.UpdateChunksAround(null, pos, 2);
        world.InjectFaultedChunkJobForTests(99, 99);

        for (int i = 0; i < 5; i++)
        {
            world.ProcessPendingWork(null, pos, 2);
        }

        if (world.GetActiveChunks().Exists(c => c.ChunkX == 99 && c.ChunkZ == 99))
        {
            throw new Exception("Faulted chunk generation should not register a chunk.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunChunkUnloadDiscardsStaleInFlight()
    {
        Console.Write("Running Chunk Unload Stale In-Flight Test... ");

        using var world = new VoxelWorld(1337, WorldGenParams.ForType(WorldType.Default));
        const int renderDistance = 3;
        var start = new Vector3(16f, 64f, 16f);
        world.UpdateChunksAround(null, start, renderDistance);

        for (int frame = 0; frame < 30; frame++)
        {
            world.ProcessPendingWork(null, start, renderDistance);
        }

        int peakChunks = world.ActiveChunkCount;

        for (int step = 0; step < 8; step++)
        {
            var farPos = new Vector3(16f + step * 32f, 64f, 16f);
            world.UpdateChunksAround(null, farPos, renderDistance);

            for (int frame = 0; frame < 15; frame++)
            {
                world.ProcessPendingWork(null, farPos, renderDistance);
            }

            int maxExpected = (renderDistance + 1) * 2 + 1;
            maxExpected *= maxExpected;
            maxExpected += renderDistance * 4;

            if (world.ActiveChunkCount > maxExpected)
            {
                throw new Exception(
                    $"Expected bounded chunk count after fast travel, got {world.ActiveChunkCount} (peak {peakChunks}, max {maxExpected}).");
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunEnsureChunksLoadedDoesNotUnloadPlayerRadius()
    {
        Console.Write("Running Ensure Chunks Loaded No Unload Test... ");

        using var world = new VoxelWorld(1337, WorldGenParams.ForType(WorldType.Default));
        const int renderDistance = 8;
        var playerPos = new Vector3(16.5f, 64f, 16.5f);
        world.UpdateChunksAround(null, playerPos, renderDistance);

        for (int frame = 0; frame < 80; frame++)
        {
            world.ProcessPendingWork(null, playerPos, renderDistance, maxTerrainPerFrame: 24, maxMeshPerFrame: 8);
        }

        int loadedAroundPlayer = world.ActiveChunkCount;
        if (loadedAroundPlayer < (renderDistance * 2) * (renderDistance * 2))
        {
            throw new Exception($"Expected a full player radius loaded, got {loadedAroundPlayer} chunks.");
        }

        var farVillageCenter = new Vector3(64.5f, 64f, 64.5f);
        for (int i = 0; i < 30; i++)
        {
            world.EnsureChunksLoaded(farVillageCenter, chunkRadius: 2);
            world.ProcessPendingWork(null, playerPos, renderDistance, maxTerrainPerFrame: 4, maxMeshPerFrame: 2);
        }

        if (world.ActiveChunkCount < loadedAroundPlayer - 16)
        {
            throw new Exception(
                $"EnsureChunksLoaded unloaded player chunks: before={loadedAroundPlayer}, after={world.ActiveChunkCount}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
}
