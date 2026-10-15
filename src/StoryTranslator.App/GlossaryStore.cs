using System.IO;
using System.Text.Json;
using StoryTranslator.Core;

namespace StoryTranslator.App;

/// <summary>Loads/saves the user glossary in LocalApplicationData without dialogue or provider secrets.</summary>
public sealed class GlossaryStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public GlossaryStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoryTranslator", "glossary.json");

    public IReadOnlyList<GlossaryEntry> Load()
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<GlossaryEntry>();
            return JsonSerializer.Deserialize<List<GlossaryEntry>>(File.ReadAllText(_path), _options) ?? [];
        }
        catch (JsonException) { return Array.Empty<GlossaryEntry>(); }
        catch (IOException) { return Array.Empty<GlossaryEntry>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<GlossaryEntry>(); }
    }

    public void Save(IEnumerable<GlossaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var snapshot = entries.ToArray();
        if (snapshot.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.CanonicalName) || entry.Aliases is null || string.IsNullOrWhiteSpace(entry.Category)))
            throw new ArgumentException("Every glossary entry must have a canonical name, aliases, and category.", nameof(entries));
        var fullPath = Path.GetFullPath(_path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, snapshot, _options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

