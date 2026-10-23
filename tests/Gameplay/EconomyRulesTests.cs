using System.Linq;
using HardModePatch.Core;
using Xunit;

public class EconomyRulesTests
{
    [Theory]
    [InlineData(40, 10, 20)]
    [InlineData(40, 30, 40)]
    [InlineData(40, 0, 0)]
    [InlineData(5, 3, 5)]
    public void CafeCustomersAreCappedByPcs(int gameCap, int pcs, int expected)
    {
        Assert.Equal(expected, CafeMath.Cap(gameCap, pcs));
    }

    [Fact]
    public void BakeryMarginsSitNearVanillaFood()
    {
        Assert.Equal(5, BakeryPrices.All.Count);
        Assert.All(BakeryPrices.All, p =>
        {
            Assert.StartsWith("bakeryxx:itemname_", p.ItemName);
            Assert.InRange(p.Margin, 2.0f, 5.0f);
            Assert.True(p.Market > p.Wholesale);
        });
        Assert.Equal(7.50f, BakeryPrices.For("bakeryxx:itemname_bread")!.Market);
        Assert.Null(BakeryPrices.For("ba:itemname_croissant"));
        Assert.Equal(BakeryPrices.All.Count, BakeryPrices.All.Select(p => p.ItemName).Distinct().Count());
    }

    [Theory]
    [InlineData(1000000, 51, 490000)]
    [InlineData(1000.01, 51, 490.00)]
    [InlineData(500, 0, 500)]
    [InlineData(500, 150, 0)]
    [InlineData(-250, 51, -250)]
    public void WithdrawalsAreTaxedAtTheSaveRate(decimal amount, int tax, decimal expected)
    {
        Assert.Equal(expected, BankRules.AfterTax(amount, tax));
    }

    [Fact]
    public void LoanProductsOutsideTheLimitsAreRefused()
    {
        Assert.Null(BankRules.CheckLoan(0.15m, 0.03m, 6));
        Assert.Contains("APR", BankRules.CheckLoan(0.25m, 0.01m, 12));
        Assert.Contains("fee", BankRules.CheckLoan(0.10m, 0.10m, 12));
        Assert.Contains("term", BankRules.CheckLoan(0.10m, 0.01m, 1));
        Assert.Null(BankRules.CheckCard(0.30m));
        Assert.NotNull(BankRules.CheckCard(0.45m));
    }

    [Fact]
    public void RateAdjustmentsStopAtTheCaps()
    {
        Assert.Equal(0.15m, BankRules.ClampRate(isLoan: true, isCard: false, 0.20m));
        Assert.Equal(0.30m, BankRules.ClampRate(isLoan: false, isCard: true, 0.40m));
        Assert.Equal(0.45m, BankRules.ClampRate(isLoan: false, isCard: false, 0.45m));
    }

    [Fact]
    public void ApplicantsKeepTheScoreTheyDrew()
    {
        Assert.Equal(612, BankRules.ApplicantScore(850, 612));
    }
}

public class EconomyRulesRoundOneTests
{
    [Fact]
    public void StationsPairPcsWithSeatsUnderTheSameRoot()
    {
        // Two PCs and one chair on desk A, one PC and two chairs on desk B, a PC on desk C with no chair.
        var pcs = new[] { "A", "A", "B", "C" };
        var seats = new[] { "A", "B", "B" };
        Assert.Equal(2, CafeMath.UsableStations(pcs, seats));
    }

    [Fact]
    public void UnresolvedRootsCountForNothing()
    {
        Assert.Equal(0, CafeMath.UsableStations(new string?[] { null }!, new string?[] { null }!));
    }

    [Theory]
    [InlineData(0.15, 0.005, 0)]
    [InlineData(0.14, 0.005, 0.005)]
    [InlineData(0.148, 0.005, 0.002)]
    [InlineData(0.20, 0.005, 0)]
    [InlineData(0.15, -0.005, -0.005)]
    public void LoanRateNudgesStopAtTheCap(double current, double delta, double expected)
    {
        Assert.Equal((decimal)expected, BankRules.ClampDelta(isLoan: true, isCard: false, (decimal)current, (decimal)delta));
    }

    [Fact]
    public void DepositRateNudgesAreUntouched()
    {
        Assert.Equal(0.01m, BankRules.ClampDelta(isLoan: false, isCard: false, 0.45m, 0.01m));
    }

    [Fact]
    public void FeesAndTermsAreClampedToTheLimits()
    {
        Assert.Equal(0.03m, BankRules.ClampFee(0.10m));
        Assert.Equal(0.02m, BankRules.ClampFee(0.02m));
        Assert.Equal(6, BankRules.ClampTerm(1));
        Assert.Equal(24, BankRules.ClampTerm(24));
    }
}
