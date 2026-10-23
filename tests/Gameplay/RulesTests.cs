using System.IO;
using System.Linq;
using System.Text.Json;
using HardModePatch.Core;
using Xunit;

public class RulesTests
{
    [Fact]
    public void RuleIdsAreUniqueAndTargetPatchesStartOnTheirHardDefaults()
    {
        Assert.Equal(Rules.All.Count, Rules.All.Select(r => r.Id).Distinct().Count());
        Assert.All(Rules.All.Where(r => r.Target != null && r != Rules.FactoryDiplomas), r => Assert.True(r.Default));
        Assert.False(Rules.IndebtedScenario.Default);
        Assert.False(Rules.FactoryDiplomas.Default);
    }

    [Fact]
    public void PerformanceOptionsAreNotOwnedByHardModePatch()
    {
        Assert.DoesNotContain(Rules.All, r => r.Id == "bakery_help_discovery" || r.Id == "hyperreal_light_discovery");
        Assert.Contains(Rules.All, r => r.Id == "bakery_vanilla_margins");
        Assert.Contains(Rules.All, r => r.Id == "hyperreal_map_return_fix");
    }

    [Fact]
    public void EveryRuleAndTargetHasAnEnglishLabel()
    {
        var labels = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(File.ReadAllText(Path.Combine("Locales", "en.json")))!;
        Assert.All(Rules.All, r => Assert.True(labels.ContainsKey(r.LabelKey), r.LabelKey));
        Assert.True(labels.ContainsKey("hardmode_title"));
        foreach (var target in Rules.All.Where(r => r.Target != null).Select(r => r.Target).Distinct())
            Assert.True(labels.ContainsKey("hardmode_target_" + target.Key), target.Key);
        Assert.True(labels.ContainsKey("hardmode_scenario_header"));
        Assert.True(labels.ContainsKey("hardmode_scenario_unavailable"));
    }

    [Fact]
    public void VanillaPricesComeBackOnlyForSavesHardcoreDidNotMark()
    {
        Assert.True(HardcoreStart.ShouldRestoreVanillaPrices(snapshotTaken: true, saveIsHardcore: false));
        Assert.False(HardcoreStart.ShouldRestoreVanillaPrices(snapshotTaken: true, saveIsHardcore: true));
        Assert.False(HardcoreStart.ShouldRestoreVanillaPrices(snapshotTaken: false, saveIsHardcore: false));
    }
}

public class RuleChangeTests
{
    [Fact]
    public void SetRaisesChangedOnlyOnARealChange()
    {
        var rule = new Rule("test_rule", Targets.EmpireBank, true);
        int calls = 0;
        bool last = true;
        rule.Changed += value => { calls++; last = value; };
        rule.Set(true);
        Assert.Equal(0, calls);
        rule.Set(false);
        Assert.Equal(1, calls);
        Assert.False(last);
        Assert.False(rule.Enabled);
    }
}

public class RentRulesTests
{
    [Theory]
    [InlineData("ba:buildingtype_residential", 39, 78)]
    [InlineData("ba:buildingtype_retail", 34, 102)]
    [InlineData("ba:buildingtype_office", 1, 3)]
    [InlineData("ba:buildingtype_retail", 0, 0)]
    public void FlatsDoubleAndEverythingElseTriples(string type, int market, int expected)
    {
        Assert.Equal(expected, RentRules.Scale(market, type));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void OnlyRivalsKeepTheGameRentInEstimates(bool rented, bool owned, bool keep)
    {
        Assert.Equal(keep, RentRules.KeepGameRentInEstimate(rented, owned));
    }

    [Fact]
    public void RentRebalanceIsOptInLikeTheScenario()
    {
        Assert.False(Rules.RentRebalance.Default);
    }
}
