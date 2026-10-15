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
    public void TranslationCannotBePresentedWhenModeIsOff()
    {
        var overlay = new RecordingOverlay();
        var gate = new TranslationPresentationGate(overlay, () => { });

        Assert.False(gate.TryShowTranslation("late translation"));
        Assert.Empty(overlay.Translations);
    }

    [Fact]
    public async Task TurningOffDuringPresentationFinishesAfterShowAndLeavesOverlayHidden()
    {
        var overlay = new BlockingOverlay();
        var cancellationSignaled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TranslationPresentationGate(overlay, () => cancellationSignaled.TrySetResult());
        gate.Activate();

        var show = Task.Run(() => gate.TryShowTranslation("translation"));
        await overlay.ShowEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var turnOff = Task.Run(gate.TurnOff);
        var offPassedPresentation = await Task.WhenAny(cancellationSignaled.Task, Task.Delay(100)) == cancellationSignaled.Task;
        overlay.AllowShow.TrySetResult();

        Assert.True(await show);
        await turnOff;
        Assert.False(offPassedPresentation);
        Assert.False(gate.IsActive);
        Assert.Equal(1, overlay.ShowCount);
        Assert.Equal(1, overlay.HideCount);
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
        public List<string> Translations { get; } = [];
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public void Show() => ShowCount++;
        public bool TryShowTranslation(string translation, Func<bool> isStillAllowed)
        {
            if (!isStillAllowed()) return false;
            Translations.Add(translation);
            ShowCount++;
            return true;
        }
        public void Hide() => HideCount++;
    }

    private sealed class BlockingOverlay : ITranslationOverlayVisibility
    {
        public TaskCompletionSource ShowEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowShow { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public void Show() => ShowCount++;
        public bool TryShowTranslation(string translation, Func<bool> isStillAllowed)
        {
            ShowEntered.TrySetResult();
            AllowShow.Task.GetAwaiter().GetResult();
            if (!isStillAllowed()) return false;
            ShowCount++;
            return true;
        }
        public void Hide() => HideCount++;
    }
}
