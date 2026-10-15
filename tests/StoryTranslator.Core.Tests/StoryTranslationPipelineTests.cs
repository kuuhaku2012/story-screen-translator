using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class StoryTranslationPipelineTests
{
    [Fact]
    public async Task RepeatedDialogueAfterAnotherLineReusesTranslationAndRestoresNames()
    {
        var translator = new RecordingTranslator();
        var coordinator = new TranslationCoordinator(translator);
        var overlay = new RecordingOverlay();
        var gate = new TranslationPresentationGate(overlay, coordinator.CancelPending);
        var pipeline = new StoryTranslationPipeline(
            new TextStabilityFilter(),
            new GlossaryProtector([new GlossaryEntry("Rover", ["rover"], "character")]),
            coordinator,
            gate);
        pipeline.Enable();

        Assert.False(await pipeline.ObserveAsync("I met rover."));
        Assert.True(await pipeline.ObserveAsync("I met rover."));
        Assert.False(await pipeline.ObserveAsync("We left."));
        Assert.True(await pipeline.ObserveAsync("We left."));
        Assert.False(await pipeline.ObserveAsync("I met rover."));
        Assert.True(await pipeline.ObserveAsync("I met rover."));

        Assert.True(translator.CallCount == 2, string.Join(" | ", translator.Sources));
        Assert.Equal(new[] { "VI Met Rover.", "VI We left.", "VI Met Rover." }, overlay.Translations);
        Assert.Equal(3, overlay.ShowCount);
    }

    [Fact]
    public async Task TranslatorFailureDoesNotOpenPresentationGate()
    {
        var translator = new RecordingTranslator(fail: true);
        var coordinator = new TranslationCoordinator(translator);
        var overlay = new RecordingOverlay();
        var gate = new TranslationPresentationGate(overlay, coordinator.CancelPending);
        var pipeline = new StoryTranslationPipeline(
            new TextStabilityFilter(),
            new GlossaryProtector([]),
            coordinator,
            gate);
        pipeline.Enable();

        Assert.False(await pipeline.ObserveAsync("A line."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.ObserveAsync("A line."));

        Assert.True(gate.IsActive);
        Assert.Empty(overlay.Translations);
        Assert.Equal(0, overlay.ShowCount);
    }

    private sealed class RecordingTranslator(bool fail = false) : ITextTranslator
    {
        public int CallCount { get; private set; }
        public List<string> Sources { get; } = [];
        public Task<string> TranslateAsync(string source, string target, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Sources.Add(source);
            if (fail) return Task.FromException<string>(new InvalidOperationException("translator failed"));
            var translated = source.Replace("I met ", "Met ", StringComparison.Ordinal);
            return Task.FromResult("VI " + translated);
        }
    }

    private sealed class RecordingOverlay : ITranslationOverlayVisibility
    {
        public List<string> Translations { get; } = [];
        public int ShowCount { get; private set; }
        public bool TryShowTranslation(string translation, Func<bool> isStillAllowed)
        {
            if (!isStillAllowed()) return false;
            ShowCount++;
            Translations.Add(translation);
            return true;
        }
        public void Show() => ShowCount++;
        public void Hide() { }
    }
}
