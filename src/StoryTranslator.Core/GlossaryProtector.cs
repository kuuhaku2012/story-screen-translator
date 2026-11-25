using System.Text;
using System.Text.RegularExpressions;

namespace StoryTranslator.Core;

/// <summary>Protects known names in one sentence before translation.</summary>
public sealed class GlossaryProtector
{
    private readonly AliasPattern[] _patterns;

    public GlossaryProtector(IEnumerable<GlossaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var patterns = new List<AliasPattern>();
        var entryOrder = 0;
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.CanonicalName);
            ArgumentNullException.ThrowIfNull(entry.Aliases);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Category);

            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                entry.CanonicalName
            };
            foreach (var alias in entry.Aliases)
            {
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    aliases.Add(alias);
                }
            }

            foreach (var alias in aliases)
            {
                patterns.Add(new AliasPattern(alias, entry.CanonicalName, entryOrder));
            }

            entryOrder++;
        }

        _patterns = patterns
            .OrderByDescending(pattern => pattern.Alias.Length)
            .ThenBy(pattern => pattern.EntryOrder)
            .ToArray();
    }

    public ProtectedText Protect(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var builder = new StringBuilder(source.Length);
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        while (index < source.Length)
        {
            var match = FindMatch(source, index);
            if (match is null)
            {
                builder.Append(source[index]);
                index++;
                continue;
            }

            var token = CreateToken(source, tokens.Count + 1);
            tokens.Add(token, match.CanonicalName);
            builder.Append(token);
            index += match.Alias.Length;
        }

        return new ProtectedText(builder.ToString(), tokens);
    }

    public static string Restore(string translated, IReadOnlyDictionary<string, string> tokenMap)
    {
        ArgumentNullException.ThrowIfNull(translated);
        ArgumentNullException.ThrowIfNull(tokenMap);

        foreach (var (token, canonicalName) in tokenMap)
        {
            translated = translated.Replace(token, canonicalName, StringComparison.Ordinal);
        }

        return translated;
    }

    /// <summary>Reject missing, duplicated or foreign reserved tokens before restoring known names.</summary>
    public static string RestoreValidated(string translated, ProtectedText source)
    {
        ArgumentNullException.ThrowIfNull(translated);
        ArgumentNullException.ThrowIfNull(source);
        static Dictionary<string, int> Counts(string text) => Regex.Matches(text, @"⟬STG_[^⟭]*⟭")
            .Select(m => m.Value).GroupBy(t => t, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var expected = Counts(source.Text);
        var actual = Counts(translated);
        if (expected.Count != actual.Count || expected.Any(e => actual.GetValueOrDefault(e.Key) != e.Value))
            throw new InvalidOperationException("Bản dịch làm mất, lặp hoặc thêm ký hiệu bảo vệ tên riêng. Đã chặn kết quả; không tự gửi lại.");
        return Restore(translated, source.TokenMap);
    }

    private AliasPattern? FindMatch(string source, int index)
    {
        foreach (var pattern in _patterns)
        {
            if (index + pattern.Alias.Length > source.Length ||
                !source.AsSpan(index, pattern.Alias.Length).Equals(pattern.Alias, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var beforeIsWord = index > 0 && IsWordCharacter(source[index - 1]);
            var afterIndex = index + pattern.Alias.Length;
            var afterIsWord = afterIndex < source.Length && IsWordCharacter(source[afterIndex]);
            if (!beforeIsWord && !afterIsWord)
            {
                return pattern;
            }
        }

        return null;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static string CreateToken(string source, int ordinal)
    {
        var token = $"⟬STG_{ordinal:D4}⟭";
        var collision = 0;
        do
        {
            if (collision > 0)
            {
                token = $"⟬STG_{ordinal:D4}_{collision:D4}⟭";
            }
            collision++;
        }
        while (source.Contains(token, StringComparison.Ordinal));

        return token;
    }

    private sealed record AliasPattern(string Alias, string CanonicalName, int EntryOrder);
}

public sealed record ProtectedText(string Text, IReadOnlyDictionary<string, string> TokenMap);
