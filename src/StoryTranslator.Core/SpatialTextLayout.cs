using System.Drawing;

namespace StoryTranslator.Core;

public sealed record OcrWordBox(string Text, RectangleF Bounds);
public sealed record ScreenTextBlock(string Text, RectangleF Bounds, float LineHeight);
public sealed record TranslatedScreenBlock(string Text, RectangleF Bounds, float LineHeight);

public interface IBatchTextTranslator
{
    Task<IReadOnlyList<string>> TranslateBatchAsync(IReadOnlyList<string> texts, CancellationToken token);
}

/// <summary>Geometry-based horizontal text grouping; does not join separate columns across a wide gutter.</summary>
public static class SpatialTextLayout
{
    public static IReadOnlyList<ScreenTextBlock> Group(IEnumerable<IEnumerable<OcrWordBox>> lines, bool comicMode = false)
    {
        var segments = new List<ScreenTextBlock>();
        foreach (var line in lines)
        {
            var words = line.Where(w => !string.IsNullOrWhiteSpace(w.Text) && w.Bounds.Width > 0 && w.Bounds.Height > 0)
                .OrderBy(w => w.Bounds.Left).ToArray();
            var part = new List<OcrWordBox>();
            foreach (var word in words)
            {
                if (part.Count > 0)
                {
                    var previous = part[^1].Bounds;
                    var height = Math.Max(previous.Height, word.Bounds.Height);
                    if (word.Bounds.Left - previous.Right > Math.Max(24, height * 1.6f) ||
                        Math.Abs(word.Bounds.Top - previous.Top) > height * .65f)
                    {
                        segments.Add(MakeLine(part));
                        part.Clear();
                    }
                }
                part.Add(word);
            }
            if (part.Count > 0) segments.Add(MakeLine(part));
        }

        if (comicMode) return GroupComicLines(segments);

        var paragraphs = new List<List<ScreenTextBlock>>();
        foreach (var segment in segments.OrderBy(s => s.Bounds.Top).ThenBy(s => s.Bounds.Left))
        {
            List<ScreenTextBlock>? best = null;
            var bestScore = float.MaxValue;
            foreach (var paragraph in paragraphs)
            {
                var previous = paragraph[^1];
                var height = Math.Max(previous.LineHeight, segment.LineHeight);
                var gap = segment.Bounds.Top - previous.Bounds.Bottom;
                var overlap = Math.Max(0, Math.Min(previous.Bounds.Right, segment.Bounds.Right) - Math.Max(previous.Bounds.Left, segment.Bounds.Left));
                var aligned = Math.Abs(previous.Bounds.Left - segment.Bounds.Left) <= height * 1.2f;
                if (comicMode)
                    aligned |= Math.Abs((previous.Bounds.Left + previous.Bounds.Right - segment.Bounds.Left - segment.Bounds.Right) / 2) <= height * .75f;
                var similarFont = Math.Min(previous.LineHeight, segment.LineHeight) >= height * .74f;
                if (segment.Bounds.Top <= previous.Bounds.Top || gap < -height * .2f || gap > height * .8f || !similarFont ||
                    (!aligned && overlap < Math.Min(previous.Bounds.Width, segment.Bounds.Width) * .75f) ||
                    paragraph.Count >= 12 || paragraph.Sum(p => p.Text.Length) + segment.Text.Length > 1400) continue;
                var score = gap + Math.Abs(previous.Bounds.Left - segment.Bounds.Left) * .1f;
                if (score < bestScore) { best = paragraph; bestScore = score; }
            }
            if (best is null) paragraphs.Add([segment]);
            else best.Add(segment);
        }
        return paragraphs.Select(p => new ScreenTextBlock(string.Join("\n", p.Select(s => s.Text)),
                p.Select(s => s.Bounds).Aggregate(RectangleF.Union), p.Average(s => s.LineHeight)))
            .OrderBy(b => b.Bounds.Top).ThenBy(b => b.Bounds.Left).ToArray();
    }

