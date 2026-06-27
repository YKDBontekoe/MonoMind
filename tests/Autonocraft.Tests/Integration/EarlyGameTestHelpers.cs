using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Village;
using Autonocraft.World;

namespace Autonocraft.Tests.Integration;

internal static class EarlyGameTestHelpers
{
    public static GameSession CreateStarterSession(int seed = 424242)
    {
        var session = new GameSession(seed);
        var loadPos = new Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f);
        session.Grid.UpdateChunksAround(null, loadPos, 3);
        session.ResetOpeningGuideForNewWorld();
        session.Villages.InitializeStarterSettlement(session.Grid, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        session.PlacePlayerOnSurface(GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
        return session;
    }

    public static Village.Village RequireStarterVillage(GameSession session)
        => session.Villages.GetPrimaryVillage() ?? throw new Exception("Starter village missing.");

    public static int StarterPathEndX(Village.Village village)
        => village.AnchorX > GameConstants.DefaultSpawnX ? village.AnchorX - 2 : village.AnchorX + 2;

    public static (int X, int Z) StarterMarker(Village.Village village)
        => (village.AnchorX - 3, village.AnchorZ - 2);

    public static (int X, int Z) StarterResourceStump(Village.Village village)
        => (village.AnchorX + 7, village.AnchorZ - 3);

    public static int SurfaceAnchorY(VoxelWorld world, int x, int z)
        => StructureFingerprint.FindSurfaceAnchorY(world, x, z);
}
