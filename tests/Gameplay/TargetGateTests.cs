using System.Linq;
using HardModePatch.Core;
using Xunit;

public class TargetGateTests
{
    static readonly string[] Verified = { "62ba60cb66dc226c" };

    [Fact]
    public void MissingModIsMissingWhateverElse()
    {
        Assert.Equal(TargetState.Missing, TargetGate.Classify(false, "62ba60cb66dc226c", Verified, 3, 3));
    }

    [Fact]
    public void ReviewedHashWithAllHooksIsVerified()
    {
        Assert.Equal(TargetState.Verified, TargetGate.Classify(true, "62BA60CB66DC226C", Verified, 3, 3));
    }

    [Fact]
    public void NewHashWithAllHooksIsUnverified()
    {
        Assert.Equal(TargetState.Unverified, TargetGate.Classify(true, "0000000000000000", Verified, 3, 3));
    }

    [Fact]
    public void UnreadableFileFallsBackToTheHooks()
    {
        Assert.Equal(TargetState.Unverified, TargetGate.Classify(true, null, Verified, 3, 3));
    }

    [Fact]
    public void AMissingHookBeatsAReviewedHash()
    {
        Assert.Equal(TargetState.Incompatible, TargetGate.Classify(true, "62ba60cb66dc226c", Verified, 3, 2));
    }

    [Fact]
    public void ATargetWithNoHooksIsNeverPatched()
    {
        Assert.Equal(TargetState.Incompatible, TargetGate.Classify(true, "62ba60cb66dc226c", Verified, 0, 0));
    }

    [Fact]
    public void HashPrefixIsTheFirstSixteenLowercaseDigits()
    {
        Assert.Equal("62ba60cb66dc226c", TargetGate.HashPrefix("62BA60CB66DC226C0123456789ABCDEF"));
        Assert.Null(TargetGate.HashPrefix("62ba"));
        Assert.Null(TargetGate.HashPrefix(null));
    }

    [Fact]
    public void TargetsHaveUniqueKeysAssembliesAndWellFormedHashes()
    {
        Assert.Equal(Targets.All.Count, Targets.All.Select(t => t.Key).Distinct().Count());
        Assert.Equal(Targets.All.Count, Targets.All.Select(t => t.AssemblyName).Distinct().Count());
        foreach (var target in Targets.All)
        {
            Assert.NotEmpty(target.VerifiedHashes);
            Assert.All(target.VerifiedHashes, h => Assert.Matches("^[0-9a-f]{16}$", h));
        }
    }
}
