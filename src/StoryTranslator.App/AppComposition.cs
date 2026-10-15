using System.IO;
using StoryTranslator.Core;

namespace StoryTranslator.App;

/// <summary>The app's default composition keeps preferences and translation cache in LocalApplicationData.</summary>
public sealed record AppRuntimeServices(
    string DataRoot,
    string CachePath,
    ITranslationCache Cache,
    SettingsStore SettingsStore,
    GlossaryStore GlossaryStore,
    TranslationCoordinator Coordinator);

public static class AppComposition
{
    public static AppRuntimeServices Create(ITextTranslator? translator = null)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Create(Path.Combine(localAppData, "StoryTranslator"), translator);
    }

    /// <summary>Creates services under a supplied app-data root; primarily useful for isolated tests.</summary>
    public static AppRuntimeServices Create(string dataRoot, ITextTranslator? translator = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var root = Path.GetFullPath(dataRoot);
        var cachePath = Path.Combine(root, "translation-cache.json");
        var cache = new JsonTranslationCache(cachePath);
        var coordinator = new TranslationCoordinator(translator ?? new UnavailableTranslator(), cache);
        return new AppRuntimeServices(
            root,
            cachePath,
            cache,
            new SettingsStore(Path.Combine(root, "settings.json")),
            new GlossaryStore(Path.Combine(root, "glossary.json")),
            coordinator);
    }

    private sealed class UnavailableTranslator : ITextTranslator
    {
        public Task<string> TranslateAsync(string source, string target, CancellationToken cancellationToken) =>
            Task.FromException<string>(new InvalidOperationException("Chưa cấu hình bộ dịch."));
    }
}

