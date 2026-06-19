using System;
using System.Numerics;
using Autonocraft.Domain.World;
using Autonocraft.Engine;
using Autonocraft.World;
using Autonocraft.World.Generation;
using Xunit;

namespace Autonocraft.Tests.Unit;

public sealed class ParticleSystemHighAltitudeTests
{
    [Fact]
    public void UpdateAmbient_AtWorldCeiling_DoesNotThrow()
    {
        var particles = new Autonocraft.Engine.ParticleSystem();
        var world = new VoxelWorld(1337);

        for (int frame = 0; frame < 120; frame++)
        {
            particles.UpdateAmbient(
                0.016f,
                new Vector3(64f, Chunk.Height - 3f, 64f),
                world,
                timeOfDay: 0.35f,
                weather: new WeatherSystem());
        }
    }
}
