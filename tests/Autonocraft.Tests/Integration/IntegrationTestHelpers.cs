using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Entities;

namespace Autonocraft.Tests.Integration;

internal static class IntegrationTestHelpers
{
    public static void SyncCamera(AutonocraftGame game, Player player)
    {
        game.Camera.Position = player.Position + new Vector3(0f, Player.EyeHeight, 0f);
        game.Camera.Yaw = player.Yaw;
        game.Camera.Pitch = player.Pitch;
    }

    public static void AimAt(Player player, Vector3 target)
    {
        var eye = player.Position + new Vector3(0f, Player.EyeHeight, 0f);
        var delta = target - eye;
        if (delta == Vector3.Zero)
        {
            return;
        }

        delta = Vector3.Normalize(delta);
        player.Pitch = MathF.Asin(Math.Clamp(delta.Y, -1f, 1f)) * (180f / MathF.PI);
        player.Yaw = MathF.Atan2(delta.Z, delta.X) * (180f / MathF.PI);
    }
}
