using System.Text.Json;

namespace StoryTranslator.Core;

/// <summary>Persistent translation cache with a fixed 1,000-entry LRU limit.</summary>
public sealed class JsonTranslationCache : ITranslationCache
{
    public const int MaximumEntries = 1000;
    private readonly object _gate = new();
    private readonly string _path;
    private readonly Dictionary<CacheKey, LinkedListNode<CacheEntry>> _entries = new();
    private readonly LinkedList<CacheEntry> _recency = new();

    public JsonTranslationCache(string path, Action<string>? diagnostic = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        Load();
    }

    public int Count { get { lock (_gate) return _entries.Count; } }

    public bool TryGet(string source, string target, out string translation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        lock (_gate)
        {
            if (!_entries.TryGetValue(new CacheKey(source, target), out var node))
            {
                translation = string.Empty;
                return false;
            }
            _recency.Remove(node);
            _recency.AddFirst(node);
            translation = node.Value.Translation;
            return true;
        }
    }

    public void Set(string source, string target, string translation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(translation);
        lock (_gate)
        {
            var key = new CacheKey(source, target);
            if (_entries.TryGetValue(key, out var existing))
            {
                existing.Value = new CacheEntry(key, translation);
                _recency.Remove(existing);
                _recency.AddFirst(existing);
            }
            else
            {
                _entries.Add(key, _recency.AddFirst(new CacheEntry(key, translation)));
                if (_entries.Count > MaximumEntries)
                {
                    var oldest = _recency.Last!;
                    _recency.RemoveLast();
                    _entries.Remove(oldest.Value.Key);
                }
            }
            PersistAtomically();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var records = JsonSerializer.Deserialize<List<CacheRecord>>(File.ReadAllText(_path));
            if (records is null) return;
            foreach (var record in records.Take(MaximumEntries).Reverse())
            {
                if (record is null || record.Source is null || record.Target is null || record.Translation is null) continue;
                var key = new CacheKey(record.Source, record.Target);
                if (_entries.TryGetValue(key, out var previous))
                {
                    _recency.Remove(previous);
                    _entries.Remove(key);
                }
                _entries.Add(key, _recency.AddFirst(new CacheEntry(key, record.Translation)));
            }
        }
        catch (JsonException)
        {
            // The cache contains private dialogue, so recovery is deliberately silent.
            _entries.Clear();
            _recency.Clear();
        }
        catch (IOException)
        {
            _entries.Clear();
            _recency.Clear();
        }
        catch (UnauthorizedAccessException)
        {
            _entries.Clear();
            _recency.Clear();
        }
    }

    private void PersistAtomically()
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var records = _recency.Select(entry => new CacheRecord(entry.Key.Source, entry.Key.Target, entry.Translation)).ToArray();
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, records);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private readonly record struct CacheKey(string Source, string Target);
    private sealed record CacheEntry(CacheKey Key, string Translation);
    private sealed record CacheRecord(string? Source, string? Target, string? Translation);
}

