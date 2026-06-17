using Autonocraft.Domain.Core;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class DayNightCycleTests
{
    [Theory]
    [InlineData(0.0f, TimePhase.Dawn)]
    [InlineData(0.03f, TimePhase.Dawn)]
    [InlineData(0.05f, TimePhase.Dawn)]
    [InlineData(0.10f, TimePhase.Day)]
    [InlineData(0.31f, TimePhase.Day)]
    [InlineData(0.50f, TimePhase.Day)]
    [InlineData(0.58f, TimePhase.Dusk)]
    [InlineData(0.61f, TimePhase.Dusk)]
    [InlineData(0.65f, TimePhase.Night)]
    [InlineData(0.95f, TimePhase.Night)]
    public void GetTimePhaseMatchesExpectedBoundaries(float timeOfDay, TimePhase expected)
    {
        Assert.Equal(expected, DayNightCycle.GetTimePhase(timeOfDay));
    }

    [Theory]
    [InlineData(-0.25f, 0.75f)]
    [InlineData(1.4f, 0.4f)]
    [InlineData(2.62f, 0.62f)]
    public void NormalizeTimeWrapsAndClamps(float input, float expected)
    {
        Assert.Equal(expected, DayNightCycle.NormalizeTime(input), precision: 4);
    }

    [Theory]
    [InlineData(0.0f, false)]
    [InlineData(0.58f, false)]
    [InlineData(0.65f, true)]
    [InlineData(0.31f, false)]
    public void IsNightFollowsTimePhase(float timeOfDay, bool expectedNight)
    {
        Assert.Equal(expectedNight, DayNightCycle.IsNight(timeOfDay));
    }

    [Theory]
    [InlineData(0.12f, true)]
    [InlineData(0.60f, true)]
    [InlineData(0.03f, false)]
    [InlineData(0.70f, false)]
    public void IsBroadDaytimeMatchesHudWindow(float timeOfDay, bool expectedDaytime)
    {
        Assert.Equal(expectedDaytime, DayNightCycle.IsBroadDaytime(timeOfDay));
    }

    [Fact]
    public void GetHudTimeLabelCoversFullCycle()
    {
        Assert.Equal("DAWN", DayNightCycle.GetHudTimeLabel(0.03f));
        Assert.Equal("NOON", DayNightCycle.GetHudTimeLabel(DayNightCycle.Noon));
        Assert.Equal("DUSK", DayNightCycle.GetHudTimeLabel(0.58f));
        Assert.Equal("NIGHT", DayNightCycle.GetHudTimeLabel(0.9f));
    }

    [Fact]
    public void WarpTimeForSunMapsKeyTimesToSunAngles()
    {
        float dawn = DayNightCycle.WarpTimeForSun(0f);
        float noon = DayNightCycle.WarpTimeForSun(DayNightCycle.Noon);
        float dusk = DayNightCycle.WarpTimeForSun(DayNightCycle.Sunset);
        float midnight = DayNightCycle.WarpTimeForSun(DayNightCycle.Midnight);

        Assert.Equal(0f, dawn, precision: 4);
        Assert.Equal(0.25f, noon, precision: 4);
        Assert.Equal(0.5f, dusk, precision: 4);
        Assert.Equal(0.75f, midnight, precision: 4);
    }

    [Fact]
    public void DaylightIsLongerThanNight()
    {
        Assert.True(DayNightCycle.DaylightFraction > 0.5f);
        Assert.True(DayNightCycle.DaylightFraction > 1f - DayNightCycle.DaylightFraction);
    }
}
