using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class TranslationPresentationGateTests
{
    [Fact]
    public void OverlayCannotBeShownUntilModeIsActive()
    {
        var overlay = new RecordingOverlay();
        var gate = new TranslationPresentationGate(overlay, () => { });

        Assert.False(gate.IsActive);
        Assert.False(gate.TryShowOverlay());
        Assert.Equal(0, overlay.ShowCount);
    }

    [Fact]
    public void TurningOffCancelsHidesAndPermanentlyBlocksReopenUntilReactivated()
    {
        var overlay = new RecordingOverlay();
        var canceled = 0;
        var gate = new TranslationPresentationGate(overlay, () => canceled++);
        gate.Activate();
        Assert.True(gate.TryShowOverlay());
        Assert.Equal(1, overlay.ShowCount);

        gate.TurnOff();

        Assert.False(gate.IsActive);
        Assert.Equal(1, canceled);
        Assert.Equal(1, overlay.HideCount);
        Assert.False(gate.TryShowOverlay());
        Assert.Equal(1, overlay.ShowCount);
    }

    private sealed class RecordingOverlay : ITranslationOverlayVisibility
    {
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public void Show() => ShowCount++;
        public void Hide() => HideCount++;
    }
}
