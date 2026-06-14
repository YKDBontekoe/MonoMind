using Autonocraft.Domain.Core;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class DayNightCycleTests
{
    [Theory]
    [InlineData(0.0f, TimePhase.Night)]
    [InlineData(0.19f, TimePhase.Night)]
    [InlineData(0.2f, TimePhase.Dawn)]
    [InlineData(0.29f, TimePhase.Dawn)]
    [InlineData(0.3f, TimePhase.Day)]
    [InlineData(0.5f, TimePhase.Day)]
    [InlineData(0.69f, TimePhase.Day)]
    [InlineData(0.7f, TimePhase.Dusk)]
    [InlineData(0.81f, TimePhase.Dusk)]
    [InlineData(0.82f, TimePhase.Night)]
    [InlineData(0.95f, TimePhase.Night)]
    public void GetTimePhaseMatchesExpectedBoundaries(float timeOfDay, TimePhase expected)
    {
        Assert.Equal(expected, DayNightCycle.GetTimePhase(timeOfDay));
    }

    [Theory]
    [InlineData(-0.25f, 0.75f)]
    [InlineData(1.4f, 0.4f)]
    [InlineData(2.82f, 0.82f)]
    public void NormalizeTimeWrapsAndClamps(float input, float expected)
    {
        Assert.Equal(expected, DayNightCycle.NormalizeTime(input), precision: 4);
    }

    [Theory]
    [InlineData(0.0f, true)]
    [InlineData(0.81f, false)]
    [InlineData(0.82f, true)]
    [InlineData(0.5f, false)]
    public void IsNightFollowsTimePhase(float timeOfDay, bool expectedNight)
    {
        Assert.Equal(expectedNight, DayNightCycle.IsNight(timeOfDay));
    }

    [Theory]
    [InlineData(0.23f, true)]
    [InlineData(0.77f, true)]
    [InlineData(0.22f, false)]
    [InlineData(0.78f, false)]
    public void IsBroadDaytimeMatchesHudWindow(float timeOfDay, bool expectedDaytime)
    {
        Assert.Equal(expectedDaytime, DayNightCycle.IsBroadDaytime(timeOfDay));
    }

    [Fact]
    public void GetHudTimeLabelCoversFullCycle()
    {
        Assert.Equal("NIGHT", DayNightCycle.GetHudTimeLabel(0.05f));
        Assert.Equal("DAWN", DayNightCycle.GetHudTimeLabel(0.25f));
        Assert.Equal("NOON", DayNightCycle.GetHudTimeLabel(0.5f));
        Assert.Equal("DUSK", DayNightCycle.GetHudTimeLabel(0.75f));
        Assert.Equal("NIGHT", DayNightCycle.GetHudTimeLabel(0.9f));
    }
}
