using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using StoryTranslator.Core;
using System.Drawing;

namespace StoryTranslator.App;

/// <summary>Windows local OCR adapter. Windows.Media.Ocr requires MSIX package identity on desktop.</summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsOcrEngine : IFrameLayoutRecognizer
{
    private readonly OcrEngine _engine;
    private readonly bool _comicMode;

    private WindowsOcrEngine(OcrEngine engine, bool comicMode) { _engine = engine; _comicMode = comicMode; }

    public static WindowsOcrEngine CreateEnglish(bool comicMode = false)
    {
        EnsurePackageIdentity();
        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"));
        if (engine is null)
        {
            throw new InvalidOperationException("The Windows English OCR language pack is not installed.");
        }

        return new WindowsOcrEngine(engine, comicMode);
    }

    public async Task<string> RecognizeAsync(ICapturedFrame frame, CancellationToken cancellationToken)
        => string.Join("\n", (await RecognizeLayoutAsync(frame, cancellationToken)).Select(b => b.Text));

    public async Task<IReadOnlyList<ScreenTextBlock>> RecognizeLayoutAsync(ICapturedFrame frame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frame);
        cancellationToken.ThrowIfCancellationRequested();
        if (frame is not WindowsCapturedFrame windowsFrame)
        {
            throw new ArgumentException("Windows OCR requires a Windows captured frame.", nameof(frame));
        }

        var original = windowsFrame.SoftwareBitmap;
        var factor = _comicMode ? 2 : 1;
        var size = OcrImageSizing.Fit(original.PixelWidth * factor, original.PixelHeight * factor, (int)OcrEngine.MaxImageDimension);
        SoftwareBitmap? scaled = null;
        try
        {
            if (size.Width != original.PixelWidth || size.Height != original.PixelHeight)
            {
                using var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);
                encoder.SetSoftwareBitmap(original);
                await encoder.FlushAsync();
                cancellationToken.ThrowIfCancellationRequested();
                stream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                scaled = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    new BitmapTransform { ScaledWidth = (uint)size.Width, ScaledHeight = (uint)size.Height },
                    ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _engine.RecognizeAsync(scaled ?? original);
            cancellationToken.ThrowIfCancellationRequested();
            var scaleX = (float)original.PixelWidth / size.Width;
            var scaleY = (float)original.PixelHeight / size.Height;
            return SpatialTextLayout.Group(result.Lines.Select(line => line.Words.Select(word =>
                new OcrWordBox(word.Text, new RectangleF((float)word.BoundingRect.X * scaleX,
                    (float)word.BoundingRect.Y * scaleY, (float)word.BoundingRect.Width * scaleX,
                    (float)word.BoundingRect.Height * scaleY)))), _comicMode);
        }
        finally { scaled?.Dispose(); }
    }

    private static void EnsurePackageIdentity()
    {
        uint length = 0;
        var result = GetCurrentPackageFullName(ref length, null);
        const int ErrorInsufficientBuffer = 122;
        const int AppModelErrorNoPackage = 15700;
        if (result == ErrorInsufficientBuffer || result == 0)
        {
            return;
        }

        if (result == AppModelErrorNoPackage)
        {
            throw new InvalidOperationException("Windows OCR requires an MSIX package identity; the current unpackaged app is not eligible.");
        }

        throw new InvalidOperationException($"Unable to verify Windows OCR package identity (error {result}).");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);
}
