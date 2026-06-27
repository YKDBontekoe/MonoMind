using System;
using Autonocraft.Core;

namespace Autonocraft.Tests.Integration;

public static class EarlyGameSpawnTests
{
    public static void RunStarterAreaHasPathLandmarkAndResources()
    {
        Console.Write("Running Starter Area Path, Landmark, and Resources Test... ");

        var session = EarlyGameTestHelpers.CreateStarterSession();
        var world = session.Grid;
        var village = EarlyGameTestHelpers.RequireStarterVillage(session);

        int pathEndX = EarlyGameTestHelpers.StarterPathEndX(village);
        int pathTiles = 0;
        for (int x = Math.Min(GameConstants.DefaultSpawnX, pathEndX); x <= Math.Max(GameConstants.DefaultSpawnX, pathEndX); x++)
        {
            int y = world.GetHighestSolidY(x, GameConstants.DefaultSpawnZ);
            if (world.GetBlock(x, y, GameConstants.DefaultSpawnZ) == BlockType.OakPlank)
            {
                pathTiles++;
            }
        }

        if (pathTiles < 2)
        {
            throw new Exception($"Expected visible starter path from spawn to settlement, got {pathTiles} plank tiles.");
        }

        var marker = EarlyGameTestHelpers.StarterMarker(village);
        bool hasLantern = false;
        for (int y = 0; y < 128; y++)
        {
            if (world.GetBlock(marker.X, y, marker.Z) == BlockType.Lantern)
            {
                hasLantern = true;
                break;
            }
        }

        if (!hasLantern)
        {
            throw new Exception("Expected lantern marker near the starter settlement.");
        }

        var stump = EarlyGameTestHelpers.StarterResourceStump(village);
        int stumpY = EarlyGameTestHelpers.SurfaceAnchorY(world, stump.X, stump.Z);
        int logs = 0;
        for (int dy = 0; dy < 3; dy++)
        {
            if (world.GetBlock(stump.X, stumpY + dy, stump.Z) == BlockType.OakLog)
            {
                logs++;
            }
        }

        if (logs < 3)
        {
            throw new Exception($"Expected starter resource stump with 3 logs, got {logs}.");
        }

        Console.WriteLine("PASSED");
    }
}
