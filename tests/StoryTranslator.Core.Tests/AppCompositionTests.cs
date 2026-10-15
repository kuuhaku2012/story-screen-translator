using StoryTranslator.App;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class AppCompositionTests
{    [Fact]
    public void DefaultCompositionRootsPersistentStoresInLocalApplicationData()
    {
        var services = AppComposition.Create(new CountingTranslator());
        var expectedRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoryTranslator"));

        Assert.Equal(expectedRoot, services.DataRoot);
        Assert.Equal(Path.Combine(expectedRoot, "translation-cache.json"), services.CachePath);
        Assert.IsType<JsonTranslationCache>(services.Cache);
    }

    [Fact]
    public async Task CompositionUsesPersistentCacheUnderLocalAppDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryTranslatorCompositionTests", Guid.NewGuid().ToString("N"));
        try
        {
            var translator = new CountingTranslator();
            var first = AppComposition.Create(root, translator);
            Assert.IsType<JsonTranslationCache>(first.Cache);
            Assert.StartsWith(Path.GetFullPath(root), first.CachePath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("translated", await first.Coordinator.SubmitAsync("dialogue", "vi"));

            var second = AppComposition.Create(root, translator);
            Assert.Equal("translated", await second.Coordinator.SubmitAsync("dialogue", "vi"));
            Assert.Equal(1, translator.Calls);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CountingTranslator : ITextTranslator
    {
        public int Calls { get; private set; }
        public Task<string> TranslateAsync(string source, string target, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult("translated");
        }
    }
}



