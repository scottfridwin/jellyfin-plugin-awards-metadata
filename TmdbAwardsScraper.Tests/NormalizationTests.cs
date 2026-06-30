using TmdbAwardsScraper.Normalization;
using Xunit;

namespace TmdbAwardsScraper.Tests;

public class NormalizationTests
{
    private readonly SlugTextNormalizer _normalizer;

    public NormalizationTests()
    {
        _normalizer = new SlugTextNormalizer();
    }

    [Theory]
    [InlineData("Academy Awards", "academy-awards")]
    [InlineData("Best Original Screenplay", "best-original-screenplay")]
    [InlineData("Golden Globes", "golden-globes")]
    [InlineData("Best Picture", "best-picture")]
    [InlineData("BAFTA Awards", "bafta-awards")]
    [InlineData("Screen Actors Guild Awards", "screen-actors-guild-awards")]
    [InlineData("Best Actor in a Leading Role", "best-actor-in-a-leading-role")]
    public void Normalize_ProducesExpectedSlugs(string input, string expected)
    {
        var result = _normalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  Leading Spaces  ", "leading-spaces")]
    [InlineData("Multiple   Spaces", "multiple-spaces")]
    [InlineData("Tabs\tand\tnewlines\n", "tabs-and-newlines")]
    public void Normalize_HandlesWhitespace(string input, string expected)
    {
        var result = _normalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Café", "cafe")]
    [InlineData("naïve", "naive")]
    [InlineData("über", "uber")]
    public void Normalize_HandlesDiacritics(string input, string expected)
    {
        var result = _normalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Best Actor (Drama)", "best-actor-drama")]
    [InlineData("Award: Special", "award-special")]
    [InlineData("Film/Movie", "film-movie")]
    public void Normalize_HandlesSpecialCharacters(string input, string expected)
    {
        var result = _normalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _normalizer.Normalize(string.Empty));
    }

    [Fact]
    public void Normalize_NullString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _normalizer.Normalize(null!));
    }

    [Fact]
    public void Normalize_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _normalizer.Normalize("   "));
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        var input = "Academy Awards";
        var firstPass = _normalizer.Normalize(input);
        var secondPass = _normalizer.Normalize(firstPass);

        Assert.Equal(firstPass, secondPass);
    }
}
