namespace StoryTranslator.Core;

/// <summary>A canonical name and the OCR spellings that should resolve to it.</summary>
public sealed record GlossaryEntry(string CanonicalName, IReadOnlyList<string> Aliases, string Category);
