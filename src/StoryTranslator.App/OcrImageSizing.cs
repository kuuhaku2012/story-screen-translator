using System.Drawing;

namespace StoryTranslator.App;

public static class OcrImageSizing
{
    public static Size Fit(int width, int height, int limit)
    {
        if (width <= 0 || height <= 0 || limit <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var scale = Math.Min(1d, (double)limit / Math.Max(width, height));
        return new Size(Math.Max(1, (int)Math.Floor(width * scale)), Math.Max(1, (int)Math.Floor(height * scale)));
    }
}
