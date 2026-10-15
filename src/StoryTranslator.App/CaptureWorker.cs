using System.Diagnostics;
using System.Drawing;
using StoryTranslator.Core;

namespace StoryTranslator.App;

public interface ICapturedFrame : IDisposable;

public interface IRegionFrameSource
{
    Task<ICapturedFrame> CaptureAsync(Rectangle region, CancellationToken cancellationToken);
}

public interface IFrameTextRecognizer
{
    Task<string> RecognizeAsync(ICapturedFrame frame, CancellationToken cancellationToken);
}

/// <summary>Sequential low-rate capture loop. Stop cancels promptly; an in-flight OCR call is allowed to finish and its result is discarded.</summary>
public sealed class CaptureWorker : IScreenCapture, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IRegionFrameSource _frameSource;
    private readonly IFrameTextRecognizer _recognizer;
    private readonly TimeSpan _sampleInterval;
    private readonly Action<string>? _diagnostic;
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;

    public CaptureWorker(
        IRegionFrameSource frameSource,
        IFrameTextRecognizer recognizer,
        TimeSpan? sampleInterval = null,
        Action<string>? diagnostic = null)
    {
        _frameSource = frameSource ?? throw new ArgumentNullException(nameof(frameSource));
        _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
        _sampleInterval = sampleInterval ?? TimeSpan.FromMilliseconds(500);
        if (_sampleInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleInterval));
        }

        _diagnostic = diagnostic;
    }

    public event EventHandler<string>? TextRecognized;

    public void Start(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "The capture region must have positive width and height.");
        }

        lock (_gate)
        {
            if (_runCancellation is not null)
            {
                return;
            }

            if (_runTask is { IsCompleted: false })
            {
                throw new InvalidOperationException("The previous capture session is still stopping.");
            }

            var cancellation = new CancellationTokenSource();
            _runCancellation = cancellation;
            _runTask = RunAsync(region, cancellation);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _runCancellation?.Cancel();
            _runCancellation = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? runTask;
        Stop();
        lock (_gate)
        {
            runTask = _runTask;
        }

        if (runTask is not null)
        {
            await runTask.ConfigureAwait(false);
        }
    }

    private async Task RunAsync(Rectangle region, CancellationTokenSource session)
    {
        var token = session.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var started = Stopwatch.GetTimestamp();
                try
                {
                    using var frame = await _frameSource.CaptureAsync(region, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    var recognized = await _recognizer.RecognizeAsync(frame, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    PublishIfCurrent(session, recognized);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _diagnostic?.Invoke($"capture_pipeline_error:{ex.GetType().Name}");
                }

                var remaining = _sampleInterval - Stopwatch.GetElapsedTime(started);
                if (remaining > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(remaining, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_runCancellation, session))
                {
                    _runCancellation = null;
                }
            }

            session.Dispose();
        }
    }

    private void PublishIfCurrent(CancellationTokenSource session, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (_gate)
        {
            if (ReferenceEquals(_runCancellation, session) && !session.IsCancellationRequested)
            {
                TextRecognized?.Invoke(this, text);
            }
        }
    }
}
