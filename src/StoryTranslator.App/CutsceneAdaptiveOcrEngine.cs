using System.Diagnostics;
using System.Drawing;
using StoryTranslator.Core;

namespace StoryTranslator.App;

/// <summary>Uses fast native-size OCR when it finds a subtitle; enlarges only missed frames.</summary>
public sealed class CutsceneAdaptiveOcrEngine(
    IFrameLayoutRecognizer normal,
    IFrameLayoutRecognizer enlarged,
    Func<ICapturedFrame, Size> frameSize,
    Action<string, TimeSpan>? timing = null) : IFrameLayoutRecognizer
{
    public async Task<IReadOnlyList<ScreenTextBlock>> RecognizeLayoutAsync(ICapturedFrame frame, CancellationToken token)
    {
        var size = frameSize(frame);
        var started = Stopwatch.GetTimestamp();
        var first = await normal.RecognizeLayoutAsync(frame, token).ConfigureAwait(false);
        timing?.Invoke("ocr-native", Stopwatch.GetElapsedTime(started));
        var selected = CutsceneSubtitleSelector.Select(first, size);
        // Top lyrics may be readable while the lower dialogue is missed by native-size OCR.
        if (selected.Any(IsBottom)) return first;
        token.ThrowIfCancellationRequested();
        started = Stopwatch.GetTimestamp();
        var second = await enlarged.RecognizeLayoutAsync(frame, token).ConfigureAwait(false);
        timing?.Invoke("ocr-upscale-fallback", Stopwatch.GetElapsedTime(started));
        return CutsceneSubtitleSelector.Select(second, size).Any(IsBottom) || selected.Count == 0
            ? second : first;

        bool IsBottom(ScreenTextBlock block) =>
            (block.Bounds.Top + block.Bounds.Bottom) / 2 >= size.Height * .70f;
    }

    public async Task<string> RecognizeAsync(ICapturedFrame frame, CancellationToken token) =>
        string.Join("\n", (await RecognizeLayoutAsync(frame, token).ConfigureAwait(false)).Select(block => block.Text));
}
