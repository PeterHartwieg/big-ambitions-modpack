using System.Linq;
using HardModePatch.Core;
using Xunit;

public class SeriesSettingsTests
{
    [Fact]
    public void EachOptionIsListedOnceWithAValueItsKindAllows()
    {
        var keys = SeriesSettings.Options.Select(o => o.Owner + ":" + o.Id).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(SeriesSettings.Options.Where(o => o.Kind == OptionKind.Toggle), o => Assert.InRange(o.Value, 0, 1));
        Assert.All(SeriesSettings.Options, o => Assert.True(o.Value >= 0 && !string.IsNullOrEmpty(o.Why), o.Label));
    }

    [Fact]
    public void OurOwnValuesAreOurRulesAndTurnThePackOn()
    {
        var own = SeriesSettings.Options.Where(o => o.Owner == SeriesSettings.Self).ToList();
        Assert.All(own, o => Assert.Contains(Rules.All, r => r.Id == o.Id));
        foreach (var rule in new[] { Rules.IndebtedScenario, Rules.HappinessRebalance, Rules.OwnerJobFullPay, Rules.FactoryDiplomas, Rules.EquipmentUtilities, Rules.OfficeIt })
            Assert.Contains(own, o => o.Id == rule.Id && o.Value == 1);
        // Explicit zero also disables an older series preset's saved rent multiplier.
        Assert.Contains(own, o => o.Id == Rules.RentRebalance.Id && o.Value == 0);
        Assert.False(Rules.RentRebalance.Default);
        Assert.False(Rules.EquipmentUtilities.Default);
        Assert.False(Rules.OfficeIt.Default);
        Assert.True(Rules.SeriesPrefill.Default);
    }

    [Fact]
    public void ThePlanValuesForTheOtherMods()
    {
        int Value(string owner, string id) => SeriesSettings.Options.Single(o => o.Owner == owner && o.Id == id).Value;
        Assert.Equal(1, Value(SeriesSettings.RivalBalance, "rival_preset"));
        Assert.Equal(0, Value(SeriesSettings.DynamicRivals, "wholesale_rival_count"));
        Assert.Equal(1, Value(SeriesSettings.DynamicRivals, "import_rival_count"));
        Assert.Equal(2, Value(SeriesSettings.DynamicRivals, "event_frequency"));
        foreach (var id in new[] { "camera_enabled", "camera_mouselook", "car_camera", "social_media" }) Assert.Equal(0, Value(SeriesSettings.Rlo, id));
        Assert.Equal(2, Value(SeriesSettings.Rlo, "business_frequency"));
        Assert.Equal(1, Value(SeriesSettings.Rlo, "careers_study"));
        Assert.DoesNotContain(SeriesSettings.Options, o => o.Id == "dev_diagnostics" || o.Id == "permadeath");
    }

    [Fact]
    public void NewGameHoldsTheSeriesValuesOnce()
    {
        var fields = SeriesSettings.NewGame.Select(v => v.Field).ToList();
        Assert.Equal(fields.Count, fields.Distinct().Count());
        object Get(string f) => SeriesSettings.NewGame.Single(v => v.Field == f).Value;
        Assert.Equal(60, Get("daysPerYear"));
        Assert.Equal(0, Get("startingMoney"));
        Assert.Equal(1.5f, Get("rivalsDifficultyMultiplier"));
        Assert.Equal(51, Get("taxPercentage"));
        Assert.Equal(0.5f, Get("baseCustomerPromotionMultiplier"));
        Assert.Equal(2f, Get("importerUrgentFeeMultiplier"));
        Assert.Equal(0.1f, Get("exportMultiplier"));
        Assert.Equal(0.5f, Get("sellingMultiplier"));
        Assert.All(SeriesSettings.NewGame.Where(v => v.Value is bool), v => Assert.False((bool)v.Value));
    }

    static readonly SeriesOption Wanted = new SeriesOption("RealLifeOverhaul", "Real Life Overhaul", "crime_frequency", OptionKind.Slider, 1, "test");

    [Fact]
    public void PickFindsTheOwnersOptionByIdNotByModId()
    {
        var found = SeriesSettings.Pick(Wanted, new[]
        {
            new FoundOption("OtherMod", "1234", "crime_frequency", OptionKind.Slider, 1, 10),
            new FoundOption("RealLifeOverhaul", "3816536560", "crime_frequency", OptionKind.Slider, 1, 10)
        }, out var reason);
        Assert.NotNull(found);
        Assert.Null(reason);
        Assert.Equal("3816536560", found!.ModId);
    }

    [Theory]
    [InlineData("Missing", "RealLifeOverhaul", "other", OptionKind.Slider, 1, 10, "isn't loaded")]
    [InlineData("RealLifeOverhaul", "RealLifeOverhaul", "other", OptionKind.Slider, 1, 10, "no option")]
    [InlineData("RealLifeOverhaul", "RealLifeOverhaul", "crime_frequency", OptionKind.Toggle, 0, 1, "is a toggle")]
    [InlineData("RealLifeOverhaul", "RealLifeOverhaul", "crime_frequency", OptionKind.Slider, 2, 10, "takes 2 to 10")]
    public void PickSkipsWithAReason(string skip, string owner, string id, OptionKind kind, int min, int max, string because)
    {
        var registered = skip == "Missing" ? new FoundOption[0] : new[] { new FoundOption(owner, "x", id, kind, min, max) };
        Assert.Null(SeriesSettings.Pick(Wanted, registered, out var reason));
        Assert.Contains(because, reason);
    }

    [Fact]
    public void PickRefusesAnIdTheOwnerRegistersTwice()
    {
        var twice = new[] { new FoundOption("RealLifeOverhaul", "a", "crime_frequency", OptionKind.Slider, 1, 10), new FoundOption("RealLifeOverhaul", "b", "crime_frequency", OptionKind.Slider, 1, 10) };
        Assert.Null(SeriesSettings.Pick(Wanted, twice, out var reason));
        Assert.Contains("2 times", reason);
    }

    [Fact]
    public void PrefKeyIsTheGamesFormat()
    {
        Assert.Equal("m:Dynamic Rivals:event_frequency", SeriesSettings.PrefKey("Dynamic Rivals", "event_frequency"));
        Assert.Null(SeriesSettings.PrefKey("", "x"));
    }
}
