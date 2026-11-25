using System.Windows;

namespace StoryTranslator.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--evaluate-ocr")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var result = await FileOcrEvaluation.RunAsync(e.Args[1]);
            Shutdown(result);
            return;
        }
        if (e.Args.Length == 2 && (e.Args[0] == "--overlay-smoke-test" || e.Args[0] == "--large-ocr-smoke-test" || e.Args[0] == "--spatial-smoke-test"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var result = e.Args[0] == "--spatial-smoke-test" ? await SpatialOverlaySmoke.RunAsync(e.Args[1])
                : e.Args[0] == "--overlay-smoke-test"
                ? await InPlaceOverlaySmoke.RunAsync(e.Args[1])
                : await OcrSmokeTestRunner.RunAsync(e.Args[1], SyntheticOcrFrame.RecognizeOversizedAsync);
            Environment.ExitCode = result;
            Shutdown(result);
            return;
        }
        var exitCode = await AppStartupCommand.DispatchAsync(
            e.Args,
            () => new MainWindow().Show(),
            message => System.Diagnostics.Trace.TraceError("{0}", message),
            path => OcrSmokeTestRunner.RunProductionAsync(path));

        if (exitCode is int code)
        {
            Environment.ExitCode = code;
            Shutdown(code);
        }
    }
}
