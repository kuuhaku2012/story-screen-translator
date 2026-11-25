using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace StoryTranslator.App;

/// <summary>Creates a disposable in-memory BMP containing only a fixed synthetic OCR sample.</summary>
[SupportedOSPlatform("windows10.0.17763.0")]
internal static class SyntheticOcrFrame
{
    public static async Task<WindowsCapturedFrame> CreateAsync(CancellationToken cancellationToken, int width = 760, int height = 180, int fontSize = 52)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes;
        using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var stream = new MemoryStream())
        {
            graphics.Clear(Color.White);
            graphics.DrawString(OcrSmokeTestRunner.ExpectedText, font, Brushes.Black, new PointF(18, 42));
            bitmap.Save(stream, ImageFormat.Bmp);
            bytes = stream.ToArray();
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var randomAccessStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(randomAccessStream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                _ = writer.DetachStream();
            }

            randomAccessStream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            if (cancellationToken.IsCancellationRequested)
            {
                softwareBitmap.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return new WindowsCapturedFrame(softwareBitmap);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public static async Task<string> RecognizeOversizedAsync(CancellationToken token)
    {
        var width = checked((int)Windows.Media.Ocr.OcrEngine.MaxImageDimension + 128);
        using var frame = await CreateAsync(token, width, 480, 96);
        return await WindowsOcrEngine.CreateEnglish().RecognizeAsync(frame, token);
    }
}
