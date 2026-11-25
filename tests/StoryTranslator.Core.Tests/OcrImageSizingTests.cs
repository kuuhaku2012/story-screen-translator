using StoryTranslator.App;

namespace StoryTranslator.Core.Tests;

public sealed class OcrImageSizingTests
{
    [Theory]
    [InlineData(1920, 1080, 2600, 1920, 1080)]
    [InlineData(3840, 2160, 2600, 2600, 1462)]
    [InlineData(2160, 3840, 2600, 1462, 2600)]
    [InlineData(6000, 100, 2600, 2600, 43)]
    public void FitsLargeFramesToEngineLimit(int width, int height, int limit, int expectedWidth, int expectedHeight)
    {
        var size = OcrImageSizing.Fit(width, height, limit);
        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
    }
}
