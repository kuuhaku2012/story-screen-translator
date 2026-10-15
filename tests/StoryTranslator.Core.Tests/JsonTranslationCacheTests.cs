using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class JsonTranslationCacheTests
{
    [Fact]
    public void EntriesSurviveReloadAndAccessRefreshesRecency()
    {
        var path = NewCachePath();
        try
        {
            var first = new JsonTranslationCache(path);
            first.Set("Hello", "vi", "Xin chào");
            var second = new JsonTranslationCache(path);
            Assert.True(second.TryGet("Hello", "vi", out var translated));
            Assert.Equal("Xin chào", translated);
        }
        finally { DeleteCacheFiles(path); }
    }

    [Fact]
    public void CacheEvictsLeastRecentlyUsedEntryAtHardLimitOfOneThousand()
    {
        var path = NewCachePath();
        try
        {
            var cache = new JsonTranslationCache(path);
            for (var index = 0; index < 1000; index++) cache.Set($"line-{index}", "vi", $"translated-{index}");
            cache = new JsonTranslationCache(path);
            cache.Set("line-1000", "vi", "translated-1000");
            Assert.Equal(1000, cache.Count);
            Assert.False(cache.TryGet("line-0", "vi", out _));
            Assert.True(cache.TryGet("line-1", "vi", out _));
            Assert.True(cache.TryGet("line-1000", "vi", out _));
        }
        finally { DeleteCacheFiles(path); }
    }

    [Fact]
    public void MalformedCacheRecoversEmptyWithoutExposingDialogueInDiagnostic()
    {
        var path = NewCachePath();
        const string privateLine = "private dialogue content";
        var diagnostics = new List<string>();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"{{ malformed {privateLine} }}");
            var cache = new JsonTranslationCache(path, diagnostics.Add);
            Assert.Equal(0, cache.Count);
            Assert.Empty(diagnostics);
            Assert.False(cache.TryGet(privateLine, "vi", out _));
        }
        finally { DeleteCacheFiles(path); }
    }

    private static string NewCachePath() => Path.Combine(Path.GetTempPath(), "StoryTranslatorTests", Guid.NewGuid().ToString("N"), "cache.json");
    private static void DeleteCacheFiles(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}






