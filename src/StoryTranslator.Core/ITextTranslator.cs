namespace StoryTranslator.Core;

/// <summary>Translates text without taking screenshots or other image data.</summary>
public interface ITextTranslator
{
    Task<string> TranslateAsync(string source, string target, CancellationToken cancellationToken);
}
