using System.Drawing;
using StoryTranslator.App;

namespace StoryTranslator.Core.Tests;

public sealed class CaptureRegionValidatorTests
{
    private static readonly Rectangle VirtualDesktop = new(-1920, 0, 3840, 1080);

    [Fact]
    public void IsWithin_RejectsHorizontalExtentThatOverflowsInt32()
    {
        var region = new Rectangle(int.MaxValue - 5, 100, 20, 20);

        Assert.False(CaptureRegionValidator.IsWithin(region, VirtualDesktop));
    }

    [Fact]
    public void IsWithin_RejectsVerticalExtentThatOverflowsInt32()
    {
        var region = new Rectangle(10, int.MaxValue - 5, 20, 20);

        Assert.False(CaptureRegionValidator.IsWithin(region, VirtualDesktop));
    }

    [Theory]
    [InlineData(1900, 100, 30, 20)]
    [InlineData(-1921, 100, 20, 20)]
    [InlineData(10, 1070, 20, 20)]
    public void IsWithin_RejectsOrdinaryOutOfBoundsRegion(int x, int y, int width, int height)
    {
        Assert.False(CaptureRegionValidator.IsWithin(new Rectangle(x, y, width, height), VirtualDesktop));
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(3840, 0)]
    [InlineData(-1, 1080)]
    [InlineData(3840, -1)]
    public void IsWithin_RejectsNonpositiveVirtualDesktopMetrics(int width, int height)
    {
        Assert.False(CaptureRegionValidator.IsWithin(new Rectangle(0, 0, 10, 10), new Rectangle(0, 0, width, height)));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    public void IsWithin_RejectsNonpositiveCaptureDimensions(int width, int height)
    {
        Assert.False(CaptureRegionValidator.IsWithin(new Rectangle(0, 0, width, height), VirtualDesktop));
    }
}
