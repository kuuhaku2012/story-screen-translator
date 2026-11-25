using System.Drawing;
using StoryTranslator.App;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class CutsceneAdaptiveOcrEngineTests
{
    [Fact]
    public async Task RecognizedSubtitleSkipsEnlargedPass()
    {
        var normal = new Stub([new("This is my cause", new RectangleF(830, 890, 210, 40), 40)]);
        var enlarged = new Stub([]);
        var engine = new CutsceneAdaptiveOcrEngine(normal, enlarged, _ => new Size(1920, 1080));

        var result = await engine.RecognizeLayoutAsync(new Frame(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(1, normal.Calls);
        Assert.Equal(0, enlarged.Calls);
    }

    [Fact]
    public async Task UnrelatedNormalTextFallsBackToEnlargedSubtitle()
    {
        var normal = new Stub([new("YouTube", new RectangleF(20, 20, 80, 20), 20)]);
        var enlarged = new Stub([new("How they must've hated you", new RectangleF(700, 900, 500, 30), 30)]);
        var engine = new CutsceneAdaptiveOcrEngine(normal, enlarged, _ => new Size(1920, 1080));

        var result = await engine.RecognizeLayoutAsync(new Frame(), CancellationToken.None);

        Assert.Equal("How they must've hated you", result[0].Text);
        Assert.Equal(1, normal.Calls);
        Assert.Equal(1, enlarged.Calls);
    }

    [Fact]
    public async Task TopLyricAloneDoesNotSuppressFallbackForBottomDialogue()
    {
        var normal = new Stub([new("And all this worldly splendor fades like a dream",
            new RectangleF(120, 70, 650, 28), 28)]);
        var enlarged = new Stub([new("And all this worldly splendor fades like a dream",
                new RectangleF(120, 70, 650, 28), 28),
            new("Back then, even in death, none of you regretted following me",
                new RectangleF(420, 900, 1050, 30), 30)]);
        var engine = new CutsceneAdaptiveOcrEngine(normal, enlarged, _ => new Size(1920, 1080));

        var result = await engine.RecognizeLayoutAsync(new Frame(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, enlarged.Calls);
    }
    private sealed class Frame : ICapturedFrame { public void Dispose() { } }
    private sealed class Stub(IReadOnlyList<ScreenTextBlock> blocks) : IFrameLayoutRecognizer
    {
        public int Calls;
        public Task<IReadOnlyList<ScreenTextBlock>> RecognizeLayoutAsync(ICapturedFrame frame, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(blocks);
        }
        public Task<string> RecognizeAsync(ICapturedFrame frame, CancellationToken token) =>
            throw new NotSupportedException();
    }
}
