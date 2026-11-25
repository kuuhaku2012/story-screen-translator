using System.IO;
using System.Text.Json;

namespace StoryTranslator.App;

/// <summary>Session-only timing samples. Never stores recognized text, screen images, or credentials.</summary>
public sealed class CutsceneTimingRecorder
{
    private readonly object _gate = new();
    private readonly List<Sample> _samples = [];

    public void Add(string stage, TimeSpan elapsed)
    {
        lock (_gate)
        {
            if (_samples.Count < 4096)
                _samples.Add(new Sample(DateTimeOffset.UtcNow, stage, Math.Round(elapsed.TotalMilliseconds, 1)));
        }
    }

    public void Save(string directory)
    {
        Sample[] samples;
        lock (_gate) samples = _samples.ToArray();
        if (samples.Length == 0) return;
        Directory.CreateDirectory(directory);
        var name = $"cutscene-timing-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json";
        File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(new { samples },
            new JsonSerializerOptions { WriteIndented = true }));
    }

    public readonly record struct Sample(DateTimeOffset Utc, string Stage, double Milliseconds);
}
