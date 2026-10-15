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
        var glossary = new GlossaryProtector([
            new GlossaryEntry("Rover", ["rover"], "character"),
            new GlossaryEntry("Academy", ["the academy"], "organization")
        ]);
        var translator = new DeterministicTranslator();
        var coordinator = new TranslationCoordinator(translator);
        var overlay = new RecordingOverlay();
        var gate = new TranslationPresentationGate(overlay, coordinator.CancelPending);
        var pipeline = new StoryTranslationPipeline(new TextStabilityFilter(), glossary, coordinator, gate);
        pipeline.Enable();
        worker.TextRecognized += async (_, recognized) => await pipeline.ObserveAsync(recognized);

        worker.Start(new Rectangle(10, 10, 300, 80));
        await WaitUntilAsync(() => overlay.ShowCount >= 1);
        worker.Stop();
        await worker.DisposeAsync();

        Assert.Equal("Dịch: Tôi đã gặp Rover gần Academy.", overlay.LastText);
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
        var pipeline = new StoryTranslationPipeline(new TextStabilityFilter(), new GlossaryProtector([]), coordinator, gate);
        pipeline.Enable();

        Assert.False(await pipeline.ObserveAsync("Hello."));
        Assert.True(await pipeline.ObserveAsync("Hello."));
        Assert.False(await pipeline.ObserveAsync("Hello."));
        Assert.False(await pipeline.ObserveAsync("A different line."));
        Assert.True(await pipeline.ObserveAsync("A different line."));
        Assert.False(await pipeline.ObserveAsync("Hello."));
        Assert.True(await pipeline.ObserveAsync("Hello."));
        Assert.Equal(2, translator.CallCount);

        pipeline.TurnOff();
        Assert.Equal(1, overlay.HideCount);
        Assert.False(await pipeline.ObserveAsync("A late line."));
        Assert.False(gate.TryShowTranslation("late translation"));
        Assert.Equal(3, overlay.ShowCount);
    }

    private sealed class SyntheticFrameSource(params string[] recognizedLines) : IRegionFrameSource
    {
        private readonly Queue<string> _lines = new(recognizedLines);
        public int FramesCreated { get; private set; }
        public int FramesDisposed { get; private set; }

        public async Task<ICapturedFrame> CaptureAsync(Rectangle region, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_lines.Count == 0)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new OperationCanceledException(cancellationToken);
            }

            FramesCreated++;
            return new SyntheticFrame(
                _lines.Count > 0 ? _lines.Dequeue() : string.Empty,
                () => FramesDisposed++);
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
        public bool TryShowTranslation(string translation, Func<bool> isStillAllowed)
        {
            if (!isStillAllowed()) return false;
            LastText = translation;
            ShowCount++;
            return true;
        }
        public void Hide() => HideCount++;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout) throw new TimeoutException("Expected synthetic pipeline result was not presented.");
            await Task.Delay(5);
        }
    }
}
