using System.IO;
using System.Drawing;
using System.Text.Json;

namespace StoryTranslator.App;

public sealed record AppSettings
{
    public Rectangle? CaptureRegion { get; init; }
    public int SamplingIntervalMilliseconds { get; init; } = 500;
    public string SourceLanguage { get; init; } = "en";
    public string TargetLanguage { get; init; } = "vi";
    public List<StoryTranslator.Core.GlossaryEntry> Glossary { get; init; } = [];
}

/// <summary>Persists only non-secret user preferences under LocalApplicationData.</summary>
public sealed class SettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public SettingsStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoryTranslator", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), _options) ?? new AppSettings();
            if (settings.SamplingIntervalMilliseconds is < 250 or > 5000 ||
                string.IsNullOrWhiteSpace(settings.SourceLanguage) || string.IsNullOrWhiteSpace(settings.TargetLanguage))
            {
                return new AppSettings();
            }
            return settings;
        }
        catch (JsonException) { return new AppSettings(); }
        catch (IOException) { return new AppSettings(); }
        catch (UnauthorizedAccessException) { return new AppSettings(); }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SamplingIntervalMilliseconds is < 250 or > 5000) throw new ArgumentOutOfRangeException(nameof(settings));
        if (string.IsNullOrWhiteSpace(settings.SourceLanguage) || string.IsNullOrWhiteSpace(settings.TargetLanguage))
            throw new ArgumentException("Language codes are required.", nameof(settings));
        PersistAtomically(settings);
    }

    private void PersistAtomically(AppSettings settings)
    {
        var fullPath = Path.GetFullPath(_path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, settings, _options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}



