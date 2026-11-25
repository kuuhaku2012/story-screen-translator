using System.Drawing;

namespace StoryTranslator.Core;

/// <summary>Optional landscape subtitle gate; coordinates are relative to the selected capture region.</summary>
public static class CutsceneSubtitleSelector
{
    public static IReadOnlyList<ScreenTextBlock> Select(IReadOnlyList<ScreenTextBlock> blocks, Size region)
    {
        if (region.Width < 800 || region.Height < 450 || region.Width < region.Height * 1.25)
            return blocks;

        var candidates = blocks.Where(b =>
        {
            var bounds = b.Bounds;
            var x = (bounds.Left + bounds.Right) / 2 / region.Width;
            var y = (bounds.Top + bounds.Bottom) / 2 / region.Height;
            return LatinCount(b.Text) >= 2 && x is > .18f and < .82f &&
                (y is > .02f and < .16f || y is > .70f and < .95f);
        }).ToArray();

        var top = candidates.Where(b => b.Bounds.Top < region.Height * .16f).ToArray();
        var bottom = candidates.Except(top).ToArray();
        var lowerDialogue = KeepAnchored(bottom);
        return lowerDialogue.Count > 0 ? lowerDialogue : KeepAnchored(top);

        IReadOnlyList<ScreenTextBlock> KeepAnchored(ScreenTextBlock[] zone)
            => zone.Any(b => LatinCount(b.Text) >= 12 ||
                b.Bounds.Width >= region.Width * .13f ||
                (b.Bounds.Left + b.Bounds.Right) / 2 / region.Width is > .42f and < .58f)
                ? MergeSameLine(zone) : [];
    }

    private static IReadOnlyList<ScreenTextBlock> MergeSameLine(ScreenTextBlock[] blocks)
    {
        var merged = new List<ScreenTextBlock>();
        foreach (var block in blocks.OrderBy(b => b.Bounds.Left))
        {
            var index = merged.FindIndex(existing =>
                Math.Abs((existing.Bounds.Top + existing.Bounds.Bottom - block.Bounds.Top - block.Bounds.Bottom) / 2)
                    <= Math.Max(existing.LineHeight, block.LineHeight) * .65f &&
                block.Bounds.Left >= existing.Bounds.Right &&
                block.Bounds.Left - existing.Bounds.Right <= Math.Max(existing.LineHeight, block.LineHeight) * 2.5f);
            if (index < 0) merged.Add(block);
            else
            {
                var existing = merged[index];
                merged[index] = new ScreenTextBlock(existing.Text + " " + block.Text,
                    RectangleF.Union(existing.Bounds, block.Bounds),
                    (existing.LineHeight + block.LineHeight) / 2);
            }
        }
        return merged;
    }

    private static int LatinCount(string text) => text.Count(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
}
