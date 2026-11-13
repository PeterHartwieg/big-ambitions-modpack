using System;
using System.Linq;
using HardModePatch.Core;
using Xunit;

public class RloLineTests
{
    static readonly string[] Reviewed = Targets.RealLifeOverhaul.VerifiedHashes;

    [Fact]
    public void AnyRlo10ReleaseIsAcceptedByVersion()
    {
        Assert.True(RloLine.Accepts("0c4b4aea5c339f91", Reviewed, new Version(1, 0, 5, 0)));
        Assert.True(RloLine.Accepts("1234567890abcdef", Reviewed, new Version(1, 0, 6, 0)));
        Assert.True(RloLine.Accepts("1234567890abcdef", Reviewed, new Version(1, 0, 12, 0)));
        Assert.True(RloLine.Accepts("1234567890abcdef", Reviewed, new Version(1, 0, 0, 0)));
    }

    [Fact]
    public void UnknownBinariesOutsideTheLineOrClaimingTheReviewedVersionAreRefused()
    {
        Assert.False(RloLine.Accepts("bcfcc56ddf18554a", Reviewed, new Version(1, 0, 5, 0)));
        Assert.False(RloLine.Accepts(null, Reviewed, new Version(1, 0, 6, 0)));
        Assert.False(RloLine.Accepts("1234567890abcdef", Reviewed, null));
        Assert.False(RloLine.Accepts("1234567890abcdef", Reviewed, new Version(1, 1, 0, 0)));
        Assert.False(RloLine.Accepts("1234567890abcdef", Reviewed, new Version(2, 0, 0, 0)));
        Assert.False(RloLine.Accepts("1234567890abcdef", Reviewed, new Version(0, 50, 0, 0)));
        Assert.False(RloLine.Accepts("1234567890abcdef", Reviewed, new Version(0, 51, 0, 0)));
        Assert.True(RloLine.Accepts("f339319c9ec1ad65", Reviewed, new Version(0, 50, 0, 0)));
        Assert.True(RloLine.Accepts("1f7647f2e188a43e", Reviewed, new Version(0, 50, 0, 0)));
        Assert.True(RloLine.Accepts("2f360a365cf7915f", Reviewed, new Version(0, 45, 0, 0)));
        Assert.True(RloLine.Accepts("4f82b4f88f919178", Reviewed, new Version(0, 49, 0, 0)));
    }

    static FoundOption Rlo(string id) { return new FoundOption(SeriesSettings.Rlo, "RealLifeOverhaul", id, OptionKind.Toggle, 0, 1); }

    [Fact]
    public void OnRlo10TheButtonWritesTheOffenceTogglesAndLeavesDiplomasAlone()
    {
        var options = RloLine.SeriesOptions(new[] { Rlo("offence_red_light"), Rlo("living_population") }, true);
        Assert.DoesNotContain(options, o => o.Id == Rules.FactoryDiplomas.Id);
        Assert.DoesNotContain(options, o => o.Id == "player_offences");
        Assert.Equal(RloLine.Offences, options.Where(o => o.Id.StartsWith("offence_")).Select(o => o.Id));
        Assert.All(options.Where(o => o.Id.StartsWith("offence_")), o => Assert.True(o.Owner == SeriesSettings.Rlo && o.Kind == OptionKind.Toggle && o.Value == 0));
        Assert.Equal(8, RloLine.Offences.Length);
        Assert.DoesNotContain(options, o => o.Id == "living_population" || o.Id == "ui_interface" || o.Id == "start_debt");
        Assert.Equal(SeriesSettings.Options.Length - 2 + 8, options.Count);
    }

    [Fact]
    public void On050TheButtonIsUnchanged()
    {
        var options = RloLine.SeriesOptions(new[] { Rlo("player_offences") }, false);
        Assert.Equal(SeriesSettings.Options, options);
        Assert.Contains(options, o => o.Owner == SeriesSettings.Self && o.Id == Rules.FactoryDiplomas.Id && o.Value == 1);
        Assert.Contains(options, o => o.Id == "player_offences" && o.Value == 0);
    }

    [Fact]
    public void TheLivingEconomyReadersLeaveOutThePlayersOwnRent()
    {
        var readers = RloLine.LivingRentReaders.Select(r => r[0] + "." + r[1]).ToList();
        Assert.Equal(5, readers.Count);
        Assert.Contains("RealLifeOverhaul.Population.PopulationGame.Rent", readers);
        Assert.Contains("RealLifeOverhaul.Roommates.RoommateGame.RentPerDay", readers);
        Assert.DoesNotContain(readers, r => r.Contains("EstateGame") || r.Contains("LifeGame"));
        Assert.True(RloLine.AllLivingReadersNeeded(new Version(1, 0, 6)));
        Assert.False(RloLine.AllLivingReadersNeeded(new Version(0, 50, 0)));
    }

    [Fact]
    public void OffOnRlo1TextsAreClear()
    {
        Assert.Contains("off on Real Life Overhaul 1.0", RloLine.DiplomasOff);
        Assert.Contains("off on Real Life Overhaul 1.0", RloLine.SeriesDiplomasOff);
        Assert.DoesNotContain("—", RloLine.DiplomasOff + RloLine.SeriesDiplomasOff + RloLine.Accepted);
    }
}
