using HardModePatch.Core;
using Xunit;

public class RunningCostsTests
{
    static readonly RunningCosts.Appliance None = default(RunningCosts.Appliance);

    [Theory]
    [InlineData(1000, 12, 1000)]
    [InlineData(1000, 24, 1750)]
    [InlineData(1000, 6, 625)]
    [InlineData(1000, 30, 1750)]
    [InlineData(250, 0, 250)]
    [InlineData(0, 12, 0)]
    public void FloorUseFollowsTheHoursOpen(long rloUse, int hours, long expected)
    {
        Assert.Equal(expected, RunningCosts.DayUse(true, rloUse, hours, None));
        Assert.Equal(expected, RunningCosts.DayUse(false, rloUse, hours, None));
    }

    [Fact]
    public void CookingDrawsOnTopWhileOpen()
    {
        var fryer = RunningCosts.Of("ba:itemname_industrialfryermachine", 0x40);
        Assert.Equal(45000 + 60000, RunningCosts.DayUse(true, 45000, 12, fryer));
        Assert.Equal(11250, RunningCosts.DayUse(true, 11250, 0, fryer));
    }

    [Fact]
    public void RefrigerationReplacesRlosStandbyWhenItDrawsMore()
    {
        // 50 m2 of food and drink: RLO's open day 45 kWh, its standby quarter 11.25 kWh.
        var fridge = RunningCosts.Of("ba:itemname_industrialfridge", 0x40);
        var small = RunningCosts.Of("ba:itemname_drinksfridge", 0x40);
        Assert.Equal(12000 + 33750, RunningCosts.DayUse(true, 45000, 12, fridge));
        Assert.Equal(12000, RunningCosts.DayUse(true, 11250, 0, fridge));
        Assert.Equal(45000, RunningCosts.DayUse(true, 45000, 12, small));
        Assert.Equal(11250, RunningCosts.DayUse(true, 11250, 0, small));
    }

    [Fact]
    public void PlumbingReplacesRlosBasinsWhenItUsesMore()
    {
        var sink = RunningCosts.Of("ba:itemname_countersink", 0x1000001);
        var washes = new RunningCosts.Appliance(0, 0, 300);
        Assert.Equal(1500, RunningCosts.DayUse(false, 1500, 12, sink));
        Assert.Equal(375 + 3600, RunningCosts.DayUse(false, 1500, 12, washes));
        Assert.Equal(375, RunningCosts.DayUse(false, 375, 0, washes));
    }

    [Fact]
    public void UnknownItemsFallBackOnTheGamesFlagsAndStockUsesNothing()
    {
        Assert.Equal(10000, RunningCosts.Of("othermod:press", 0x80).OpenWatts);
        Assert.Equal(150, RunningCosts.Of("othermod:pc", 0x800010).OpenWatts);
        Assert.Equal(30, RunningCosts.Of("othermod:basin", 0x1000000).LitresPerHour);
        Assert.Equal(20, RunningCosts.Of("othermod:wc", 0x2000000).LitresPerHour);
        Assert.False(RunningCosts.Of("ba:itemname_ceilinglampwarehouseforsale", 0x2000).Uses);
        Assert.False(RunningCosts.Of("ba:itemname_industrialfryermachine", 0x2000 | 0x40).Uses);
        Assert.False(RunningCosts.Of("ba:itemname_armchair1", 0x1000).Uses);
        Assert.False(RunningCosts.Of(null, 0).Uses);
    }

    [Fact]
    public void SeatsAreComputersNotStockForSale()
    {
        Assert.True(RunningCosts.IsSeat(0x800010));
        Assert.False(RunningCosts.IsSeat(0x10));
        Assert.False(RunningCosts.IsSeat(0x800000 | 0x2000));
    }

    [Theory]
    [InlineData(10, 1000, 5, 30000)]
    [InlineData(10, 1200, 5, 36000)]
    [InlineData(1, 1000, 30, 500)]
    [InlineData(0, 1000, 5, 0)]
    [InlineData(3, 1000, 0, 45000)]
    [InlineData(1, 9000, 1, 60000)]
    [InlineData(1, 100, 1, 7500)]
    public void SeatsCostAMonthOfITSharedOverTheGamesMonth(int seats, int level, int monthDays, int expected)
    {
        Assert.Equal(expected, RunningCosts.SeatDayCents(seats, level, monthDays));
    }

    [Theory]
    [InlineData("ba:businesstype_lawfirm", true)]
    [InlineData("ba:businesstype_headquarters", true)]
    [InlineData("ba:businesstype_importexport", true)]
    [InlineData("ba:businesstype_fastfoodrestaurant", false)]
    [InlineData("reallife:businesstype_softwarestudio", false)]
    [InlineData(null, false)]
    public void OfficesAreTheGamesOfficeTypes(string? type, bool office)
    {
        Assert.Equal(office, RunningCosts.IsOffice(type));
    }

    [Theory]
    [InlineData(false, 8, 0)]
    [InlineData(true, 8, 8)]
    [InlineData(true, 30, 24)]
    [InlineData(true, -2, 0)]
    public void HoursComeFromTheSchedule(bool open, int hours, int expected)
    {
        Assert.Equal(expected, RunningCosts.Hours(open, hours));
    }

    [Fact]
    public void RunningCostRulesStartOffForTheModpackToTurnOn()
    {
        Assert.False(Rules.EquipmentUtilities.Default);
        Assert.False(Rules.OfficeIt.Default);
        Assert.Null(Rules.EquipmentUtilities.Target);
        Assert.Null(Rules.OfficeIt.Target);
    }
}
