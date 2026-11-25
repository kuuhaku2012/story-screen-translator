using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace StoryTranslator.App;

/// <summary>Explicit offline evaluation of user-authorized image fixtures; never screen-captures or sends text.</summary>
internal static class FileOcrEvaluation
{
    public static async Task<int> RunAsync(string manifestPath)
    {
        const string root = @"E:\tool_dich_man_hinh\artifacts\multimodal-evaluation";
        var resultPath = Path.Combine(root, "windows-ocr.json");
        try
        {
            if (!Path.GetFullPath(manifestPath).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Evaluation manifest must be inside the evaluation directory.");
            var inputs = JsonSerializer.Deserialize<Input[]>(await File.ReadAllTextAsync(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            var reports = new List<object>();
            var process = Process.GetCurrentProcess();
            foreach (var comic in new[] { false, true })
            {
                var engine = WindowsOcrEngine.CreateEnglish(comic);
                foreach (var input in inputs)
                {
                    var path = Path.GetFullPath(input.Path);
                    if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Evaluation input is outside the fixture directory.");
                    using var stream = new InMemoryRandomAccessStream();
                    using (var writer = new DataWriter(stream))
                    {
                        writer.WriteBytes(await File.ReadAllBytesAsync(path));
                        await writer.StoreAsync(); await writer.FlushAsync(); _ = writer.DetachStream();
                    }
                    stream.Seek(0);
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    using var frame = new WindowsCapturedFrame(await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied));
                    var times = new List<double>();
                    IReadOnlyList<StoryTranslator.Core.ScreenTextBlock> blocks = [];
                    var cpuStart = process.TotalProcessorTime;
                    for (var repeat = 0; repeat < 3; repeat++)
                    {
                        var started = Stopwatch.GetTimestamp();
                        blocks = await engine.RecognizeLayoutAsync(frame, CancellationToken.None);
                        times.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    }
                    process.Refresh();
                    reports.Add(new { input.Id, ComicMode = comic, Milliseconds = times,
                        CpuMilliseconds = (process.TotalProcessorTime - cpuStart).TotalMilliseconds,
                        PeakWorkingSetMiB = process.PeakWorkingSet64 / 1048576d,
                        Blocks = blocks.Select(b => new { b.Text, X = b.Bounds.X, Y = b.Bounds.Y, Width = b.Bounds.Width, Height = b.Bounds.Height }) });
                }
            }
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new { Status = "completed", Reports = reports }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new { Status = "failed", Error = error.Message }));
            return 1;
        }
    }

    private sealed record Input(string Id, string Path);
}
