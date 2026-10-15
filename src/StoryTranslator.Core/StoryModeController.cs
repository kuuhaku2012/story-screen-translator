using System.Drawing;

namespace StoryTranslator.Core;

public sealed class StoryModeController
{
    private readonly IScreenCapture _screenCapture;

    public StoryModeController(IScreenCapture screenCapture)
    {
        _screenCapture = screenCapture;
    }

    public bool IsEnabled { get; private set; }

    public void Enable(Rectangle region)
    {
        if (IsEnabled)
        {
            return;
        }

        _screenCapture.Start(region);
        IsEnabled = true;
    }

    public void Disable()
    {
        if (!IsEnabled)
        {
            return;
        }

        _screenCapture.Stop();
        IsEnabled = false;
    }
}
