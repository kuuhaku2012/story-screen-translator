using System.IO;
using System.Text.Json;

namespace StoryTranslator.App;

public enum OcrSmokeCommandKind
{
    Normal,
    SmokeTest,
    Invalid
}

public sealed record OcrSmokeCommandLine(OcrSmokeCommandKind Kind, string? ResultPath, string? Error);

public static class OcrSmokeTestCommand
{
    public static OcrSmokeCommandLine Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0) return new OcrSmokeCommandLine(OcrSmokeCommandKind.Normal, null, null);
        if (args.Length == 2 && args[0] == "--ocr-smoke-test" && !string.IsNullOrWhiteSpace(args[1]))
        {
            return new OcrSmokeCommandLine(OcrSmokeCommandKind.SmokeTest, args[1], null);
        }

        return new OcrSmokeCommandLine(
            OcrSmokeCommandKind.Invalid,
            null,
            "Usage: StoryTranslator.App.exe [--ocr-smoke-test <result-path>]");
    }
}

public sealed record OcrSmokeResult(string Status, string ExpectedText, string? RecognizedText, string? Error);

public static class OcrSmokeTestRunner
{
    public const string ExpectedText = "STORY TEXT 123";

    public static Task<int> RunProductionAsync(string resultPath, CancellationToken cancellationToken = default) =>
        RunAsync(resultPath, RecognizeSyntheticSampleAsync, cancellationToken);

    public static async Task<int> RunAsync(
        string resultPath,
        Func<CancellationToken, Task<string>> recognize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultPath);
        ArgumentNullException.ThrowIfNull(recognize);

        OcrSmokeResult result;
        try
        {
            var recognized = await recognize(cancellationToken).ConfigureAwait(false);
            var passed = Normalize(recognized).Contains(ExpectedText, StringComparison.OrdinalIgnoreCase);
            result = new OcrSmokeResult(
                passed ? "passed" : "failed",
                ExpectedText,
                recognized,
                passed ? null : $"OCR output did not contain the expected synthetic text '{ExpectedText}'.");
        }
        catch (Exception exception)
        {
            result = new OcrSmokeResult("failed", ExpectedText, null, exception.Message);
        }

        try
        {
            var fullPath = Path.GetFullPath(resultPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(fullPath, json, CancellationToken.None).ConfigureAwait(false);
            return result.Status == "passed" ? 0 : 1;
        }
        catch
        {
            return 2;
        }
    }

    private static async Task<string> RecognizeSyntheticSampleAsync(CancellationToken cancellationToken)
    {
        var engine = WindowsOcrEngine.CreateEnglish();
        using var frame = await SyntheticOcrFrame.CreateAsync(cancellationToken).ConfigureAwait(false);
        return await engine.RecognizeAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    private static string Normalize(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
