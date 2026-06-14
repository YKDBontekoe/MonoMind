using Autonocraft.Engine;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class SceneLightingTests
{
    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    public void LightingScalarsStayNormalizedAcrossCycle(float timeOfDay)
    {
        var lighting = SceneLighting.FromTimeOfDay(timeOfDay);
        Assert.InRange(lighting.DayLight, 0f, 1f);
        Assert.InRange(lighting.SunsetFactor, 0f, 1f);
        Assert.InRange(lighting.TwilightFactor, 0f, 1f);
    }

    [Fact]
    public void QuarterCycleEnablesSunAndDisablesMoon()
    {
        var lighting = SceneLighting.FromTimeOfDay(0.25f);
        Assert.True(lighting.SunEnabled);
        Assert.False(lighting.MoonEnabled);
        Assert.True(lighting.SunDirection.Y > 0.02f);
        Assert.True(lighting.MoonDirection.Y < -0.02f);
        Assert.True(lighting.DayLight > 0.9f);
    }

    [Fact]
    public void ThreeQuarterCycleEnablesMoonAndDisablesSun()
    {
        var lighting = SceneLighting.FromTimeOfDay(0.75f);
        Assert.False(lighting.SunEnabled);
        Assert.True(lighting.MoonEnabled);
        Assert.True(lighting.MoonDirection.Y > 0.02f);
    }

    [Fact]
    public void SunAndMoonDirectionsAreOpposite()
    {
        var lighting = SceneLighting.FromTimeOfDay(0.33f);
        Assert.Equal(-lighting.SunDirection, lighting.MoonDirection);
    }

    [Fact]
    public void HorizonCycleHasStrongerSunsetTintThanNoon()
    {
        var noon = SceneLighting.FromTimeOfDay(0.25f);
        var sunset = SceneLighting.FromTimeOfDay(0.5f);
        Assert.True(sunset.SunsetFactor > noon.SunsetFactor);
    }
}
