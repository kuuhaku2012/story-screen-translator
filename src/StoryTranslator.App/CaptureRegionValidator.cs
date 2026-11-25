using System.Drawing;

namespace StoryTranslator.App;

/// <summary>Validates capture rectangles without using Rectangle.Right/Bottom, whose int arithmetic can wrap.</summary>
public static class CaptureRegionValidator
{
    public static bool Overlaps(Rectangle first, Rectangle second) =>
        first.Width > 0 && first.Height > 0 && second.Width > 0 && second.Height > 0 &&
        (long)first.Left < (long)second.Left + second.Width &&
        (long)second.Left < (long)first.Left + first.Width &&
        (long)first.Top < (long)second.Top + second.Height &&
        (long)second.Top < (long)first.Top + first.Height;

    public static bool IsWithin(Rectangle region, Rectangle virtualDesktop)
    {
        if (region.Width <= 0 || region.Height <= 0 || virtualDesktop.Width <= 0 || virtualDesktop.Height <= 0)
        {
            return false;
        }

        try
        {
            var regionLeft = (long)region.X;
            var regionTop = (long)region.Y;
            var regionRight = checked(regionLeft + region.Width);
            var regionBottom = checked(regionTop + region.Height);

            var desktopLeft = (long)virtualDesktop.X;
            var desktopTop = (long)virtualDesktop.Y;
            var desktopRight = checked(desktopLeft + virtualDesktop.Width);
            var desktopBottom = checked(desktopTop + virtualDesktop.Height);

            return regionLeft >= desktopLeft
                && regionTop >= desktopTop
                && regionRight <= desktopRight
                && regionBottom <= desktopBottom;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
