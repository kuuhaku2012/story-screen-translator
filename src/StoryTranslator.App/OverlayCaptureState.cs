namespace StoryTranslator.App;

/// <summary>UI-thread state: capture temporarily suppresses display; OFF clears it permanently.</summary>
public sealed class OverlayCaptureState
{
    private bool _capturing;
    private bool _requested;
    private Func<bool>? _allowed;
    public bool ShouldShow => !_capturing && _requested && (_allowed?.Invoke() ?? true);
    public void RequestShow(Func<bool>? allowed = null) { _requested = true; _allowed = allowed; }
    public void Hide() { _requested = false; _allowed = null; }
    public void BeginCapture() => _capturing = true;
    public void EndCapture() => _capturing = false;
}
