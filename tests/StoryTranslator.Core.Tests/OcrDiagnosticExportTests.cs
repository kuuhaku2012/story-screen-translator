using System.Drawing;
using StoryTranslator.App;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class OcrDiagnosticExportTests
{
    [Fact]
    public void ExportKeepsSourceAndDisplayCoordinatesAndEscapesOcrHtml()
    {
        var root = @"E:\tool_dich_man_hinh\artifacts\diagnostic-tests";
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3XkAAAAASUVORK5CYII=");
        var result = OcrDiagnosticExport.Save(root, png, new Rectangle(-100, 50, 640, 480),
            [new ScreenTextBlock("<script>alert(1)</script>", new RectangleF(10, 20, 90, 30), 20)], true, 123);
        Assert.True(File.Exists(Path.Combine(result, "source.png")));
        var json = File.ReadAllText(Path.Combine(result, "ocr.json"));
        Assert.Contains("Windows.Media.Ocr", json);
        Assert.Contains("SourceBounds", json);
        Assert.Contains("DisplayBounds", json);
        var html = File.ReadAllText(Path.Combine(result, "preview.html"));
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
    }
}
