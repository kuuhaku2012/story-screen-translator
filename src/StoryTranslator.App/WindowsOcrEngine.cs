using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace StoryTranslator.App;

/// <summary>Windows local OCR adapter. Windows.Media.Ocr requires MSIX package identity on desktop.</summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsOcrEngine : IFrameTextRecognizer
{
    private readonly OcrEngine _engine;

    private WindowsOcrEngine(OcrEngine engine) => _engine = engine;

    public static WindowsOcrEngine CreateEnglish()
    {
        EnsurePackageIdentity();
        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"));
        if (engine is null)
        {
            throw new InvalidOperationException("The Windows English OCR language pack is not installed.");
        }

        return new WindowsOcrEngine(engine);
    }

    public async Task<string> RecognizeAsync(ICapturedFrame frame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frame);
        cancellationToken.ThrowIfCancellationRequested();
        if (frame is not WindowsCapturedFrame windowsFrame)
        {
            throw new ArgumentException("Windows OCR requires a Windows captured frame.", nameof(frame));
        }

        var result = await _engine.RecognizeAsync(windowsFrame.SoftwareBitmap);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Text;
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
