using System.Drawing;

namespace StoryTranslator.Core;

/// <summary>Keep cards within source bounds plus small padding, shared between nearby sources.</summary>
public static class SpatialPanelLayout
{
    public static IReadOnlyList<RectangleF> Arrange(IReadOnlyList<TranslatedScreenBlock> blocks, SizeF area)
    {
        var result = new List<RectangleF>();
        for (var i = 0; i < blocks.Count; i++)
        {
            var source = RectangleF.Intersect(blocks[i].Bounds, new RectangleF(PointF.Empty, area));
            if (source.Width <= 0 || source.Height <= 0) { result.Add(RectangleF.Empty); continue; }
            var left = Math.Min(4, source.Left);
            var top = Math.Min(4, source.Top);
            var right = Math.Min(4, area.Width - source.Right);
            var bottom = Math.Min(4, area.Height - source.Bottom);
            foreach (var other in blocks.Where((_, index) => index != i))
            {
                var b = other.Bounds;
                if (b.Top < source.Bottom + 4 && b.Bottom > source.Top - 4)
                {
                    if (b.Left >= source.Right) right = Math.Min(right, (b.Left - source.Right) / 2);
                    if (b.Right <= source.Left) left = Math.Min(left, (source.Left - b.Right) / 2);
                }
                if (b.Left < source.Right + 4 && b.Right > source.Left - 4)
                {
                    if (b.Top >= source.Bottom) bottom = Math.Min(bottom, (b.Top - source.Bottom) / 2);
                    if (b.Bottom <= source.Top) top = Math.Min(top, (source.Top - b.Bottom) / 2);
                }
            }
            result.Add(new RectangleF(source.Left - left, source.Top - top,
                source.Width + left + right, source.Height + top + bottom));
        }
        return result;
    }
}
