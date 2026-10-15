namespace StoryTranslator.Core.Tests;

public sealed class TextStabilityFilterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_observations_do_not_emit_or_stabilize(string? text)
    {
        var filter = new TextStabilityFilter();

        Assert.Null(filter.Observe(text));
        Assert.Null(filter.Observe("line"));
    }

    [Fact]
    public void One_observation_is_not_stable()
    {
        var filter = new TextStabilityFilter();

        Assert.Null(filter.Observe("  line  "));
    }

    [Fact]
    public void Two_normalized_equal_observations_emit_trimmed_collapsed_text()
    {
        var filter = new TextStabilityFilter();

        Assert.Null(filter.Observe("  First   line!  "));
        Assert.Equal("First line!", filter.Observe("First line!"));
    }

    [Fact]
    public void Stable_line_is_suppressed_until_a_different_stable_line_appears()
    {
        var filter = new TextStabilityFilter();

        Assert.Null(filter.Observe("alpha"));
        Assert.Equal("alpha", filter.Observe("alpha"));
        Assert.Null(filter.Observe("alpha"));
        Assert.Null(filter.Observe("beta"));
        Assert.Equal("beta", filter.Observe("beta"));
        Assert.Null(filter.Observe("beta"));
    }

    [Fact]
    public void Returning_to_a_prior_line_after_another_stable_line_emits_again()
    {
        var filter = new TextStabilityFilter();

        Assert.Null(filter.Observe("alpha"));
        Assert.Equal("alpha", filter.Observe("alpha"));
        Assert.Null(filter.Observe("beta"));
        Assert.Equal("beta", filter.Observe("beta"));
        Assert.Null(filter.Observe("alpha"));
        Assert.Equal("alpha", filter.Observe("alpha"));
    }

    [Fact]
    public void Different_intervening_observation_resets_consecutive_candidate()
    {
        var filter = new TextStabilityFilter();

        Assert.Null(filter.Observe("alpha"));
        Assert.Null(filter.Observe("beta"));
        Assert.Null(filter.Observe("alpha"));
        Assert.Equal("alpha", filter.Observe("alpha"));
    }
}
