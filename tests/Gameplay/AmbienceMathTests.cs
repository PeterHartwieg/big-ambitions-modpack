using HardModePatch.Core;
using Xunit;

public class AmbienceMathTests
{
    static AmbienceHour Hour(double vanillaRaw, int capacity, bool eligible = true)
    {
        return new AmbienceHour
        {
            VanillaRaw = vanillaRaw,
            VanillaTarget = capacity > 0 ? AmbienceMath.CeilingToInt(System.Math.Min(vanillaRaw, capacity)) : 0,
            EffectiveCapacity = capacity,
            Eligible = eligible
        };
    }

    [Fact]
    public void BonusIsAShareOfTheGamesOwnCustomers()
    {
        var hour = Hour(20.0, 60);
        AmbienceMath.ScaleWithDemand(hour, 1.0, 0.10);
        Assert.Equal(2.0, hour.AmbienceRaw, 6);
        Assert.Equal(22, hour.FinalTarget);
        Assert.Equal(2, hour.AddedAfterCapacity);
    }

    [Fact]
    public void LowDemandStaysLowWhateverTheBuildingSize()
    {
        // At 0.1 customer promotion a big shop might get 1.5 customers an hour. The mod would have added
        // 10% of a 200-customer building; scaled with demand it adds 0.15.
        var hour = Hour(1.5, 200);
        AmbienceMath.ScaleWithDemand(hour, 1.0, 0.10);
        Assert.Equal(0.15, hour.AmbienceRaw, 6);
        Assert.Equal(2, hour.FinalTarget);
        Assert.Equal(0, hour.AddedAfterCapacity);
    }

    [Fact]
    public void NoGameCustomersMeansNoAmbienceCustomers()
    {
        var hour = Hour(0.0, 60);
        AmbienceMath.ScaleWithDemand(hour, 1.0, 0.20);
        Assert.Equal(0.0, hour.AmbienceRaw);
        Assert.Equal(0, hour.AddedAfterCapacity);
    }

    [Fact]
    public void IneligibleHoursGetNothing()
    {
        var hour = Hour(30.0, 60, eligible: false);
        AmbienceMath.ScaleWithDemand(hour, 1.0, 0.20);
        Assert.Equal(0.0, hour.AmbienceRaw);
        Assert.Equal(hour.VanillaTarget, hour.FinalTarget);
    }

    [Fact]
    public void CapacityBlocksTheBonusAndIsCounted()
    {
        var hour = Hour(58.0, 60);
        AmbienceMath.ScaleWithDemand(hour, 1.0, 0.10);   // 63.8 wanted, 60 fit
        Assert.Equal(60, hour.FinalTarget);
        Assert.Equal(2, hour.AddedAfterCapacity);
        Assert.Equal(4, hour.TotalBlockedByCapacity);
        Assert.Equal(4, hour.BlockedByCapacity);
    }

    [Fact]
    public void ScoreIsClamped()
    {
        var hour = Hour(10.0, 60);
        AmbienceMath.ScaleWithDemand(hour, 3.0, 0.10);
        Assert.Equal(1.0, hour.AmbienceRaw, 6);
    }
}
