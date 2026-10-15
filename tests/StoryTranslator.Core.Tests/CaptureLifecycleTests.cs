using System.Drawing;
using System.Collections.Concurrent;
using StoryTranslator.App;

namespace StoryTranslator.Core.Tests;

public sealed class CaptureLifecycleTests
{
    [Fact]
    public void Start_RejectsAnEmptyRegionWithoutCapturing()
    {
        var source = new FakeFrameSource();
        var worker = new CaptureWorker(source, new DelegateRecognizer(_ => Task.FromResult("")), TimeSpan.FromMilliseconds(10));

        Assert.Throws<ArgumentOutOfRangeException>(() => worker.Start(Rectangle.Empty));
        Assert.Equal(0, source.CaptureCount);
    }

    [Fact]
    public async Task Stop_PreventsFutureCaptureAndRecognition()
    {
        var source = new FakeFrameSource();
        var recognitionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRecognitionToFinish = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new CaptureWorker(source, new DelegateRecognizer(_ =>
        {
            recognitionStarted.TrySetResult();
            return allowRecognitionToFinish.Task;
        }), TimeSpan.FromMilliseconds(20));
        worker.Start(new Rectangle(10, 10, 100, 40));

        await recognitionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        worker.Stop();
        allowRecognitionToFinish.TrySetResult("hello");
        await WaitUntilAsync(() => source.Frames.TryPeek(out var frame) && frame.DisposeCount == 1);
        await Task.Delay(100);

        Assert.Equal(1, source.CaptureCount);
        Assert.Equal(1, Assert.Single(source.Frames).DisposeCount);
    }

    [Fact]
    public async Task Worker_DisposesFrameAfterSuccessfulRecognition()
    {
        var source = new FakeFrameSource();
        var worker = new CaptureWorker(source, new DelegateRecognizer(_ => Task.FromResult("recognized")), TimeSpan.FromMilliseconds(500));
        worker.Start(new Rectangle(0, 0, 20, 20));

        await WaitUntilAsync(() => source.Frames.TryPeek(out var frame) && frame.DisposeCount == 1);
        worker.Stop();

        Assert.Equal(1, Assert.Single(source.Frames).DisposeCount);
    }

    [Fact]
    public async Task Worker_DisposesFrameWhenRecognitionFails()
    {
        var source = new FakeFrameSource();
        var worker = new CaptureWorker(source, new DelegateRecognizer(_ => Task.FromException<string>(new InvalidOperationException("synthetic failure"))), TimeSpan.FromMilliseconds(500));
        worker.Start(new Rectangle(0, 0, 20, 20));

        await WaitUntilAsync(() => source.Frames.TryPeek(out var frame) && frame.DisposeCount == 1);
        worker.Stop();

        Assert.Equal(1, Assert.Single(source.Frames).DisposeCount);
    }

    [Fact]
    public async Task Worker_DisposesFrameAndDiscardsLateResultAfterStop()
    {
        var source = new FakeFrameSource();
        var recognitionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRecognitionToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var published = new List<string>();
        var worker = new CaptureWorker(source, new DelegateRecognizer(async _ =>
        {
            recognitionStarted.TrySetResult();
            await allowRecognitionToFinish.Task;
            return "late dialogue";
        }), TimeSpan.FromMilliseconds(500));
        worker.TextRecognized += (_, text) => published.Add(text);
        worker.Start(new Rectangle(0, 0, 20, 20));

        await recognitionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        worker.Stop();
        allowRecognitionToFinish.TrySetResult();
        await WaitUntilAsync(() => source.Frames.TryPeek(out var frame) && frame.DisposeCount == 1);

        Assert.Empty(published);
        Assert.Equal(1, Assert.Single(source.Frames).DisposeCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(5);
        }
    }

    private sealed class DelegateRecognizer(Func<ICapturedFrame, Task<string>> recognize) : IFrameTextRecognizer
    {
        public Task<string> RecognizeAsync(ICapturedFrame frame, CancellationToken cancellationToken) => recognize(frame);
    }

    private sealed class FakeFrameSource : IRegionFrameSource
    {
        public ConcurrentQueue<FakeFrame> Frames { get; } = new();
        private int _captureCount;
        public int CaptureCount => Volatile.Read(ref _captureCount);

        public Task<ICapturedFrame> CaptureAsync(Rectangle region, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _captureCount);
            var frame = new FakeFrame();
            Frames.Enqueue(frame);
            return Task.FromResult<ICapturedFrame>(frame);
        }
    }

    private sealed class FakeFrame : ICapturedFrame
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
