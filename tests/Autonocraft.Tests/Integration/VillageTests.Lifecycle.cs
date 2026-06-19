using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autonocraft.Core;
using DevCommands = Autonocraft.Core.DevCommands.DevCommandRouter;
using Autonocraft.Ai;
using Autonocraft.Domain.Core;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Tests.Integration;

public static partial class VillageTests
{
    public static void RunFullVillageLifecycleJobs()
    {
        Console.Write("Running Full Village Lifecycle Jobs Test... ");
        var session = new GameSession(9191);
        var world = session.Grid;
        var villages = session.Villages;
        var villagers = session.Villagers;
        var animals = session.Animals;
        world.UpdateChunksAround(null, new Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f), 3);
        villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        var village = villages.GetPrimaryVillage();
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        villagers.Update(0f, world, new[] { village });
        villages.CreativeMode = true;

        BuildHouseThroughSimulation(villages, villagers, world, animals, village);
        ExerciseFarmJob(villages, villagers, world, animals, village);
        ExerciseMineAndMasonJobs(villages, villagers, world, animals, village);
        ExerciseHaulJob(villages, villagers, world, animals, village);
        ExerciseCraftCookAndHuntJobs(villages, villagers, world, animals, village);

        Console.WriteLine("PASSED");
    }

}
