using System.Drawing;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class StoryModeControllerTests
{
    [Fact]
    public void New_controller_starts_disabled()
    {
        var controller = new StoryModeController(new RecordingScreenCapture());

        Assert.False(controller.IsEnabled);
    }

    [Fact]
    public void Enable_starts_capture_with_the_selected_region_once()
    {
        var capture = new RecordingScreenCapture();
        var controller = new StoryModeController(capture);
        var region = new Rectangle(10, 20, 300, 200);

        controller.Enable(region);

        Assert.True(controller.IsEnabled);
        Assert.Equal(new[] { region }, capture.StartedRegions);
    }

    [Fact]
    public void Enabling_again_while_enabled_is_a_no_op()
    {
        var capture = new RecordingScreenCapture();
        var controller = new StoryModeController(capture);

        controller.Enable(new Rectangle(1, 2, 3, 4));
        controller.Enable(new Rectangle(5, 6, 7, 8));

        Assert.Single(capture.StartedRegions);
        Assert.Equal(0, capture.StopCount);
    }

    [Fact]
    public void Disable_stops_capture_and_turns_mode_off()
    {
        var capture = new RecordingScreenCapture();
        var controller = new StoryModeController(capture);
        controller.Enable(new Rectangle(1, 2, 3, 4));

        controller.Disable();

        Assert.False(controller.IsEnabled);
        Assert.Equal(1, capture.StopCount);
    }

    [Fact]
    public void Disabling_while_already_disabled_is_a_no_op()
    {
        var capture = new RecordingScreenCapture();
        var controller = new StoryModeController(capture);

        controller.Disable();

        Assert.False(controller.IsEnabled);
        Assert.Equal(0, capture.StopCount);
    }

    [Fact]
    public void Failed_start_leaves_mode_disabled()
    {
        var capture = new RecordingScreenCapture { StartException = new InvalidOperationException("capture failed") };
        var controller = new StoryModeController(capture);

        Assert.Throws<InvalidOperationException>(() => controller.Enable(new Rectangle(1, 2, 3, 4)));
        Assert.False(controller.IsEnabled);
        Assert.Equal(0, capture.StopCount);
    }

    private sealed class RecordingScreenCapture : IScreenCapture
    {
        public List<Rectangle> StartedRegions { get; } = [];
        public int StopCount { get; private set; }
        public Exception? StartException { get; init; }

        public void Start(Rectangle region)
        {
            if (StartException is not null)
            {
                throw StartException;
            }

            StartedRegions.Add(region);
        }

        public void Stop() => StopCount++;
    }
}
