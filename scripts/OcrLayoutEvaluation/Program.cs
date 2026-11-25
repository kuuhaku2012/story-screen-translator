using System.Drawing;
using System.Text.Json;
using StoryTranslator.Core;

const string root = @"E:\tool_dich_man_hinh\artifacts\multimodal-evaluation";
using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "rapid-v6-small.json")));
var reports = new List<object>();
foreach (var comic in new[] { false, true })
foreach (var report in source.RootElement.GetProperty("Reports").EnumerateArray())
{
    var lines = report.GetProperty("Blocks").EnumerateArray().Select(b => new[]
    {
        new OcrWordBox(b.GetProperty("Text").GetString()!, new RectangleF(
            b.GetProperty("X").GetSingle(), b.GetProperty("Y").GetSingle(),
            b.GetProperty("Width").GetSingle(), b.GetProperty("Height").GetSingle()))
    });
    var blocks = SpatialTextLayout.Group(lines, comic);
    reports.Add(new { Id = report.GetProperty("Id").GetString(), ComicMode = comic,
        Blocks = blocks.Select(b => new { b.Text, X = b.Bounds.X, Y = b.Bounds.Y, Width = b.Bounds.Width, Height = b.Bounds.Height }) });
}
var output = args.Length > 0 ? args[0] : Path.Combine(root, "rapid-production-layout.json");
File.WriteAllText(output, JsonSerializer.Serialize(new { Reports = reports }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Production grouping evaluated on {reports.Count} mode/input pairs.");
