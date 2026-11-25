using StoryTranslator.App;

namespace StoryTranslator.Core.Tests;

public sealed class AppStartupCommandTests
{
    [Fact]
    public async Task NormalArgumentsOpenMainWindowWithoutRunningSmokeTest()
    {
        var shown = 0;
        var smokeRuns = 0;

        var exitCode = await AppStartupCommand.DispatchAsync(
            [],
            () => shown++,
            _ => { },
            _ =>
            {
                smokeRuns++;
                return Task.FromResult(0);
            });

        Assert.Null(exitCode);
        Assert.Equal(1, shown);
        Assert.Equal(0, smokeRuns);
    }

    [Fact]
    public async Task SmokeArgumentsRunOcrWithoutOpeningMainWindow()
    {
        var shown = 0;
        string? outputPath = null;

        var exitCode = await AppStartupCommand.DispatchAsync(
            ["--ocr-smoke-test", "result.json"],
            () => shown++,
            _ => { },
            path =>
            {
                outputPath = path;
                return Task.FromResult(7);
            });

        Assert.Equal(7, exitCode);
        Assert.Equal(0, shown);
        Assert.Equal("result.json", outputPath);
    }

    [Fact]
    public async Task InvalidArgumentsReportUsageWithoutOpeningMainWindow()
    {
        var shown = 0;
        string? error = null;

        var exitCode = await AppStartupCommand.DispatchAsync(
            ["--unknown"],
            () => shown++,
            message => error = message,
            _ => Task.FromResult(0));

        Assert.Equal(2, exitCode);
        Assert.Equal(0, shown);
        Assert.Contains("Usage:", error);
    }
}
