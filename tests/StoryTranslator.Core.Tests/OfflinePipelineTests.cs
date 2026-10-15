using System.Drawing;
using StoryTranslator.App;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class OfflinePipelineTests
{
    [Fact]
    public async Task SyntheticFrames_FlowThroughStabilityGlossaryTranslatorAndPresentationGate()
    {
        var frameSource = new SyntheticFrameSource("I met rover near the academy.", "I met rover near the academy.");
        var recognizer = new SyntheticRecognizer();
        var worker = new CaptureWorker(frameSource, recognizer, TimeSpan.FromMilliseconds(5));
        var stability = new TextStabilityFilter();
        var glossary = new GlossaryProtector([
            new GlossaryEntry("Rover", ["rover"], "character"),
            new GlossaryEntry("Academy", ["the academy"], "organization")
        ]);
        var translator = new DeterministicTranslator();
        var coordinator = new TranslationCoordinator(translator);
        var overlay = new RecordingOverlay();
        var gate = new TranslationPresentationGate(overlay, coordinator.CancelPending);
        gate.Activate();

        var presentation = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        worker.TextRecognized += async (_, recognized) =>
        {
            var stable = stability.Observe(recognized);
            if (stable is null) return;

            var protectedText = glossary.Protect(stable);
            try
            {
                var translated = await coordinator.SubmitAsync(protectedText.Text, "vi");
                var restored = GlossaryProtector.Restore(translated, protectedText.TokenMap);
                if (gate.TryShowOverlay())
                {
                    overlay.LastText = restored;
                    presentation.TrySetResult(restored);
                }
            }
            catch (InvalidOperationException ex)
            {
                presentation.TrySetException(ex);
            }
        };

        worker.Start(new Rectangle(10, 10, 300, 80));
        var shown = await presentation.Task.WaitAsync(TimeSpan.FromSeconds(3));
        worker.Stop();
        await worker.DisposeAsync();

        Assert.Equal("Dịch: Tôi đã gặp Rover gần Academy.", shown);
        Assert.Equal(1, translator.CallCount);
        Assert.Equal(2, recognizer.CallCount);
        Assert.Equal(2, frameSource.FramesCreated);
        Assert.Equal(2, frameSource.FramesDisposed);
        Assert.Equal(1, overlay.ShowCount);
    }

    [Fact]
    public async Task CachedDuplicateDoesNotCallTranslatorAgain_AndOffHidesAndBlocksPresentation()
    {
        var translator = new DeterministicTranslator();
        var cache = new InMemoryTranslationCache();
        var coordinator = new TranslationCoordinator(translator, cache);
        var overlay = new RecordingOverlay();
        var gate = new TranslationPresentationGate(overlay, coordinator.CancelPending);
        gate.Activate();

        Assert.Equal("Dịch: Hello.", await coordinator.SubmitAsync("Hello.", "vi"));
        Assert.Equal("Dịch: Hello.", await coordinator.SubmitAsync("Hello.", "vi"));
        Assert.Equal(1, translator.CallCount);

        gate.TurnOff();
        Assert.Equal(1, overlay.HideCount);
        Assert.False(gate.TryShowOverlay());
        Assert.Equal(0, overlay.ShowCount);
    }

    [Fact]
    public async Task TranslatorFailureIsPropagatedWithoutOpeningOverlay()
    {
        var translator = new DeterministicTranslator(fail: true);
        var coordinator = new TranslationCoordinator(translator);
        var overlay = new RecordingOverlay();
        var gate = new TranslationPresentationGate(overlay, coordinator.CancelPending);
        gate.Activate();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.SubmitAsync("A line", "vi"));

        Assert.Equal("Synthetic translator failure", error.Message);
        Assert.Equal(1, translator.CallCount);
        Assert.Equal(0, overlay.ShowCount);
    }

    private sealed class SyntheticFrameSource(params string[] recognizedLines) : IRegionFrameSource
    {
        private readonly Queue<string> _lines = new(recognizedLines);
        public int FramesCreated { get; private set; }
        public int FramesDisposed { get; private set; }

        public Task<ICapturedFrame> CaptureAsync(Rectangle region, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FramesCreated++;
            return Task.FromResult<ICapturedFrame>(new SyntheticFrame(
                _lines.Count > 0 ? _lines.Dequeue() : string.Empty,
                () => FramesDisposed++));
        }
    }

    private sealed class SyntheticFrame(string text, Action onDispose) : ICapturedFrame
    {
        public string Text { get; } = text;
        public void Dispose() => onDispose();
    }

    private sealed class SyntheticRecognizer : IFrameTextRecognizer
    {
        public int CallCount { get; private set; }
        public Task<string> RecognizeAsync(ICapturedFrame frame, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(((SyntheticFrame)frame).Text);
        }
    }

    private sealed class DeterministicTranslator(bool fail = false) : ITextTranslator
    {
        public int CallCount { get; private set; }
        public Task<string> TranslateAsync(string source, string target, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (fail) return Task.FromException<string>(new InvalidOperationException("Synthetic translator failure"));

            var output = source
                .Replace("I met ", "Tôi đã gặp ", StringComparison.Ordinal)
                .Replace(" near ", " gần ", StringComparison.Ordinal);
            return Task.FromResult("Dịch: " + output);
        }
    }

    private sealed class RecordingOverlay : ITranslationOverlayVisibility
    {
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public string? LastText { get; set; }
        public void Show() => ShowCount++;
        public void Hide() => HideCount++;
    }
}
