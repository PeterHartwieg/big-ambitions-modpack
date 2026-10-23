using System.Linq;
using HardModePatch.Core;
using Xunit;

public class HappinessRulesTests
{
    static int Amount(string type) => HappinessRules.Find(HappinessValue.Prefix + type)!.Amount;

    [Fact]
    public void HomeTopsOutAt47BeforeRloMoods()
    {
        Assert.Equal(47, new[] { "hygiene", "watched_tv", "played_videogames", "read", "exercised" }.Sum(Amount));
    }

    [Fact]
    public void HomeActivitiesBankUpToFourDays()
    {
        foreach (var type in new[] { "watched_tv", "played_videogames", "read", "exercised" })
        {
            var v = HappinessRules.Find(HappinessValue.Prefix + type)!;
            Assert.False(v.Outside);
            Assert.Equal(96, v.MaxHours);
        }
    }

    [Fact]
    public void FiveDifferentOutingsReachTheLowForties()
    {
        Assert.InRange(new[] { "watched_show", "played_golf", "went_swimming", "walked_in_the_park", "walked_in_the_beach" }.Sum(Amount), 40, 48);
    }

    [Fact]
    public void WhileDoingItCountsTheSameAsTheResult()
    {
        Assert.Equal(Amount("watched_show"), Amount("watching_show"));
        Assert.Equal(Amount("read"), Amount("reading"));
        Assert.Equal(Amount("went_to_nightclub"), Amount("in_a_nightclub"));
    }

    [Theory]
    [InlineData("watched_show", 6, 120)]
    [InlineData("gambled", -1, 168)]
    [InlineData("played_golf", 0, 0)]
    [InlineData("read", 36, 36)]
    [InlineData("first_apartment", 120, 120)]
    public void OneOutsideVisitFillsTheWholeDuration(string type, int requested, int expected)
    {
        Assert.Equal(expected, HappinessRules.GrantedHours(HappinessValue.Prefix + type, requested));
    }

    [Fact]
    public void NoHomeIsMinus15OnlyWhileAHotelRoomIsBooked()
    {
        Assert.Equal(-15, HappinessRules.NoHomeAmount(-50, true));
        Assert.Equal(-50, HappinessRules.NoHomeAmount(-50, false));
    }

    [Fact]
    public void EveryTypeIsListedOnceAndNoneIsRlo()
    {
        Assert.Equal(HappinessRules.All.Count, HappinessRules.All.Select(v => v.Type).Distinct().Count());
        Assert.All(HappinessRules.All, v => Assert.StartsWith("ba:happinessmodifier_", v.Type));
    }
}
