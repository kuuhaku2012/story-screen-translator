using System.Text.Json;
using StoryTranslator.App;

namespace StoryTranslator.Core.Tests;

public sealed class CutsceneTimingRecorderTests
{
    [Fact]
    public void SavesOnlyStageAndDuration()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "story-timing-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recorder = new CutsceneTimingRecorder();
            recorder.Add("ocr-native", TimeSpan.FromMilliseconds(125.45));
            recorder.Save(directory);
            var path = Assert.Single(Directory.GetFiles(directory, "*.json"));
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var sample = Assert.Single(json.RootElement.GetProperty("samples").EnumerateArray());
            Assert.Equal("ocr-native", sample.GetProperty("Stage").GetString());
            Assert.Equal(125.4, sample.GetProperty("Milliseconds").GetDouble());
            Assert.Equal(3, sample.EnumerateObject().Count());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
