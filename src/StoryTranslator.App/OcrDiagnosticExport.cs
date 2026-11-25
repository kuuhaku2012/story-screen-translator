using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;
using StoryTranslator.Core;

namespace StoryTranslator.App;

/// <summary>Explicit local export; caller supplies pixels only after user clicks diagnostic capture.</summary>
public static class OcrDiagnosticExport
{
    public static string Save(string root, byte[] png, Rectangle captureRegion,
        IReadOnlyList<ScreenTextBlock> blocks, bool comicMode, double ocrMilliseconds)
    {
        var folder = Path.Combine(Path.GetFullPath(root), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "source.png"), png);
        var panels = SpatialPanelLayout.Arrange(blocks.Select(b => new TranslatedScreenBlock(b.Text, b.Bounds, b.LineHeight)).ToArray(), captureRegion.Size);
        var report = new
        {
            Engine = "Windows.Media.Ocr", ComicMode = comicMode, OcrMilliseconds = ocrMilliseconds,
            CaptureRegion = new { captureRegion.X, captureRegion.Y, captureRegion.Width, captureRegion.Height },
            CoordinateSpace = "Physical source pixels relative to capture origin; display rectangles are current panel-layout predictions, not bubble detection",
            Blocks = blocks.Select((b, i) => new { Id = i + 1, b.Text, b.LineHeight,
                SourceBounds = Box(b.Bounds), DisplayBounds = Box(panels[i]) })
        };
        File.WriteAllText(Path.Combine(folder, "ocr.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        var image = "data:image/png;base64," + Convert.ToBase64String(png);
        string Layer(IEnumerable<RectangleF> boxes, string color) =>
            $"<svg viewBox='0 0 {captureRegion.Width} {captureRegion.Height}'><image href='{image}' width='{captureRegion.Width}' height='{captureRegion.Height}'/>" +
            string.Concat(boxes.Select((b, i) => $"<rect x='{F(b.X)}' y='{F(b.Y)}' width='{F(b.Width)}' height='{F(b.Height)}' fill='none' stroke='{color}' stroke-width='2'/><text x='{F(b.X)}' y='{F(Math.Max(12, b.Y))}' fill='{color}' font-size='14'>{i + 1}</text>")) + "</svg>";
        var rows = string.Concat(blocks.Select((b, i) => $"<tr><td>{i + 1}</td><td>{WebUtility.HtmlEncode(b.Text)}</td></tr>"));
        var html = "<meta charset='utf-8'><title>Local OCR diagnostic</title><style>body{font:16px Segoe UI;background:#222;color:#eee}svg{width:min(90vw,900px);display:block}td{border:1px solid #777;padding:8px;white-space:pre-wrap}</style>" +
            "<h1>OCR cục bộ — không dịch / không gửi API</h1><h2>Box nguồn OCR</h2>" + Layer(blocks.Select(b => b.Bounds), "#ff3030") +
            "<h2>Khung thẻ dự kiến của bố cục hiện tại</h2>" + Layer(panels, "#10cfff") + "<table>" + rows + "</table>";
        File.WriteAllText(Path.Combine(folder, "preview.html"), html);
        return folder;
    }
    private static object Box(RectangleF b) => new { b.X, b.Y, b.Width, b.Height };
    private static string F(float n) => n.ToString("0.##", CultureInfo.InvariantCulture);
}
