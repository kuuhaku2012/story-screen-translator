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
    {
        var normalized = Normalize(text);
        if (normalized is null)
        {
            _candidate = null;
            return null;
        }

        if (!string.Equals(_candidate, normalized, StringComparison.Ordinal))
        {
            _candidate = normalized;
            return null;
        }

        if (string.Equals(_lastEmitted, normalized, StringComparison.Ordinal))
        {
            return null;
        }

        _lastEmitted = normalized;
        return normalized;
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
