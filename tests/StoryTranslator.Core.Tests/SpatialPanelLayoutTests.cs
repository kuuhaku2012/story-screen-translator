using System.Drawing;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class SpatialPanelLayoutTests
{
    [Fact]
    public void TinySourceDoesNotBecomeNinetyPixelCard()
    {
        var panels = SpatialPanelLayout.Arrange([
            new TranslatedScreenBlock("của tôi", new RectangleF(289,746,19,10),10)
        ], new SizeF(662,919));
        Assert.InRange(panels[0].Width,19,27);
        Assert.InRange(panels[0].Height,10,18);
    }

    [Fact]
    public void CloseCardsHaveNoPaddingOverlap()
    {
        var panels = SpatialPanelLayout.Arrange([
            new TranslatedScreenBlock("A",new RectangleF(20,20,40,20),20),
            new TranslatedScreenBlock("B",new RectangleF(64,20,40,20),20)
        ],new SizeF(200,100));
        Assert.True(panels[0].Right <= panels[1].Left);
    }

    [Fact]
    public void EdgePaddingStaysInsideAreaAndDoesNotMoveSourceAnchor()
    {
        var panels = SpatialPanelLayout.Arrange([
            new TranslatedScreenBlock("Edge",new RectangleF(0,0,20,10),10),
            new TranslatedScreenBlock("End",new RectangleF(90,90,10,10),10)
        ],new SizeF(100,100));
        Assert.All(panels,p=>Assert.True(new RectangleF(0,0,100,100).Contains(p)));
        Assert.Equal(0,panels[0].Left);
        Assert.Equal(100,panels[1].Right);
    }
    [Fact]
    public void PanelsRemainAtOwnBlocksAndDoNotExpandIntoNeighbors()
    {
        var blocks = new[]
        {
            new TranslatedScreenBlock("Trái", new RectangleF(20, 30, 90, 20), 18),
            new TranslatedScreenBlock("Phải", new RectangleF(400, 30, 100, 20), 18),
            new TranslatedScreenBlock("Dưới", new RectangleF(20, 65, 150, 20), 18)
        };
        var panels = SpatialPanelLayout.Arrange(blocks, new SizeF(800, 600));
        Assert.Equal(3, panels.Count);
        Assert.InRange(panels[0].Left, 16, 20);
        Assert.InRange(panels[1].Left, 396, 400);
        Assert.True(panels[0].Bottom < panels[2].Top);
        Assert.All(panels, panel => Assert.True(new RectangleF(0, 0, 800, 600).Contains(panel)));
    }
}
