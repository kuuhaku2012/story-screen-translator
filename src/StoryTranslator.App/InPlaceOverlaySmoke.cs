using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Brushes = System.Windows.Media.Brushes;

namespace StoryTranslator.App;

/// <summary>Captures only a solid, owned test window; no provider call or user key.</summary>
public static class InPlaceOverlaySmoke
{
    public static Task<int> RunAsync(string path) => OcrSmokeTestRunner.RunAsync(path, RecognizeAsync);

    private static async Task<string> RecognizeAsync(CancellationToken token)
    {
        var source = new Window
        {
            Left = 30, Top = 30, Width = 520, Height = 180,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            Topmost = true, Background = Brushes.LightSkyBlue,
            Content = new TextBlock { Text = OcrSmokeTestRunner.ExpectedText, FontSize = 36,
                Foreground = Brushes.Black, Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Center }
        };
        var overlay = new TranslationOverlay();
        try
        {
            source.Show();
            source.UpdateLayout();
            var handle = new WindowInteropHelper(source).Handle;
            if (!GetWindowRect(handle, out var bounds)) throw new InvalidOperationException("Cannot locate owned sample window.");
            var region = new Rectangle(bounds.Left + 8, bounds.Top + 8, bounds.Right - bounds.Left - 16, bounds.Bottom - bounds.Top - 16);
            overlay.SetRegion(region);
            overlay.TryShowTranslation("OVERLAY ONLY 999");
            Flush();
            if (!GetWindowRect(new WindowInteropHelper(overlay).Handle, out var placed) ||
                placed.Left != region.Left || placed.Top != region.Top ||
                placed.Right - placed.Left != region.Width || placed.Bottom - placed.Top != region.Height)
                throw new InvalidOperationException("Overlay does not match the requested physical rectangle.");

            using (var pixel = new Bitmap(1, 1))
            {
                using (var graphics = Graphics.FromImage(pixel))
                    graphics.CopyFromScreen(region.Left + 4, region.Top + 4, 0, 0, new System.Drawing.Size(1, 1));
                var actual = pixel.GetPixel(0, 0);
                var expected = Color.LightSkyBlue;
                if (Math.Abs(actual.R - expected.R) > 8 || Math.Abs(actual.G - expected.G) > 8 || Math.Abs(actual.B - expected.B) > 8)
                    throw new InvalidOperationException("Overlay background is not transparent over the owned sample.");
            }

            var capture = new WindowsRegionCapture(async (_, cancellation) =>
            {
                await overlay.Dispatcher.InvokeAsync(() =>
                {
                    overlay.BeginCapture();
                    Flush();
                    var center = new NativePoint { X = region.Left + region.Width / 2, Y = region.Top + region.Height / 2 };
                    if (WindowFromPoint(center) != handle) throw new InvalidOperationException("Owned sample is covered; refusing to capture desktop content.");
                });
                cancellation.ThrowIfCancellationRequested();
            }, async () => { await overlay.Dispatcher.InvokeAsync(() => overlay.EndCapture()); });
            using var frame = await capture.CaptureAsync(region, token);
            if (!overlay.IsVisible) throw new InvalidOperationException("Translation was not restored after capture.");
            return await WindowsOcrEngine.CreateEnglish().RecognizeAsync(frame, token);
        }
        finally { overlay.Close(); source.Close(); }
    }

    private static void Flush() { if (DwmFlush() < 0) throw new InvalidOperationException("DWM synchronization failed."); }
    [StructLayout(LayoutKind.Sequential)] private struct Bounds { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out Bounds bounds);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
