namespace StoryTranslator.App;

/// <summary>Routes command-line startup before the WPF main window is created.</summary>
public static class AppStartupCommand
{
    public static async Task<int?> DispatchAsync(
        string[] args,
        Action showMainWindow,
        Action<string> reportError,
        Func<string, Task<int>> runSmokeTest)
    {
        ArgumentNullException.ThrowIfNull(showMainWindow);
        ArgumentNullException.ThrowIfNull(reportError);
        ArgumentNullException.ThrowIfNull(runSmokeTest);

        var command = OcrSmokeTestCommand.Parse(args);
        switch (command.Kind)
        {
            case OcrSmokeCommandKind.Normal:
                showMainWindow();
                return null;
            case OcrSmokeCommandKind.SmokeTest:
                return await runSmokeTest(command.ResultPath!);
            default:
                reportError(command.Error ?? "Invalid command line.");
                return 2;
        }
    }
}
