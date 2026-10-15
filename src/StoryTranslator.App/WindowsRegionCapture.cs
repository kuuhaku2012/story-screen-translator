using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace StoryTranslator.App;

[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsCapturedFrame : ICapturedFrame
{
    public WindowsCapturedFrame(SoftwareBitmap softwareBitmap)
    {
        SoftwareBitmap = softwareBitmap ?? throw new ArgumentNullException(nameof(softwareBitmap));
    }

    public SoftwareBitmap SoftwareBitmap { get; }

    public void Dispose() => SoftwareBitmap.Dispose();
}

/// <summary>Copies only the requested desktop rectangle using the public GDI screen-copy API.</summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsRegionCapture : IRegionFrameSource
{
    public async Task<ICapturedFrame> CaptureAsync(Rectangle region, CancellationToken cancellationToken)
    {
        ValidateRegion(region);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] bmpBytes = await Task.Run(() => CopyRegionToBmp(region), cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var randomAccessStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(randomAccessStream))
            {
                writer.WriteBytes(bmpBytes);
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
            CryptographicOperations.ZeroMemory(bmpBytes);
        }
    }

    private static byte[] CopyRegionToBmp(Rectangle region)
    {
        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Bmp);
        return stream.ToArray();
    }

    private static void ValidateRegion(Rectangle region)
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);
        var virtualDesktop = new Rectangle(left, top, width, height);
        if (!CaptureRegionValidator.IsWithin(region, virtualDesktop))
        {
            throw new ArgumentOutOfRangeException(nameof(region), "The capture region and virtual desktop metrics must be positive and fully within the virtual desktop.");
        }
    }

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
