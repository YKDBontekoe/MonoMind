using Autonocraft.Domain.Core;
using Autonocraft.Engine;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class SceneLightingTests
{
    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.31f)]
    [InlineData(0.62f)]
    [InlineData(0.81f)]
    public void LightingScalarsStayNormalizedAcrossCycle(float timeOfDay)
    {
        var lighting = SceneLighting.FromTimeOfDay(timeOfDay);
        Assert.InRange(lighting.DayLight, 0f, 1f);
        Assert.InRange(lighting.SunsetFactor, 0f, 1f);
        Assert.InRange(lighting.TwilightFactor, 0f, 1f);
    }

    [Fact]
    public void NoonEnablesSunAndDisablesMoon()
    {
        var lighting = SceneLighting.FromTimeOfDay(DayNightCycle.Noon);
        Assert.True(lighting.SunEnabled);
        Assert.False(lighting.MoonEnabled);
        Assert.True(lighting.SunDirection.Y > 0.02f);
        Assert.True(lighting.MoonDirection.Y < -0.02f);
        Assert.True(lighting.DayLight > 0.9f);
    }

    [Fact]
    public void MidnightEnablesMoonAndDisablesSun()
    {
        var lighting = SceneLighting.FromTimeOfDay(DayNightCycle.Midnight);
        Assert.False(lighting.SunEnabled);
        Assert.True(lighting.MoonEnabled);
        Assert.True(lighting.MoonDirection.Y > 0.02f);
    }

    [Fact]
    public void SunAndMoonDirectionsAreOpposite()
    {
        var lighting = SceneLighting.FromTimeOfDay(0.15f);
        Assert.Equal(-lighting.SunDirection, lighting.MoonDirection);
    }

    [Fact]
    public void SunsetHasStrongerSunsetTintThanNoon()
    {
        var noon = SceneLighting.FromTimeOfDay(DayNightCycle.Noon);
        var sunset = SceneLighting.FromTimeOfDay(DayNightCycle.Sunset);
        Assert.True(sunset.SunsetFactor > noon.SunsetFactor);
    }
}
