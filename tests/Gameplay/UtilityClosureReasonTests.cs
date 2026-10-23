using HardModePatch.Core;
using Xunit;

public class UtilityClosureReasonTests
{
    [Theory]
    [InlineData("net_install")]
    [InlineData("net_none")]
    [InlineData("net_outage")]
    [InlineData("connecting")]
    [InlineData("cut")]
    [InlineData("apply")]
    public void RecognizedCurrentUtilityClosuresHaveSpecificLabels(string reason)
    {
        Assert.Equal("hardmode_closed_" + reason,
            UtilityClosureReason.Key("reallife_util_closed_" + reason, true, 47, 49));
    }

    [Theory]
    [InlineData(false, 47, 49)]
    [InlineData(true, 49, 49)]
    [InlineData(true, 50, 49)]
    public void ManualOtherOrExpiredClosureKeepsTheGameMessage(bool owned, long now, long until)
    {
        Assert.Null(UtilityClosureReason.Key("reallife_util_closed_net_install", owned, now, until));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("future_unknown_reason")]
    public void UnknownReasonsAreNotGuessed(string? reason)
    {
        Assert.Null(UtilityClosureReason.Key(reason, true, 47, 49));
    }
}
