using System.Drawing;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class SpatialTextLayoutTests
{
    [Fact]
    public void ComicOverlappingCenteredLinesStayTogether()
    {
        var blocks = SpatialTextLayout.Group(new[] {
            new[] { Word("YOU",20,0,60,40) },
            new[] { Word("HEAR ME",0,28,100,40) },
            new[] { Word("KABURA?",5,57,90,36) }
        }, comicMode: true);
        Assert.Single(blocks);
        Assert.Equal("YOU\nHEAR ME\nKABURA?",blocks[0].Text);
    }

    [Fact]
    public void ComicHeadingDoesNotAbsorbDialogue()
    {
        var blocks = SpatialTextLayout.Group(new[] {
            new[] { Word("HEADING",0,0,600,40) },
            new[] { Word("WHAT",260,65,55,28) },
            new[] { Word("TOOK YOU",240,90,95,28) }
        }, comicMode: true);
        Assert.Equal(2,blocks.Count);
        Assert.Equal("WHAT\nTOOK YOU",blocks[1].Text);
    }

    [Fact]
    public void ComicShortPronounAndSeparateColumnsArePreserved()
    {
        var blocks = SpatialTextLayout.Group(new[] {
            new[] { Word("I",30,0,9,20) },
            new[] { Word("KNOW",10,18,60,20) },
            new[] { Word("!!",25,36,20,20) },
            new[] { Word("OTHER",200,0,60,20) },
            new[] { Word("COLUMN",200,20,60,20) }
        }, comicMode: true);
        Assert.Equal(2,blocks.Count);
        Assert.Contains(blocks,b=>b.Text=="I\nKNOW\n!!");
        Assert.Contains(blocks,b=>b.Text=="OTHER\nCOLUMN");
    }
    private static OcrWordBox Word(string text, float x, float y, float width = 100, float height = 20) => new(text, new RectangleF(x, y, width, height));

    [Fact]
    public void WideGapInOneOcrLineIsSplitIntoSeparateColumns()
    {
        var blocks = SpatialTextLayout.Group(new[] { new[] { Word("Left", 10, 10), Word("Right", 400, 10) } });
        Assert.Equal(2, blocks.Count);
        Assert.Equal("Left", blocks[0].Text);
        Assert.Equal(400, blocks[1].Bounds.X);
    }

    [Fact]
    public void NearbyAlignedLinesFormParagraphWithoutMergingOtherColumn()
    {
        var blocks = SpatialTextLayout.Group(new[]
        {
            new[] { Word("First", 10, 10, 200), Word("Other", 400, 10, 150) },
            new[] { Word("Second", 10, 36, 200), Word("Column", 400, 36, 150) },
            new[] { Word("Last", 10, 62, 70) }
        });
        Assert.Equal(2, blocks.Count);
        Assert.Equal("First\nSecond\nLast", blocks[0].Text);
        Assert.Equal("Other\nColumn", blocks[1].Text);
        Assert.True(blocks[0].Bounds.Right < blocks[1].Bounds.Left);
    }

    [Fact]
    public void HeadingAndDistantLabelAreNotJoinedIntoParagraph()
    {
        var blocks = SpatialTextLayout.Group(new[]
        {
            new[] { Word("Heading", 10, 10, 200, 36) },
            new[] { Word("Paragraph", 10, 53, 200, 18) },
            new[] { Word("Label", 10, 130, 80, 18) }
        });
        Assert.Equal(3, blocks.Count);
    }
}
