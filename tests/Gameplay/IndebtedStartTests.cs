using HardModePatch.Core;
using Xunit;

public class IndebtedStartTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    public void OnlyAnExplicitNewScenarioWithoutTheOldWriterCanBeArmed(bool selected, bool hardcore, bool marked, bool expected)
    {
        Assert.Equal(expected, IndebtedStart.CanArm(selected, hardcore, marked));
    }

    [Theory]
    [InlineData(null, false, true, 16, 158, false)]
    [InlineData("complete-v1", false, true, 16, 158, false)]
    [InlineData("complete-v1", true, true, 16, 158, false)]
    [InlineData("failed-v1", false, true, 16, 158, false)]
    [InlineData("pending-v1", true, true, 16, 158, false)]
    [InlineData("pending-v1", false, false, 16, 158, false)]
    [InlineData("pending-v1", false, true, -1, 158, false)]
    [InlineData("pending-v1", false, true, 16, 0, false)]
    [InlineData("pending-v1", false, true, 0, 158, true)]
    [InlineData("pending-v1", false, true, 63, 251, true)]
    public void ReloadPayoffExistingDebtAndInvalidTermsCannotRecreateTheLoan(string? state, bool debt, bool bank, int interest, int payment, bool expected)
    {
        Assert.Equal(expected, IndebtedStart.CanComplete(state, debt, bank, interest, payment));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(4000, false)]
    public void CharacterCreationWaitsForTheCityButALoadedCityWithoutABankFails(int registrations, bool wait)
    {
        Assert.Equal(wait, IndebtedStart.ShouldWaitForCity(registrations));
    }
}
