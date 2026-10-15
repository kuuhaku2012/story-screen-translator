using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class TranslationCoordinatorTests
{
    [Fact]
    public async Task RepeatedInFlightInputIsCoalescedAndCompletedResultIsCached()
    {
        var translator = new ControlledTranslator();
        var coordinator = new TranslationCoordinator(translator);
        var first = coordinator.SubmitAsync("Hello", "vi");
        await translator.Started.Task;
        var second = coordinator.SubmitAsync("Hello", "vi");
        translator.Complete("Xin chào");

        Assert.Equal("Xin chào", await first);
        Assert.Equal("Xin chào", await second);
        Assert.Equal("Xin chào", await coordinator.SubmitAsync("Hello", "vi"));
        Assert.Equal(1, translator.CallCount);
    }

    [Fact]
    public async Task NewerDifferentInputCancelsOlderAndOnlyNewestCompletes()
    {
        var translator = new ControlledTranslator();
        var coordinator = new TranslationCoordinator(translator);
        var older = coordinator.SubmitAsync("Old", "vi");
        await translator.Started.Task;
        var newer = coordinator.SubmitAsync("New", "vi");
        await translator.WaitForCallCountAsync(2);

        Assert.True(translator.CancellationTokens[0].IsCancellationRequested);
        translator.Complete("Bản mới", callIndex: 1);
        Assert.Equal("Bản mới", await newer);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => older);
    }

    [Fact]
    public async Task CancelPendingCancelsCurrentTranslationAndPreventsStaleCacheWrite()
    {
        var translator = new CancellationIgnoringTranslator();
        var cache = new InMemoryTranslationCache(8);
        var coordinator = new TranslationCoordinator(translator, cache);
        var pending = coordinator.SubmitAsync("Source", "vi");
        await translator.Started.Task;

        coordinator.CancelPending();
        translator.Complete("Late result");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(cache.TryGet("Source", "vi", out _));
    }

    [Fact]
    public async Task NewInputDoesNotCancelMatchingInFlightRequest()
    {
        var translator = new ControlledTranslator();
        var coordinator = new TranslationCoordinator(translator);
        var current = coordinator.SubmitAsync("Same", "vi");
        await translator.Started.Task;
        var coalesced = coordinator.SubmitAsync("Same", "vi");

        Assert.False(translator.CancellationTokens[0].IsCancellationRequested);
        translator.Complete("Cùng câu");
        Assert.Equal("Cùng câu", await current);
        Assert.Equal("Cùng câu", await coalesced);
        Assert.Equal(1, translator.CallCount);
    }

    [Fact]
    public async Task CacheIsBoundedAndEvictsOldestEntry()
    {
        var cache = new InMemoryTranslationCache(2);
        cache.Set("one", "vi", "mot");
        cache.Set("two", "vi", "hai");
        cache.Set("three", "vi", "ba");

        Assert.False(cache.TryGet("one", "vi", out _));
        Assert.True(cache.TryGet("two", "vi", out var second));
        Assert.Equal("hai", second);
    }

    [Fact]
    public async Task ReplacedWorkIsCanceledEvenWhenCompletionWinsBeforeCancellationSignal()
    {
        using var allowCancellationSignal = new ManualResetEventSlim();
        var detached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var translator = new MultiCallCancellationIgnoringTranslator();
        var cache = new InMemoryTranslationCache();
        var coordinator = new TranslationCoordinator(translator, cache, () =>
        {
            detached.TrySetResult();
            allowCancellationSignal.Wait();
        });

        var stale = coordinator.SubmitAsync("Old source", "vi");
        await translator.WaitForCallCountAsync(1);
        var submitNew = Task.Run(() => coordinator.SubmitAsync("New source", "vi"));
        try
        {
            await detached.Task;
            translator.Complete(0, "Late old translation");
            await Task.WhenAny(stale, Task.Delay(TimeSpan.FromSeconds(3)));

            Assert.True(stale.IsCompleted);
            Assert.True(stale.IsCanceled);
            Assert.False(cache.TryGet("Old source", "vi", out _));
        }
        finally
        {
            allowCancellationSignal.Set();
        }

        var newest = submitNew;
        await translator.WaitForCallCountAsync(2);
        translator.Complete(1, "New translation");
        Assert.Equal("New translation", await newest);
    }
    private sealed class MultiCallCancellationIgnoringTranslator : ITextTranslator
    {
        private readonly object _gate = new();
        private readonly List<TaskCompletionSource<string>> _completions = [];
        public Task<string> TranslateAsync(string source, string target, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _completions.Add(completion);
                return completion.Task;
            }
        }
        public void Complete(int callIndex, string result)
        {
            lock (_gate) _completions[callIndex].TrySetResult(result);
        }
        public async Task WaitForCallCountAsync(int count)
        {
            var limit = DateTime.UtcNow.AddSeconds(3);
            while (true)
            {
                lock (_gate) if (_completions.Count >= count) return;
                if (DateTime.UtcNow >= limit) throw new TimeoutException($"Expected {count} translator calls.");
                await Task.Delay(10);
            }
        }
    }
    private sealed class ControlledTranslator : ITextTranslator
    {
        private readonly object _gate = new();
        private readonly List<TaskCompletionSource<string>> _completions = [];
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<CancellationToken> CancellationTokens { get; } = [];
        public int CallCount { get { lock (_gate) return _completions.Count; } }

        public Task<string> TranslateAsync(string source, string target, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _completions.Add(completion);
                CancellationTokens.Add(cancellationToken);
                cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                Started.TrySetResult();
                return completion.Task;
            }
        }

        public void Complete(string result, int callIndex = 0)
        {
            lock (_gate) _completions[callIndex].TrySetResult(result);
        }

        public async Task WaitForCallCountAsync(int count)
        {
            var limit = DateTime.UtcNow.AddSeconds(3);
            while (CallCount < count && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.Equal(count, CallCount);
        }
    }

    private sealed class CancellationIgnoringTranslator : ITextTranslator
    {
        private readonly TaskCompletionSource<string> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> TranslateAsync(string source, string target, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return _completion.Task;
        }
        public void Complete(string result) => _completion.TrySetResult(result);
    }
}
