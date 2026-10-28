using System.Linq;
using HardModePatch.Core;
using HARTModPack.Core;
using Xunit;

public class NightChirpingTests
{
    sealed class Source { public bool Muted; public float Volume = .09f; }

    [Fact]
    public void Default_and_series_preset_mute_only_the_pack_option()
    {
        Assert.True(Rules.MuteRloNightChirping.Default);
        Assert.Contains(SeriesSettings.Options, o => o.Owner == SeriesSettings.Self && o.Id == Rules.MuteRloNightChirping.Id && o.Value == 1);
        Assert.DoesNotContain(SeriesSettings.Options, o => o.Owner == SeriesSettings.Rlo && o.Id == "ambient_night");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Off_restores_prior_mute_without_undoing_Rlo_volume_changes(bool alreadyMuted)
    {
        var cricket = new Source { Muted = alreadyMuted };
        var birds = new Source();
        var mute = new SoundMute<Source>(s => s.Muted, (s, value) => s.Muted = value);
        mute.Apply(cricket, true);
        Assert.True(cricket.Muted);
        Assert.False(birds.Muted);
        cricket.Volume = .036f; // RLO continues its fades / cabin attenuation while muted.
        mute.Apply(cricket, true); // Repeated ticks must not capture our own mute as the original.
        mute.Apply(cricket, false);
        Assert.Equal(alreadyMuted, cricket.Muted);
        Assert.Equal(.036f, cricket.Volume);
        mute.Release();
        Assert.Equal(alreadyMuted, cricket.Muted);
    }

    [Fact]
    public void Replacement_missing_source_and_stop_release_only_the_owned_mute()
    {
        var first = new Source(); var next = new Source();
        var mute = new SoundMute<Source>(s => s.Muted, (s, value) => s.Muted = value);
        mute.Apply(first, true);
        mute.Apply(next, true);
        Assert.False(first.Muted); Assert.True(next.Muted);
        mute.Apply(null!, true);
        Assert.False(next.Muted);
        mute.Apply(next, true);
        mute.Release();
        Assert.False(next.Muted);
    }

    [Fact]
    public void Reviewed_companion_candidate_is_exact_and_does_not_approve_previous_candidates()
    {
        const string hash = "1f7647f2e188a43e43bc3411d2945a04eb68b70f5796e913dd9ae6807b0eba5c";
        Assert.True(TargetGate.Reviewed(TargetGate.HashPrefix(hash), Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("1f7647f2e188a43f", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("e992a80637fd664e", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("ff30cc6a2801f2b3", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("14aa86b20dd354bc", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("43c4eeed466c7149", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("790e707f3a6fcf69", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("ecbdfc92195401e1", Targets.RealLifeOverhaul.VerifiedHashes));
    }

    [Fact]
    public void Reviewed_050_guard_and_member_status_accept_the_official_binary()
    {
        const string hash = "F339319C9EC1AD65D4B3FA028D673F48E83FD075DFF18606F91A662FBAE54F5D";
        Assert.True(TargetGate.Reviewed(TargetGate.HashPrefix(hash), Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.True(TargetGate.Reviewed("4f82b4f88f919178", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("f339319c9ec1ad66", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.Equal(VersionVerdict.Tested, TestedVersions.Check("RealLifeOverhaul", TestedVersions.Exposed("0.50.0+850e278", null!)));
    }
}
