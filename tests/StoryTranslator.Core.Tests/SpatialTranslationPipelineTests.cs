using System.Drawing;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class SpatialTranslationPipelineTests
{
    private static ScreenTextBlock Block(string text, float x = 10, float y = 10) => new(text, new RectangleF(x, y, 200, 24), 20);
    private static GlossaryProtector Glossary() => new(Array.Empty<GlossaryEntry>());

    [Fact]
    public async Task InvalidSecondResultCannotCacheOrPublishAnyPartOfBatch()
    {
        var translator = new Batch { Run = (_, _) => Task.FromResult<IReadOnlyList<string>>(
            ["Xin chào", "Kết quả có tên thừa ⟬STG_0002⟭"]) };
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        Exception? error = null;
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(),
            (items, allowed) => { if (allowed()) shown = items; }, e => error = e);
        var blocks = new[] { Block("Hello"), Block("I know", 500) };
        pipeline.Enable();
        await pipeline.ObserveAsync(blocks);
        await pipeline.ObserveAsync(blocks);
        Assert.IsType<InvalidOperationException>(error);
        Assert.Empty(shown);
        translator.Run = null;
        pipeline.Enable();
        await pipeline.ObserveAsync(blocks);
        await pipeline.ObserveAsync(blocks);
        Assert.Equal(2, translator.Requests.Count);
        Assert.Equal(2, translator.Requests[1].Length);
        Assert.Equal(2, shown.Count);
    }

    [Fact]
    public async Task StableBlockTranslatesEvenWhenUnrelatedClockChanges()
    {
        var translator = new Batch();
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(), (items, allowed) => { if (allowed()) shown = items; });
        pipeline.Enable();
        await pipeline.ObserveAsync([Block("Hello"), Block("12:01", 400)]);
        await pipeline.ObserveAsync([Block("Hello"), Block("12:02", 400)]);
        Assert.Single(translator.Requests);
        Assert.Equal(new[] { "Hello" }, translator.Requests[0]);
        Assert.Single(shown);
        Assert.Equal(10, shown[0].Bounds.X);
    }

    [Fact]
    public async Task MultipleStableBlocksAreBatchedAndRetainOwnCoordinates()
    {
        var translator = new Batch();
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(), (items, allowed) => { if (allowed()) shown = items; });
        pipeline.Enable();
        var blocks = new[] { Block("Left"), Block("Right", 500) };
        await pipeline.ObserveAsync(blocks);
        await pipeline.ObserveAsync(blocks);
        Assert.Single(translator.Requests);
        Assert.Equal(2, shown.Count);
        Assert.Equal(500, shown[1].Bounds.X);
        Assert.Equal("VI Right", shown[1].Text);
    }

    [Fact]
    public async Task RemovedBlocksCannotReappearWhenSlowRequestCompletes()
    {
        var release = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var translator = new Batch { Run = (_, _) => { started.TrySetResult(); return release.Task; } };
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(), (items, allowed) => { if (allowed()) shown = items; });
        pipeline.Enable();
        await pipeline.ObserveAsync([Block("Old")]);
        var pending = pipeline.ObserveAsync([Block("Old")]);
        await started.Task;
        _ = pipeline.ObserveAsync([]);
        release.SetResult(new[] { "Late result" });
        await pending;
        Assert.Empty(shown);
    }

    [Fact]
    public async Task OffInvalidatesCallbacksAlreadyQueuedByPresenter()
    {
        var callbacks = new List<Func<bool>>();
        var pipeline = new SpatialTranslationPipeline(new Batch(), Glossary(), (_, allowed) => callbacks.Add(allowed));
        pipeline.Enable();
        await pipeline.ObserveAsync([Block("Hello")]);
        await pipeline.ObserveAsync([Block("Hello")]);
        pipeline.TurnOff();
        Assert.All(callbacks, callback => Assert.False(callback()));
    }

    [Fact]
    public async Task ReappearingTextAtNewPositionUsesCache()
    {
        var translator = new Batch();
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(), (items, allowed) => { if (allowed()) shown = items; });
        pipeline.Enable();
        await pipeline.ObserveAsync([Block("Hello")]);
        await pipeline.ObserveAsync([Block("Hello")]);
        await pipeline.ObserveAsync([]);
        await pipeline.ObserveAsync([Block("Hello", 500)]);
        await pipeline.ObserveAsync([Block("Hello", 500)]);
        Assert.Single(translator.Requests);
        Assert.Single(shown);
        Assert.Equal(500, shown[0].Bounds.X);
    }

    [Fact]
    public async Task CutsceneKeepsVisibleTextUntilShortNextLineFinishes()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var translator = new Batch { Run = (texts, _) =>
            texts[0].StartsWith("This", StringComparison.Ordinal)
                ? WaitForShortLine()
                : Task.FromResult<IReadOnlyList<string>>(["VI " + texts[0]]) };
        Task<IReadOnlyList<string>> WaitForShortLine() { started.TrySetResult(); return release.Task; }
        var views = new List<string>();
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(),
            (items, allowed) => { if (allowed()) views.Add(string.Join(" | ", items.Select(i => i.Text))); },
            cutsceneMode: true);
        pipeline.Enable();
        const string first = "A thousand times over, and I'd still see it through to the end";
        await pipeline.ObserveAsync([Block(first)]);
        await pipeline.ObserveAsync([Block("A thousand times over, and lid still see it through to the end")]);
        await pipeline.ObserveAsync([Block("A thousand tim over, and I'd still see it through to the end")]);
        await pipeline.ObserveAsync([Block("A thousand tim ek and I'd still see it through to the end")]);
        await pipeline.ObserveAsync([Block("A thousand times over, and I'd stilt see it through to the end")]);
        await pipeline.ObserveAsync([]);
        var pending = pipeline.ObserveAsync([Block("This is my cause")]);
        await started.Task;
        _ = pipeline.ObserveAsync([Block("is is my cause")]);
        _ = pipeline.ObserveAsync([]);
        Assert.Equal("VI " + first, views[^1]);
        release.SetResult(["Đây là lý do của tôi"]);
        await pending;
        Assert.Equal("Đây là lý do của tôi", views[^1]);
        Assert.DoesNotContain("", views);
    }

    [Fact]
    public async Task CutsceneKeepsLongSubtitleThroughSplitOcrError()
    {
        var translator = new Batch();
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(),
            (items, allowed) => { if (allowed()) shown = items; }, cutsceneMode: true);
        pipeline.Enable();
        const string original = "A thousand times over, and I'd still see it through to the end";
        await pipeline.ObserveAsync([Block(original)]);
        await pipeline.ObserveAsync([Block("A thousand tim ek and I'd still see it through to the end")]);
        Assert.Single(translator.Requests);
        Assert.Equal("VI " + original, Assert.Single(shown).Text);
    }

    [Fact]
    public async Task CutsceneSendsShortSentenceOnFirstObservation()
    {
        var translator = new Batch();
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(),
            (items, allowed) => { if (allowed()) shown = items; }, cutsceneMode: true);
        pipeline.Enable();
        await pipeline.ObserveAsync([Block("This is my cause")]);
        await pipeline.ObserveAsync([Block("is is my cause")]);
        Assert.Single(translator.Requests);
        Assert.Equal("VI This is my cause", Assert.Single(shown).Text);
    }

    [Fact]
    public async Task CutsceneTranslationCanArriveAfterBriefOcrGap()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var translator = new Batch { Run = (_, _) => { started.SetResult(); return release.Task; } };
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(),
            (items, allowed) => { if (allowed()) shown = items; }, cutsceneMode: true);
        pipeline.Enable();
        var pending = pipeline.ObserveAsync([Block("How they must've hated you...")]);
        await started.Task;
        _ = pipeline.ObserveAsync([]);
        _ = pipeline.ObserveAsync([]);
        release.SetResult(["Hẳn họ đã căm ghét cô..."]);
        await pending;
        Assert.Equal("Hẳn họ đã căm ghét cô...", Assert.Single(shown).Text);
    }

    [Fact]
    public async Task CutsceneSubtitleSurvivesTwoDroppedOcrCharacters()
    {
        var translator = new Batch();
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(),
            (items, allowed) => { if (allowed()) shown = items; }, cutsceneMode: true);
        pipeline.Enable();
        await pipeline.ObserveAsync([Block("Returning... all by your lonesome?")]);
        await pipeline.ObserveAsync([Block("Returning.. al by your lonesome?")]);
        Assert.Single(translator.Requests);
        Assert.Equal("Returning... all by your lonesome?", translator.Requests[0][0]);
        Assert.Equal("VI Returning... all by your lonesome?", Assert.Single(shown).Text);
    }

    [Fact]
    public async Task StableBatchRetriesAfterRateLimitCooldownAndKeepsCurrentText()
    {
        var translator = new CooldownBatch();
        IReadOnlyList<TranslatedScreenBlock> shown = [];
        var pipeline = new SpatialTranslationPipeline(translator, Glossary(),
            (items, allowed) => { if (allowed()) shown = items; });
        pipeline.Enable();

        await pipeline.ObserveAsync([Block("Hello")]);
        await pipeline.ObserveAsync([Block("Hello")]);

        Assert.Equal(2, translator.RequestCount);
        Assert.Equal(2, translator.WaitCount);
        Assert.Single(shown);
        Assert.Equal("Xin chào", shown[0].Text);
    }

    private sealed class Batch : IBatchTextTranslator
    {
        public List<string[]> Requests { get; } = [];
        public Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<string>>>? Run;
        public Task<IReadOnlyList<string>> TranslateBatchAsync(IReadOnlyList<string> texts, CancellationToken token)
        {
            Requests.Add(texts.ToArray());
            return Run?.Invoke(texts, token) ?? Task.FromResult<IReadOnlyList<string>>(texts.Select(t => "VI " + t).ToArray());
        }
    }

    private sealed class CooldownBatch : IBatchTextTranslator, IRateLimitedTextTranslator
    {
        public int RequestCount { get; private set; }
        public int WaitCount { get; private set; }
        public Task WaitUntilReadyAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            WaitCount++;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<string>> TranslateBatchAsync(IReadOnlyList<string> texts, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (++RequestCount == 1) throw new GroqRateLimitException("cooldown");
            return Task.FromResult<IReadOnlyList<string>>(texts.Select(_ => "Xin chào").ToArray());
        }
    }
}
