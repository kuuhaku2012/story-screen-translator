using System.Drawing;

namespace StoryTranslator.Core;

public interface IScreenCapture
{
    void Start(Rectangle region);
    void Stop();
}
