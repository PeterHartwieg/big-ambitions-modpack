using HardModePatch.Core;
using Xunit;

// RLO 1.0.5 review (docs/rlo-compatibility.md): what changed for the pack's rules.
public class Rlo105Tests
{
    [Fact]
    public void Rlo1CatalogueOwnsTheDiplomaIds()
    {
        // RLO 0.50.0 left 18, 19 and 34 free; RLO 1.0 uses them for bricklaying, the crane ticket and construction management.
        Assert.False(FactoryDiplomas.IdsTaken(new[] { 0, 10, 17, 20, 33, 35, 61 }));
        Assert.False(FactoryDiplomas.IdsTaken(new int[0]));
        Assert.True(FactoryDiplomas.IdsTaken(new[] { 0, 17, 18, 19, 20, 34, 62 }));
        foreach (var c in FactoryDiplomas.All) Assert.True(FactoryDiplomas.IdsTaken(new[] { 1, c.Id }));
        Assert.False(FactoryDiplomas.CanInject(new[] { 0, 17, 18, 19, 20, 34, 62 }, 64));
    }

    [Fact]
    public void Rlo1PhoneApplicationUsesTheApplyLabel()
    {
        Assert.Equal("hardmode_closed_apply", UtilityClosureReason.Key("reallife_util_closed_apply_app", true, 47, 49));
        Assert.Null(UtilityClosureReason.Key("reallife_util_closed_apply_app", true, 49, 49));
        Assert.Null(UtilityClosureReason.Key("reallife_util_closed_apply_app", false, 47, 49));
    }
}
