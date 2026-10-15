namespace StoryTranslator.Core;

/// <summary>Stores completed translations by exact source and target language.</summary>
public interface ITranslationCache
{
    bool TryGet(string source, string target, out string translation);
    void Set(string source, string target, string translation);
}

/// <summary>Translation submission boundary used by the story pipeline.</summary>
public interface ITranslationCoordinator
{
    Task<string> SubmitAsync(string line, string target = "vi", CancellationToken cancellationToken = default);
    void CancelPending();
}

/// <summary>A small bounded in-memory cache that evicts the least recently used entry.</summary>
public sealed class InMemoryTranslationCache : ITranslationCache
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<CacheKey, LinkedListNode<CacheEntry>> _entries = new();
    private readonly LinkedList<CacheEntry> _recency = new();

    public InMemoryTranslationCache(int capacity = 256)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public bool TryGet(string source, string target, out string translation)
    {
        lock (_gate)
        {
            var key = new CacheKey(source, target);
            if (!_entries.TryGetValue(key, out var node))
            {
                translation = string.Empty;
                return false;
            }

            _recency.Remove(node);
            _recency.AddFirst(node);
            translation = node.Value.Translation;
            return true;
        }
    }

    public void Set(string source, string target, string translation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(translation);
        lock (_gate)
        {
            var key = new CacheKey(source, target);
            if (_entries.TryGetValue(key, out var existing))
            {
                existing.Value = new CacheEntry(key, translation);
                _recency.Remove(existing);
                _recency.AddFirst(existing);
                return;
            }

            var node = _recency.AddFirst(new CacheEntry(key, translation));
            _entries.Add(key, node);
            if (_entries.Count > _capacity)
            {
                var oldest = _recency.Last!;
                _recency.RemoveLast();
                _entries.Remove(oldest.Value.Key);
            }
        }
    }

    private readonly record struct CacheKey(string Source, string Target);
    private sealed record CacheEntry(CacheKey Key, string Translation);
}

/// <summary>Runs at most one translation at a time and favors the newest submitted line.</summary>
public sealed class TranslationCoordinator : ITranslationCoordinator
{
    private readonly object _gate = new();
    private readonly ITextTranslator _translator;
    private readonly ITranslationCache _cache;
    private readonly Action? _beforeCancellationSignal;
    private Work? _current;

    public TranslationCoordinator(ITextTranslator translator, ITranslationCache? cache = null)
        : this(translator, cache, beforeCancellationSignal: null)
    {
    }

    internal TranslationCoordinator(ITextTranslator translator, ITranslationCache? cache, Action? beforeCancellationSignal)
    {
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _cache = cache ?? new InMemoryTranslationCache();
        _beforeCancellationSignal = beforeCancellationSignal;
    }

    public Task<string> SubmitAsync(string line, string target = "vi", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        Work? previous = null;
        Work? created = null;
        Task<string> resultTask;
        lock (_gate)
        {
            if (_current is { } current && current.Key == new TranslationKey(line, target))
            {
                resultTask = current.Completion.Task;
            }
            else if (_cache.TryGet(line, target, out var cached))
            {
                previous = _current;
                _current = null;
                resultTask = Task.FromResult(cached);
            }
            else
            {
                previous = _current;
                created = new Work(new TranslationKey(line, target), cancellationToken);
                _current = created;
                resultTask = created.Completion.Task;
            }
        }

        SignalCancellation(previous);
        if (created is not null) _ = ExecuteAsync(created, line, target);
        return cancellationToken.CanBeCanceled ? resultTask.WaitAsync(cancellationToken) : resultTask;
    }

    /// <summary>Cancels all currently pending translation work; safe to call repeatedly when turning OFF.</summary>
    public void CancelPending()
    {
        Work? canceled;
        lock (_gate)
        {
            canceled = _current;
            _current = null;
        }
        SignalCancellation(canceled);
    }

    private async Task ExecuteAsync(Work work, string source, string target)
    {
        try
        {
            var translation = await _translator.TranslateAsync(source, target, work.Cancellation.Token).ConfigureAwait(false);
            lock (_gate)
            {
                if (!ReferenceEquals(_current, work) || work.Cancellation.IsCancellationRequested)
                    throw new OperationCanceledException(work.Cancellation.Token);
                _cache.Set(source, target, translation);
                _current = null;
            }
            work.Completion.TrySetResult(translation);
        }
        catch (OperationCanceledException)
        {
            work.Completion.TrySetCanceled(work.Cancellation.Token);
        }
        catch (Exception exception)
        {
            work.Completion.TrySetException(exception);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_current, work)) _current = null;
            }
            work.Dispose();
        }
    }

    private void SignalCancellation(Work? work)
    {
        if (work is null) return;
        _beforeCancellationSignal?.Invoke();
        work.Cancel();
    }
    private readonly record struct TranslationKey(string Source, string Target);

    private sealed class Work : IDisposable
    {
        public Work(TranslationKey key, CancellationToken callerToken)
        {
            Key = key;
            Cancellation = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        }

        public TranslationKey Key { get; }
        public CancellationTokenSource Cancellation { get; }
        public TaskCompletionSource<string> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Cancel()
        {
            try { Cancellation.Cancel(); }
            catch (ObjectDisposedException) { }
        }
        public void Dispose() => Cancellation.Dispose();
    }
}