    private static ScreenTextBlock MakeLine(List<OcrWordBox> words) => new(string.Join(" ", words.Select(w => w.Text)),
        words.Select(w => w.Bounds).Aggregate(RectangleF.Union), words.Average(w => w.Bounds.Height));

    private static IReadOnlyList<ScreenTextBlock> GroupComicLines(List<ScreenTextBlock> segments)
    {
        var lines = segments.OrderBy(s => s.Bounds.Top).ThenBy(s => s.Bounds.Left).ToArray();
        var edges = new List<(double Score, int From, int To)>();
        for (var i = 0; i < lines.Length; i++)
        for (var j = 0; j < lines.Length; j++)
        {
            if (i == j) continue;
            var a = lines[i].Bounds;
            var b = lines[j].Bounds;
            var height = Math.Max(a.Height, b.Height);
            var dy = (b.Top + b.Bottom - a.Top - a.Bottom) / 2;
            var dx = Math.Abs((b.Left + b.Right - a.Left - a.Right) / 2);
            var gap = b.Top - a.Bottom;
            var overlap = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
            var widerAspect = a.Width >= b.Width ? a.Width / a.Height : b.Width / b.Height;
            if (Math.Max(a.Width, b.Width) > Math.Min(a.Width, b.Width) * 3 && widerAspect > 6) continue;
            if (dy < height * .4f || gap < -height * .5f || gap > height * 1.05f ||
                Math.Min(a.Height, b.Height) < height * .45f) continue;
            if (dx > Math.Max(height * .9f, Math.Min(a.Width, b.Width) * .55f) &&
                !(overlap >= Math.Min(a.Width, b.Width) * .65f && dx <= Math.Max(a.Width, b.Width) * .48f)) continue;
            var score = dx / height + Math.Max(gap, 0) / height + dy / height * .5 + Math.Abs(Math.Log(a.Width / b.Width)) * .15;
            edges.Add((score, i, j));
        }
        var outgoing = new Dictionary<int, int>();
        var incoming = new Dictionary<int, int>();
        foreach (var edge in edges.OrderBy(e => e.Score).ThenBy(e => e.From).ThenBy(e => e.To))
        {
            outgoing.TryAdd(edge.From, edge.To);
            incoming.TryAdd(edge.To, edge.From);
        }
        var next = outgoing.Where(e => incoming.GetValueOrDefault(e.Value, -1) == e.Key)
            .ToDictionary(e => e.Key, e => e.Value);
        var previous = next.Values.ToHashSet();
        var groups = new List<List<ScreenTextBlock>>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (previous.Contains(i)) continue;
            var chain = new List<ScreenTextBlock> { lines[i] };
            var current = i;
            while (next.TryGetValue(current, out var successor))
            {
                current = successor;
                chain.Add(lines[current]);
            }
            groups.Add(chain);
        }
        // Attach a contained single word instead of placing a second panel inside a paragraph.
        foreach (var small in groups.Where(g => g.Count == 1).OrderBy(g => Area(Bounds(g))).ToArray())
        {
            if (!groups.Contains(small)) continue;
            var a = Bounds(small);
            var large = groups.FirstOrDefault(g => g != small && g.Count >= 3 &&
                Area(RectangleF.Intersect(a, Bounds(g))) / Math.Max(1, Area(a)) >= .85f);
            if (large is null) continue;
            large.AddRange(small);
            groups.Remove(small);
        }
        return groups.Select(g => new ScreenTextBlock(
                string.Join("\n", g.OrderBy(l => l.Bounds.Top).ThenBy(l => l.Bounds.Left).Select(l => l.Text)),
                Bounds(g), g.Average(l => l.LineHeight)))
            .OrderBy(g => g.Bounds.Top).ThenBy(g => g.Bounds.Left).ToArray();
    }

    private static RectangleF Bounds(IEnumerable<ScreenTextBlock> lines) => lines.Select(l => l.Bounds).Aggregate(RectangleF.Union);
    private static float Area(RectangleF box) => Math.Max(0, box.Width) * Math.Max(0, box.Height);
}
