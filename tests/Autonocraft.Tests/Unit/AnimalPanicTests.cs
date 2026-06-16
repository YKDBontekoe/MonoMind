using System.Numerics;
using Autonocraft.Entities;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class AnimalPanicTests
{
    [Fact]
    public void TakeDamage_EntersPanicAndFleesAwayFromAttacker()
    {
        var animal = new Animal(AnimalType.Sheep, new Vector3(10f, 64f, 10f), 42);
        var attacker = new Vector3(12f, 64f, 10f);

        animal.TakeDamage(1f, attacker);

        Assert.True(animal.IsPanicking);
        Assert.True(animal.PanicTimer >= 4f);
        Assert.True(animal.WanderDistanceRemaining >= 14f);
        Assert.True(animal.WanderDirection.X < 0f);
        Assert.Equal(0f, animal.WanderDirection.Z, 3);
    }

    [Fact]
    public void TakeDamage_WhilePanicking_RefreshesTimerAndUpdatesDirection()
    {
        var animal = new Animal(AnimalType.Cow, new Vector3(0f, 64f, 0f), 7);
        animal.TakeDamage(1f, new Vector3(5f, 64f, 0f));
        Assert.True(animal.WanderDirection.X < 0f);

        animal.TakeDamage(1f, new Vector3(-4f, 64f, 0f));

        Assert.True(animal.IsPanicking);
        Assert.True(animal.PanicTimer >= 4f);
        Assert.True(animal.WanderDirection.X > 0f);
    }
}
