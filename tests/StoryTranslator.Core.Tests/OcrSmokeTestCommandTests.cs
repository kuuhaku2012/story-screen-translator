using System.Text.Json;
using StoryTranslator.App;

namespace StoryTranslator.Core.Tests;

public sealed class OcrSmokeTestCommandTests
{
    [Fact]
    public void EmptyArguments_SelectNormalStartup()
    {
        var parsed = OcrSmokeTestCommand.Parse([]);

        Assert.Equal(OcrSmokeCommandKind.Normal, parsed.Kind);
        Assert.Null(parsed.ResultPath);
        Assert.Null(parsed.Error);
    }

    [Fact]
    public void SmokeArgumentRequiresExactlyOneResultPath()
    {
        var parsed = OcrSmokeTestCommand.Parse(["--ocr-smoke-test", "result.json"]);

        Assert.Equal(OcrSmokeCommandKind.SmokeTest, parsed.Kind);
        Assert.Equal("result.json", parsed.ResultPath);
        Assert.Null(parsed.Error);
        Assert.Equal(OcrSmokeCommandKind.Invalid, OcrSmokeTestCommand.Parse(["--ocr-smoke-test"]).Kind);
        Assert.Equal(OcrSmokeCommandKind.Invalid, OcrSmokeTestCommand.Parse(["--unknown"]).Kind);
    }

    [Fact]
    public async Task RunnerWritesSuccessForExpectedSyntheticText()
    {
        var resultPath = GetResultPath("success");

        var exitCode = await OcrSmokeTestRunner.RunAsync(resultPath, _ => Task.FromResult("STORY TEXT 123"));
        var result = JsonSerializer.Deserialize<OcrSmokeResult>(await File.ReadAllTextAsync(resultPath));

        Assert.Equal(0, exitCode);
        Assert.NotNull(result);
        Assert.Equal("passed", result.Status);
        Assert.Equal("STORY TEXT 123", result.RecognizedText);
    }

    [Fact]
    public async Task RunnerWritesFailureWhenSyntheticTextDoesNotMatch()
    {
        var resultPath = GetResultPath("mismatch");

        var exitCode = await OcrSmokeTestRunner.RunAsync(resultPath, _ => Task.FromResult("unrelated text"));
        var result = JsonSerializer.Deserialize<OcrSmokeResult>(await File.ReadAllTextAsync(resultPath));

        Assert.Equal(1, exitCode);
        Assert.NotNull(result);
        Assert.Equal("failed", result.Status);
        Assert.Contains("STORY TEXT 123", result.Error);
    }

    [Fact]
    public async Task RunnerReportsPackageIdentityFailureWithoutFailingOpen()
    {
        var resultPath = GetResultPath("no-package-identity");

        var exitCode = await OcrSmokeTestRunner.RunAsync(
            resultPath,
            _ => Task.FromException<string>(new InvalidOperationException("Windows OCR requires an MSIX package identity.")));
        var result = JsonSerializer.Deserialize<OcrSmokeResult>(await File.ReadAllTextAsync(resultPath));

        Assert.Equal(1, exitCode);
        Assert.NotNull(result);
        Assert.Equal("failed", result.Status);
        Assert.Contains("package identity", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionWindowsOcrFailsClosedWhenTestHostHasNoPackageIdentity()
    {
        var error = Assert.Throws<InvalidOperationException>(() => WindowsOcrEngine.CreateEnglish());

        Assert.Contains("package identity", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetResultPath(string suffix) =>
        Path.Combine(AppContext.BaseDirectory, "ocr-smoke-" + suffix + ".json");
}
