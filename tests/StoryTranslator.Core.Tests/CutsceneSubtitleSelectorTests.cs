using System.Drawing;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class CutsceneSubtitleSelectorTests
{
    [Fact]
    public void KeepsCenteredSubtitleAndDropsPlayerChrome()
    {
        var blocks = new[]
        {
            Block("Wuthering Waves 3.7", 20, 20, 500),
            Block("For a hundred years, I rebuilt countless landscapes and cultures within the Capsule.", 342, 963, 1234),
            Block("Cinematic", 248, 1042, 78)
        };

        var selected = CutsceneSubtitleSelector.Select(blocks, new Size(1920, 1080));

        Assert.Single(selected);
        Assert.Equal(blocks[1], selected[0]);
    }

    [Fact]
    public void RejoinsSubtitleSplitIntoTwoOcrBlocks()
    {
        var blocks = new[]
        {
            Block("A thousand tim", 582, 906, 192),
            Block("over, and I'd still see it through to the end", 811, 906, 526)
        };

        var selected = Assert.Single(CutsceneSubtitleSelector.Select(blocks, new Size(1920, 1080)));
        Assert.Equal("A thousand tim over, and I'd still see it through to the end", selected.Text);
        Assert.Equal(582, selected.Bounds.Left);
        Assert.Equal(1337, selected.Bounds.Right);
    }

    [Fact]
    public void FindsTopLyricsAndLeavesPortraitComicLayoutUntouched()
    {
        var blocks = new[]
        {
            Block("Wuthering Waves", 18, 20, 166),
            Block("Don't stop. Don't look back. Memories never stay where they began", 222, 32, 1052)
        };

        Assert.Equal(blocks[1], Assert.Single(CutsceneSubtitleSelector.Select(blocks, new Size(1920, 1080))));
        Assert.Equal(blocks, CutsceneSubtitleSelector.Select(blocks, new Size(800, 1200)));
    }

    [Fact]
    public void BottomDialogueTakesPriorityOverSimultaneousTopLyrics()
    {
        var lyric = Block("And all this worldly splendor fades like a dream", 120, 70, 650);
        var dialogue = Block("Back then, even in death, none of you regretted following me", 420, 900, 1050);

        var selected = CutsceneSubtitleSelector.Select([lyric, dialogue], new Size(1920, 1080));

        Assert.Equal(dialogue, Assert.Single(selected));
    }
    private static ScreenTextBlock Block(string text, float x, float y, float width)
        => new(text, new RectangleF(x, y, width, 42), 42);
}
