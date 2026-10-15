using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class StaleTranslationPresentationTests
{
    [Fact]
    public async Task SlowACompletingAfterFastB_CannotOverwriteB()
    {
        var submitter = new DeferredSubmitter();
        var overlay = new DeferredOverlay();
        var gate = new TranslationPresentationGate(overlay, () => { });
        var pipeline = CreatePipeline(submitter, gate);
        pipeline.Enable();

        Assert.False(await pipeline.ObserveAsync("A"));
        var pendingA = pipeline.ObserveAsync("A");
        await submitter.WaitForCountAsync(1);

        Assert.False(await pipeline.ObserveAsync("B"));
        var pendingB = pipeline.ObserveAsync("B");
        await submitter.WaitForCountAsync(2);
        submitter.Complete("B", "translated B");
        Assert.True(await pendingB);
        overlay.FlushPending();
        Assert.Equal(new[] { "translated B" }, overlay.Displayed);

        submitter.Complete("A", "translated A");
        Assert.False(await pendingA);
        overlay.FlushPending();

        Assert.Equal(new[] { "translated B" }, overlay.Displayed);
    }

    [Fact]
    public async Task OldTranslationCompletingAfterOffOn_CannotReappearInNewSession()
    {
        var submitter = new DeferredSubmitter();
        var overlay = new DeferredOverlay();
        var gate = new TranslationPresentationGate(overlay, () => { });
        var pipeline = CreatePipeline(submitter, gate);
        pipeline.Enable();

        Assert.False(await pipeline.ObserveAsync("A"));
        var pendingOldSession = pipeline.ObserveAsync("A");
        await submitter.WaitForCountAsync(1);

        pipeline.TurnOff();
        pipeline.Enable();
        submitter.Complete("A", "old session A");
        Assert.False(await pendingOldSession);
        overlay.FlushPending();

        Assert.True(gate.IsActive);
        Assert.Empty(overlay.Displayed);
    }

    private static StoryTranslationPipeline CreatePipeline(DeferredSubmitter submitter, TranslationPresentationGate gate) =>
        new(new TextStabilityFilter(), new GlossaryProtector([]), submitter, gate);

    private sealed class DeferredSubmitter : ITranslationCoordinator
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, TaskCompletionSource<string>> _results = new(StringComparer.Ordinal);
        private int _count;

        public Task<string> SubmitAsync(string line, string target = "vi", CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _count++;
                var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _results.Add(line, result);
                return result.Task;
            }
        }

        public void CancelPending() { }

        public async Task WaitForCountAsync(int expected)
        {
            var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            while (true)
            {
                lock (_sync) if (_count >= expected) return;
                if (DateTime.UtcNow >= timeout) throw new TimeoutException($"Expected {expected} submitted lines.");
                await Task.Delay(5);
            }
        }

        public void Complete(string line, string translation)
        {
            lock (_sync) _results[line].TrySetResult(translation);
        }
    }

    private sealed class DeferredOverlay : ITranslationOverlayVisibility
    {
        private readonly List<(string Text, Func<bool> IsAllowed)> _pending = [];
        public List<string> Displayed { get; } = [];
        public void Show() { }
        public bool TryShowTranslation(string translation, Func<bool> isStillAllowed)
        {
            _pending.Add((translation, isStillAllowed));
            return true;
        }
        public void FlushPending()
        {
            foreach (var (text, isAllowed) in _pending)
            {
                if (isAllowed()) Displayed.Add(text);
            }
            _pending.Clear();
        }
        public void Hide() { }
    }
}
