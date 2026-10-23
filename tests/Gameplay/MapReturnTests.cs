using HardModePatch.Core;
using Xunit;

public class MapReturnTests
{
    static bool Step(MapReturn state, bool open = false, bool engaged = false, float blend = 0f,
        bool enabled = true, bool on = true) => state.Advance(enabled, open, engaged, blend, on);
    static void Open(MapReturn state) { Step(state, open: true, engaged: true, blend: 1f, on: false); }

    [Fact]
    public void WaitsForCameraAndFadeThenActsOnlyOnce()
    {
        var state = new MapReturn();
        Open(state);
        Assert.False(Step(state, blend: 0.5f));
        Assert.False(Step(state)); // Fade finished before the camera.
        state.CameraReturned();
        Assert.True(Step(state));
        Assert.False(Step(state));
        state.CameraReturned();
        Assert.False(Step(state));
    }

    [Fact]
    public void CameraMayFinishBeforeFade()
    {
        var state = new MapReturn();
        Open(state);
        state.CameraReturned();
        Assert.False(Step(state, blend: 0.01f));
        Assert.True(Step(state));
    }

    [Fact]
    public void ReopeningNeedsANewCameraReturn()
    {
        var state = new MapReturn();
        Open(state);
        state.CameraReturned();
        Assert.False(Step(state, blend: 0.5f));
        Open(state);
        Assert.False(Step(state));
        state.CameraReturned();
        Assert.True(Step(state));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void DisablingEitherEnhancementOrFixCancelsPendingReturn(bool enabled, bool on)
    {
        var state = new MapReturn();
        Open(state);
        state.CameraReturned();
        Assert.False(Step(state, enabled: enabled, on: on));
        Assert.False(Step(state));
    }

    [Fact]
    public void NoReturnWithoutAnEngagedMapAndResetCancelsIt()
    {
        var state = new MapReturn();
        state.CameraReturned();
        Assert.False(Step(state));
        Step(state, open: true);
        state.CameraReturned();
        Assert.False(Step(state));
        Open(state);
        state.CameraReturned();
        state.Reset();
        Assert.False(Step(state));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidFadeCancelsReturn(float blend)
    {
        var state = new MapReturn();
        Open(state);
        state.CameraReturned();
        Assert.False(Step(state, blend: blend));
        Assert.False(Step(state));
    }
}
