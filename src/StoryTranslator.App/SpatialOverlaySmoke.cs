using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StoryTranslator.Core;
using Brushes = System.Windows.Media.Brushes;
using WpfRect = System.Windows.Rect;

namespace StoryTranslator.App;

public static class SpatialOverlaySmoke
{
    public static Task<int> RunAsync(string path) => OcrSmokeTestRunner.RunAsync(path, token => RecognizeAsync(path, token));

    private static async Task<string> RecognizeAsync(string path, CancellationToken token)
    {
        var canvas = new Canvas();
        AddSource(canvas, "STORY TEXT 123", 24, 24, 32);
        AddSource(canvas, "RIGHT TEXT 456", 510, 24, 32);
        AddSource(canvas, "A short paragraph.\nSecond sample line.", 24, 125, 25);
        AddSource(canvas, "ANOTHER AREA", 510, 175, 28);
        var source = new Window { Left = 20, Top = 20, Width = 980, Height = 340, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, Topmost = true, Background = Brushes.LightSkyBlue, Content = canvas };
        var overlay = new TranslationOverlay();
        SpatialTranslationPipeline? pipeline = null;
        try
        {
            source.Show(); source.UpdateLayout();
            var handle = new WindowInteropHelper(source).Handle;
            if (!GetWindowRect(handle, out var bounds)) throw new InvalidOperationException("Cannot locate owned layout sample.");
            var region = new Rectangle(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            overlay.SetRegion(region);
            var capture = new WindowsRegionCapture(async (_, cancellation) =>
            {
                await source.Dispatcher.InvokeAsync(() =>
                {
                    overlay.BeginCapture();
                    if (DwmFlush() < 0) throw new InvalidOperationException("DWM failed.");
                    if (WindowFromPoint(new NativePoint { X = region.Left + 10, Y = region.Top + 10 }) != handle)
                        throw new InvalidOperationException("Owned sample is covered; refusing capture.");
                });
                cancellation.ThrowIfCancellationRequested();
            }, async () => { await overlay.Dispatcher.InvokeAsync(() => overlay.EndCapture()); });
            using var frame = await capture.CaptureAsync(region, token);
            var layout = await WindowsOcrEngine.CreateEnglish().RecognizeLayoutAsync(frame, token);
            var left = layout.FirstOrDefault(b => b.Text.Contains("STORY TEXT 123"));
            var right = layout.FirstOrDefault(b => b.Text.Contains("RIGHT TEXT 456"));
            if (left is null || right is null || ReferenceEquals(left, right) || left.Bounds.Right >= right.Bounds.Left)
                throw new InvalidOperationException("OCR merged independent columns or lost text.");
            pipeline = new SpatialTranslationPipeline(new SampleTranslator(), new GlossaryProtector([]), (blocks, allowed) => overlay.ShowBlocks(blocks, allowed));
            pipeline.Enable(token);
            await pipeline.ObserveAsync(layout);
            await pipeline.ObserveAsync(layout);
            await overlay.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            overlay.UpdateLayout();
            if (overlay.RenderedPanelCount != layout.Count || layout.Count < 4)
                throw new InvalidOperationException("Spatial panels were not rendered individually.");

            var width = Math.Max(1, (int)Math.Ceiling(source.ActualWidth));
            var height = Math.Max(1, (int)Math.Ceiling(source.ActualHeight));
            var original = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            original.Render(source);
            var cards = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            cards.Render(overlay);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawImage(original, new WpfRect(0, 0, width, height));
                drawing.DrawImage(cards, new WpfRect(0, 0, width, height));
            }
            var combined = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            combined.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(combined));
            using (var file = File.Create(Path.ChangeExtension(path, ".png"))) encoder.Save(file);
            return string.Join("\n", layout.Select(b => b.Text));
        }
        finally { pipeline?.TurnOff(); overlay.Close(); source.Close(); }
    }

    private static void AddSource(Canvas canvas, string value, double left, double top, double size)
    {
        var text = new TextBlock { Text = value, FontSize = size, Foreground = Brushes.Black, Width = 420, TextWrapping = TextWrapping.Wrap };
        Canvas.SetLeft(text, left); Canvas.SetTop(text, top); canvas.Children.Add(text);
    }

    private sealed class SampleTranslator : IBatchTextTranslator
    {
        public Task<IReadOnlyList<string>> TranslateBatchAsync(IReadOnlyList<string> texts, CancellationToken token) => Task.FromResult<IReadOnlyList<string>>(texts.Select(text =>
            text.Contains("STORY") ? "Vùng bên trái: BẢN DỊCH RÕ CHỮ" : text.Contains("RIGHT") ? "Vùng bên phải: ĐÚNG VỊ TRÍ" :
            text.Contains("ANOTHER") ? "MỘT VÙNG CHỮ KHÁC" : "Một đoạn văn mẫu.\nDòng thứ hai vẫn cùng đoạn.").ToArray());
    }
    [StructLayout(LayoutKind.Sequential)] private struct Bounds { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr handle, out Bounds bounds);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
