using System.Drawing;
using StoryTranslator.App;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class SpatialCaptureWorkerTests
{
    [Fact]
    public async Task LayoutModePublishesCoordinatesWithoutCallingFlatTextRecognizerAndDisposesFrame()
    {
        var source = new Source();
        var recognizer = new Recognizer();
        IReadOnlyList<ScreenTextBlock>? received = null;
        CaptureWorker? worker = null;
        worker = new CaptureWorker(source, recognizer);
        worker.LayoutRecognized += (_, blocks) => { received = blocks; worker.Stop(); };
        worker.Start(new Rectangle(0, 0, 800, 600));
        await worker.DisposeAsync();
        Assert.NotNull(received);
        Assert.Equal(400, Assert.Single(received).Bounds.X);
        Assert.Equal(0, recognizer.FlatCalls);
        Assert.True(source.Frame.Disposed);
    }

    private sealed class Frame : ICapturedFrame { public bool Disposed; public void Dispose() => Disposed = true; }
    private sealed class Source : IRegionFrameSource
    {
        public Frame Frame = new();
        public Task<ICapturedFrame> CaptureAsync(Rectangle region, CancellationToken token) => Task.FromResult<ICapturedFrame>(Frame);
    }
    private sealed class Recognizer : IFrameLayoutRecognizer
    {
        public int FlatCalls;
        public Task<string> RecognizeAsync(ICapturedFrame frame, CancellationToken token) { FlatCalls++; throw new InvalidOperationException("Wrong path"); }
        public Task<IReadOnlyList<ScreenTextBlock>> RecognizeLayoutAsync(ICapturedFrame frame, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ScreenTextBlock>>([new("Right", new RectangleF(400, 10, 100, 20), 20)]);
    }
}
