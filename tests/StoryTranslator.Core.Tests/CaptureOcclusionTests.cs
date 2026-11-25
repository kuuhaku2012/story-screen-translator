using System.Drawing;
using StoryTranslator.App;

namespace StoryTranslator.Core.Tests;

public sealed class CaptureOcclusionTests
{
    [Fact]
    public async Task CaptureAlwaysRestoresVisibilityAfterGuardCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var restored = false;
        var capture = new WindowsRegionCapture((region, token) =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        }, () => { restored = true; return Task.CompletedTask; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.CaptureAsync(new Rectangle(0, 0, 1, 1), cancellation.Token));
        Assert.True(restored);
    }

    [Theory]
    [InlineData(100, 100, 300, 200, true)]
    [InlineData(0, 0, 1920, 1080, true)]
    [InlineData(400, 100, 200, 200, false)]
    [InlineData(500, 500, 100, 100, false)]
    [InlineData(-50, 100, 200, 100, true)]
    public void DetectsWindowCoveringCaptureButAllowsTouchingEdges(int x, int y, int width, int height, bool expected)
    {
        Assert.Equal(expected, CaptureRegionValidator.Overlaps(new Rectangle(100, 100, 300, 200), new Rectangle(x, y, width, height)));
    }

    [Fact]
    public async Task VisibilityGuardCanCancelBeforeAnyNativeScreenRead()
    {
        using var cancellation = new CancellationTokenSource();
        var called = false;
        var capture = new WindowsRegionCapture((region, token) =>
        {
            called = true;
            cancellation.Cancel();
            return Task.CompletedTask;
        });
        // Invalid desktop coordinates must never reach native validation/capture after cancellation.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.CaptureAsync(new Rectangle(-99999, -99999, 1, 1), cancellation.Token));
        Assert.True(called);
    }
}
