using HardModePatch.Core;
using Xunit;

public class FingerprintTests
{
    [Fact]
    public void SameInstructionsGiveTheSameSixteenDigitFingerprint()
    {
        var a = Fingerprint.Of(new[] { "ldarg.0", "call System.Math::Max(System.Int32,System.Int32)", "ret" });
        var b = Fingerprint.Of(new[] { "ldarg.0", "call System.Math::Max(System.Int32,System.Int32)", "ret" });
        Assert.Equal(a, b);
        Assert.Matches("^[0-9a-f]{16}$", a);
    }

    [Fact]
    public void AnyChangedInstructionChangesIt()
    {
        var a = Fingerprint.Of(new[] { "ldc.r4 551.37", "stfld GameInstance::Money", "ret" });
        var b = Fingerprint.Of(new[] { "ldc.r4 551.37", "stfld GameInstance::Money", "pop", "ret" });
        Assert.NotEqual(a, b);
    }
}

public class GeneratedOrdinalTests
{
    [Theory]
    [InlineData("newobj EmpireBank.Core.EmpireBankRuntime+<>c__DisplayClass71_0::.ctor()", "newobj EmpireBank.Core.EmpireBankRuntime+<>c__DisplayClass#::.ctor()")]
    [InlineData("ldftn EmpireBank.Core.EmpireBankRuntime::<ApplySaveData>b__299_10()", "ldftn EmpireBank.Core.EmpireBankRuntime::<ApplySaveData>b__#()")]
    [InlineData("ldsfld EmpireBank.Core.EmpireBankRuntime+<>c::<>9__299_10", "ldsfld EmpireBank.Core.EmpireBankRuntime+<>c::<>9__#")]
    [InlineData("newobj EmpireTelemetry.EmpireTelemetryClient+<FetchStats>d__24::.ctor(System.Int32)", "newobj EmpireTelemetry.EmpireTelemetryClient+<FetchStats>d__#::.ctor(System.Int32)")]
    [InlineData("call X::<Draw>g__Place|12_0()", "call X::<Draw>g__Place|#()")]
    [InlineData("ldfld X+<Run>d__3::<i>5__2", "ldfld X+<Run>d__#::<i>5__#")]
    public void CompilerOrdinalsAreMasked(string input, string expected)
    {
        Assert.Equal(expected, Fingerprint.WithoutGeneratedOrdinals(input));
    }

    [Theory]
    [InlineData("ldc.r4 551.37")]
    [InlineData("ldc.i4 158")]
    [InlineData("ldstr \"ba:investmentfund_laceglobala\"")]
    [InlineData("call System.Math::Max(System.Int32,System.Int32)")]
    [InlineData("ldfld X::<>4__this")]
    public void OrdinaryOperandsAreKept(string input)
    {
        Assert.Equal(input, Fingerprint.WithoutGeneratedOrdinals(input));
    }
}
