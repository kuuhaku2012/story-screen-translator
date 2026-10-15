namespace StoryTranslator.Core;

/// <summary>
/// Emits each newly stable, non-empty line once. Stability is defined as two
/// consecutive observations with the same normalized text.
/// </summary>
public sealed class TextStabilityFilter
{
    private string? _candidate;
    private string? _lastEmitted;

    public string? Observe(string? text)
        => ObserveDetailed(text).StableLine;

    /// <summary>Observes normalized OCR text and reports candidate changes before a line becomes stable.</summary>
    public TextStabilityObservation ObserveDetailed(string? text)
    {
        var normalized = Normalize(text);
        if (normalized is null)
        {
            var changed = _candidate is not null;
            _candidate = null;
            return new TextStabilityObservation(null, changed);
        }

        if (!string.Equals(_candidate, normalized, StringComparison.Ordinal))
        {
            _candidate = normalized;
            return new TextStabilityObservation(null, true);
        }

        if (string.Equals(_lastEmitted, normalized, StringComparison.Ordinal))
        {
            return new TextStabilityObservation(null, false);
        }

        _lastEmitted = normalized;
        return new TextStabilityObservation(normalized, false);
    }

    /// <summary>Clears candidate and deduplication state, for example at a Story Mode session boundary.</summary>
    public void Reset()
    {
        _candidate = null;
        _lastEmitted = null;
    }

    private static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        var builder = new System.Text.StringBuilder(trimmed.Length);
        var previousWasSpace = false;

        foreach (var character in trimmed)
        {
            if (character == ' ')
            {
                if (!previousWasSpace)
                {
                    builder.Append(character);
                }

                previousWasSpace = true;
                continue;
            }

            builder.Append(character);
            previousWasSpace = false;
        }

        return builder.ToString();
    }
}

public readonly record struct TextStabilityObservation(string? StableLine, bool CandidateChanged);
