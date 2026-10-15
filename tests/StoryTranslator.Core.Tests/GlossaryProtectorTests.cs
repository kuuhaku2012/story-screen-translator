using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class GlossaryProtectorTests
{
    [Fact]
    public void ProtectAndRestore_ReturnsExactCanonicalName()
    {
        var protector = new GlossaryProtector([
            new GlossaryEntry("Yangyang", ["Yang Yang"], "character")
        ]);

        var protectedText = protector.Protect("I met Yang Yang yesterday.");
        var restored = GlossaryProtector.Restore("Tôi đã gặp " + protectedText.TokenMap.Keys.Single() + " hôm qua.", protectedText.TokenMap);

        Assert.Equal("I met " + protectedText.TokenMap.Keys.Single() + " yesterday.", protectedText.Text);
        Assert.Equal("Tôi đã gặp Yangyang hôm qua.", restored);
    }

    [Fact]
    public void ProtectAndRestore_HandlesMultipleNamesInOneSentence()
    {
        var protector = new GlossaryProtector([
            new GlossaryEntry("Jinhsi", ["Jinshi"], "character"),
            new GlossaryEntry("Jinzhou", [], "location")
        ]);

        var result = protector.Protect("Jinshi returned to Jinzhou.");

        Assert.Equal(2, result.TokenMap.Count);
        Assert.Equal("Jinhsi returned to Jinzhou.", GlossaryProtector.Restore(result.Text, result.TokenMap));
    }

    [Fact]
    public void Protect_MatchesAliasesCaseInsensitively()
    {
        var protector = new GlossaryProtector([
            new GlossaryEntry("Jinhsi", ["Jinshi"], "character")
        ]);

        var result = protector.Protect("jinSHI spoke.");

        Assert.Contains("Jinhsi", result.TokenMap.Values);
        Assert.DoesNotContain("jinSHI", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Protect_PrefersLongestOverlappingAlias()
    {
        var protector = new GlossaryProtector([
            new GlossaryEntry("Jinzhou", [], "location"),
            new GlossaryEntry("Jinzhou City", [], "location")
        ]);

        var result = protector.Protect("Jinzhou City awaits.");

        Assert.Equal("Jinzhou City", Assert.Single(result.TokenMap.Values));
        Assert.DoesNotContain("City", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Protect_PreservesUnrelatedTextAndDoesNotMatchInsideLargerWords()
    {
        const string source = "  The Jinzhou guard said:\t'go!'  ";
        var protector = new GlossaryProtector([
            new GlossaryEntry("Jinzhou", [], "location")
        ]);

        var result = protector.Protect(source);

        var token = Assert.Single(result.TokenMap.Keys);
        Assert.Equal("  The " + token + " guard said:\t'go!'  ", result.Text);
        Assert.Equal("Jinzhoujinzhou2", protector.Protect("Jinzhoujinzhou2").Text);
    }

    [Fact]
    public void Restore_LeavesUnknownOrOtherSentenceTokensUntouched()
    {
        var currentSentence = new GlossaryProtector([
            new GlossaryEntry("Rover", [], "character")
        ]).Protect("Rover arrived.");

        Assert.Equal("UNKNOWN_TOKEN Rover", GlossaryProtector.Restore(
            "UNKNOWN_TOKEN " + currentSentence.TokenMap.Keys.Single(), currentSentence.TokenMap));
    }
}
