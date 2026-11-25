using System.Diagnostics;
using System.Drawing;

namespace StoryTranslator.Core;

/// <summary>Per-block stability and one sequential batch queue. A changing block cannot invalidate its neighbors.</summary>
public sealed class SpatialTranslationPipeline
{
    private readonly object _gate = new();
    private readonly IBatchTextTranslator _translator;
    private readonly GlossaryProtector _glossary;
    private readonly Action<IReadOnlyList<TranslatedScreenBlock>, Func<bool>> _present;
    private readonly Action<Exception>? _error;
    private readonly bool _cutsceneMode;
    private readonly Action<TimeSpan>? _translationTiming;
    private readonly InMemoryTranslationCache _cache = new();
    private List<State> _states = [];
    private IReadOnlyList<TranslatedScreenBlock> _lastView = [];
    private CancellationTokenSource? _cancellation;
    private Task _drain = Task.CompletedTask;
    private bool _active, _running;
    private long _epoch, _viewVersion;
    private int _untranslatedFrames;
    private int _pendingBlankFrames;

    public SpatialTranslationPipeline(IBatchTextTranslator translator, GlossaryProtector glossary,
        Action<IReadOnlyList<TranslatedScreenBlock>, Func<bool>> present, Action<Exception>? error = null,
        bool cutsceneMode = false, Action<TimeSpan>? translationTiming = null)
    {
        _translator = translator; _glossary = glossary; _present = present; _error = error;
        _cutsceneMode = cutsceneMode;
        _translationTiming = translationTiming;
    }

    public void Enable(CancellationToken token = default)
    {
        lock (_gate)
        {
            _cancellation?.Cancel();
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            _epoch++; _viewVersion++; _active = true; _running = false;
            _states = []; _lastView = [];
            _untranslatedFrames = 0;
            _pendingBlankFrames = 0;
        }
    }

    public void TurnOff()
    {
        lock (_gate)
        {
            _active = false; _epoch++; _viewVersion++;
            _cancellation?.Cancel();
            _states.Clear();
        }
    }

    public Task ObserveAsync(IReadOnlyList<ScreenTextBlock> layout)
    {
        lock (_gate)
        {
            if (!_active) return Task.CompletedTask;
            var previous = new List<State>(_states);
            var next = new List<State>();
            foreach (var block in layout.Where(b => !string.IsNullOrWhiteSpace(b.Text)))
            {
                var match = previous.OrderByDescending(s => MatchScore(s.Block.Bounds, block.Bounds))
                    .FirstOrDefault(s => MatchScore(s.Block.Bounds, block.Bounds) >= .5f);
                if (match is null && _cutsceneMode)
                    match = previous.FirstOrDefault(s => s.Block.Text == block.Text);
                if (match is not null) previous.Remove(match);
                State state;
                if (match is not null && (match.Block.Text == block.Text ||
                    _cutsceneMode && SmallOcrDifference(match.Block.Text, block.Text)))
                {
                    state = match;
                    // A dropped letter in a moving subtitle must not invalidate its translation.
                    state.Block = block with { Text = match.Block.Text };
                    state.Samples++;
                }
                else state = new State(block);
                if ((Ready(state) || _cutsceneMode) && state.Translation is null && _cache.TryGet(block.Text, "vi", out var cached))
                    state.Translation = cached;
                next.Add(state);
            }
            var observedText = next.Count > 0;
            if (_cutsceneMode && !observedText && previous.Any(s => s.Translation is null) &&
                ++_pendingBlankFrames <= 4)
                next = previous;
            else if (observedText) _pendingBlankFrames = 0;
            _states = next;
            PublishLocked(observation: true);
            if (!_running && _states.Any(s => Ready(s) && s.Translation is null))
            {
                _running = true;
                var epoch = _epoch;
                var token = _cancellation!.Token;
                _drain = Task.Run(() => DrainAsync(epoch, token));
            }
            return _drain;
        }
    }

