using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace StoryTranslator.Core;

public interface IRateLimitedTextTranslator
{
    Task WaitUntilReadyAsync(CancellationToken token);
}

public sealed class GroqRateLimitException(string message) : InvalidOperationException(message);

/// <summary>Shared by the app across pause/resume; stores timing only, never credentials or text.</summary>
public sealed class GroqRateLimiter
{
    private readonly object _gate = new();
    private DateTimeOffset _next;

    internal TimeSpan RemainingDelay
    {
        get
        {
            lock (_gate)
            {
                var delay = _next - DateTimeOffset.UtcNow;
                return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
            }
        }
    }

    public async Task WaitAsync(CancellationToken token)
    {
        while (true)
        {
            TimeSpan delay;
            lock (_gate) delay = _next - DateTimeOffset.UtcNow;
            if (delay <= TimeSpan.Zero) return;
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
    }

    public void Defer(TimeSpan delay)
    {
        lock (_gate)
        {
            var next = DateTimeOffset.UtcNow + delay;
            if (next > _next) _next = next;
        }
    }

    public void Observe(HttpResponseHeaders headers, int estimatedBudget)
    {
        if (Number(headers, "x-ratelimit-remaining-requests") is <= 0)
            Defer(Duration(headers, "x-ratelimit-reset-requests") ?? TimeSpan.FromSeconds(1));
        if (Number(headers, "x-ratelimit-remaining-tokens") is { } tokens && tokens < estimatedBudget)
            Defer(Duration(headers, "x-ratelimit-reset-tokens") ?? TimeSpan.FromSeconds(60));
    }

    public static double? Number(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values) && double.TryParse(values.FirstOrDefault(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;

    public static TimeSpan? Duration(HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values)) return null;
        var value = values.FirstOrDefault() ?? "";
        var matches = Regex.Matches(value, @"(\d+(?:\.\d+)?)(ms|s|m|h|d)");
        if (matches.Count == 0 || string.Concat(matches.Select(m => m.Value)) != value) return null;
        var seconds = matches.Sum(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) *
            (m.Groups[2].Value switch { "ms" => .001, "m" => 60, "h" => 3600, "d" => 86400, _ => 1 }));
        return double.IsFinite(seconds) && seconds is >= 0 and <= 86400 ? TimeSpan.FromSeconds(seconds) : null;
    }
}
