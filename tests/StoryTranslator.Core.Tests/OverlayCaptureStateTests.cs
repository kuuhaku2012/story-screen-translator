using StoryTranslator.App;

namespace StoryTranslator.Core.Tests;

public sealed class OverlayCaptureStateTests
{
    [Fact]
    public void TranslationIsHiddenOnlyWhileCapturing()
    {
        var state = new OverlayCaptureState();
        state.RequestShow();
        Assert.True(state.ShouldShow);
        state.BeginCapture();
        Assert.False(state.ShouldShow);
        state.EndCapture();
        Assert.True(state.ShouldShow);
    }

    [Fact]
    public void NewResultDuringCaptureWaitsUntilCaptureEnds()
    {
        var state = new OverlayCaptureState();
        state.BeginCapture();
        state.RequestShow();
        Assert.False(state.ShouldShow);
        state.EndCapture();
        Assert.True(state.ShouldShow);
    }

    [Fact]
    public void OffDuringCaptureCannotRestoreOldTranslation()
    {
        var state = new OverlayCaptureState();
        state.RequestShow();
        state.BeginCapture();
        state.Hide();
        state.EndCapture();
        Assert.False(state.ShouldShow);
    }

    [Fact]
    public void StaleLineIsNotRestoredAfterCapture()
    {
        var allowed = true;
        var state = new OverlayCaptureState();
        state.RequestShow(() => allowed);
        state.BeginCapture();
        allowed = false;
        state.EndCapture();
        Assert.False(state.ShouldShow);
    }
}