    private async Task DrainAsync(long epoch, CancellationToken token)
    {
        var rateLimitAttempts = 0;
        try
        {
            while (true)
            {
                State[] batch;
                lock (_gate)
                {
                    if (!_active || _epoch != epoch) return;
                    batch = SelectPendingBatchLocked();
                    if (batch.Length == 0) { _running = false; return; }
                }
                if (_translator is IRateLimitedTextTranslator limited)
                {
                    await limited.WaitUntilReadyAsync(token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (!_active || _epoch != epoch) return;
                        batch = SelectPendingBatchLocked();
                    }
                    if (batch.Length == 0) continue;
                }
                token.ThrowIfCancellationRequested();
                var protectedTexts = batch.Select(s => _glossary.Protect(s.Block.Text)).ToArray();
                IReadOnlyList<string> results;
                try
                {
                    if (_cutsceneMode && protectedTexts.Length == 1 && _translator is ITextTranslator single)
                        results = [await single.TranslateAsync(protectedTexts[0].Text, "vi", token).ConfigureAwait(false)];
                    else
                        results = await _translator.TranslateBatchAsync(protectedTexts.Select(p => p.Text).ToArray(), token).ConfigureAwait(false);
                    rateLimitAttempts = 0;
                }
                catch (GroqRateLimitException) when (++rateLimitAttempts <= 2)
                {
                    // Re-select current stable blocks after cooldown; do not retry an obsolete screen.
                    continue;
                }
                catch (GroqRateLimitException)
                {
                    throw new InvalidOperationException("Groq vẫn trả HTTP 429 sau hai lần chờ. Đã dừng; kiểm tra hạn mức tại Groq Console trước khi bật lại.");
                }
                if (results.Count != batch.Length) throw new InvalidOperationException("Groq trả thiếu bản dịch cho các vùng chữ.");
                // Validate the entire batch before writing any restored result to the cache or view.
                var restored = results.Select((result, index) =>
                    GlossaryProtector.RestoreValidated(result, protectedTexts[index])).ToArray();
                lock (_gate)
                {
                    if (!_active || _epoch != epoch || token.IsCancellationRequested) return;
                    for (var i = 0; i < batch.Length; i++)
                    {
                        var translation = restored[i];
                        _cache.Set(batch[i].Block.Text, "vi", translation);
                        if (_states.Contains(batch[i]))
                        {
                            batch[i].Translation = translation;
                            _translationTiming?.Invoke(Stopwatch.GetElapsedTime(batch[i].FirstSeen));
                        }
                    }
                    PublishLocked();
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            lock (_gate)
            {
                if (_epoch != epoch || !_active) return;
                _active = false; _running = false; _viewVersion++;
            }
            _error?.Invoke(exception);
        }
    }

    private State[] SelectPendingBatchLocked()
    {
        var selected = new List<State>();
        var characters = 0;
        foreach (var state in _states.Where(s => Ready(s) && s.Translation is null))
        {
            if (selected.Count > 0 && (selected.Count >= 8 || characters + state.Block.Text.Length > 2800)) break;
            selected.Add(state);
            characters += state.Block.Text.Length;
        }
        return selected.ToArray();
    }

    private void PublishLocked(bool observation = false)
    {
        var view = _states.Where(s => s.Translation is not null)
            .Select(s => new TranslatedScreenBlock(s.Translation!, s.Block.Bounds, s.Block.LineHeight)).ToArray();
        if (_cutsceneMode)
        {
            if (view.Length > 0) _untranslatedFrames = 0;
            else if (_lastView.Count > 0)
            {
                if (observation) _untranslatedFrames++;
                // Keep the last subtitle while OCR or a new translation briefly lags a scene change.
                if (_untranslatedFrames <= 4) return;
            }
        }
        if (view.SequenceEqual(_lastView)) return;
        _lastView = view;
        var version = ++_viewVersion;
        var epoch = _epoch;
        _present(view, () => { lock (_gate) return _active && _epoch == epoch && _viewVersion == version; });
    }

    private bool Ready(State state) => state.Samples >= 2 || _cutsceneMode &&
        state.Block.Text.Count(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z') >= 10 &&
        state.Block.Text.Count(c => c == ' ') >= 2;

    private static bool SmallOcrDifference(string original, string current)
    {
        if (original.Length < 12) return false;
        var maxEdits = Math.Min(6, Math.Max(3, (int)Math.Ceiling(original.Length * .10)));
        if (Math.Abs(original.Length - current.Length) > maxEdits) return false;
        var previous = new int[current.Length + 1];
        var next = new int[current.Length + 1];
        for (var j = 0; j <= current.Length; j++) previous[j] = j;
        for (var i = 1; i <= original.Length; i++)
        {
            next[0] = i;
            var best = next[0];
            for (var j = 1; j <= current.Length; j++)
            {
                next[j] = Math.Min(Math.Min(previous[j] + 1, next[j - 1] + 1),
                    previous[j - 1] + (original[i - 1] == current[j - 1] ? 0 : 1));
                best = Math.Min(best, next[j]);
            }
            if (best > maxEdits) return false;
            (previous, next) = (next, previous);
        }
        return previous[current.Length] <= maxEdits;
    }

    private static float MatchScore(RectangleF first, RectangleF second)
    {
        var overlap = RectangleF.Intersect(first, second);
        var union = first.Width * first.Height + second.Width * second.Height - overlap.Width * overlap.Height;
        return overlap.Width > 0 && overlap.Height > 0 && union > 0 ? overlap.Width * overlap.Height / union : 0;
    }

    private sealed class State(ScreenTextBlock block)
    {
        public ScreenTextBlock Block = block;
        public readonly long FirstSeen = Stopwatch.GetTimestamp();
        public int Samples = 1;
        public string? Translation;
    }
}
