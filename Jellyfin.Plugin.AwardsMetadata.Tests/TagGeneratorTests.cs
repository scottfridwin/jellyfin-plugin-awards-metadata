using Jellyfin.Plugin.AwardsMetadata.TagGeneration;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Normalization;
using Xunit;

namespace Jellyfin.Plugin.AwardsMetadata.Tests;

public class TagGeneratorTests
{
    private readonly TagGenerator _generator;
    private readonly SlugTextNormalizer _normalizer;

    public TagGeneratorTests()
    {
        _normalizer = new SlugTextNormalizer();
        _generator = new TagGenerator("award-{awardType}-{awardResult}-{awardCategory}-{awardYear}", _normalizer);
    }

    [Fact]
    public void GenerateTag_DefaultFormat_ProducesExpectedTag()
    {
        var tag = _generator.GenerateTag("Academy Awards", "Winner", "Best Picture", 1999);
        Assert.Equal("award-academy-awards-winner-best-picture-1999", tag);
    }

    [Fact]
    public void GenerateTag_NomineeResult_ProducesExpectedTag()
    {
        var tag = _generator.GenerateTag("Academy Awards", "Nominee", "Best Actor", 2005);
        Assert.Equal("award-academy-awards-nominee-best-actor-2005", tag);
    }

    [Fact]
    public void GenerateTag_SimpleFormat_ProducesExpectedTag()
    {
        var simpleGenerator = new TagGenerator("{awardType}-{awardResult}", _normalizer);
        var tag = simpleGenerator.GenerateTag("Academy Awards", "Winner", "Best Picture", 1999);
        Assert.Equal("academy-awards-winner", tag);
    }

    [Fact]
    public void GenerateTag_CustomFormat_ProducesExpectedTag()
    {
        var customGenerator = new TagGenerator("{awardType}-{awardYear}-{awardResult}", _normalizer);
        var tag = customGenerator.GenerateTag("Golden Globes", "Winner", "Best Drama", 2020);
        Assert.Equal("golden-globes-2020-winner", tag);
    }

    [Fact]
    public void GenerateTag_WithComplexCategory_NormalizesCorrectly()
    {
        var tag = _generator.GenerateTag("Academy Awards", "Winner", "Best Original Screenplay", 2024);
        Assert.Equal("award-academy-awards-winner-best-original-screenplay-2024", tag);
    }

    [Fact]
    public void GenerateTag_IsIdempotent()
    {
        var tag1 = _generator.GenerateTag("Academy Awards", "Winner", "Best Picture", 1999);
        var tag2 = _generator.GenerateTag("Academy Awards", "Winner", "Best Picture", 1999);
        Assert.Equal(tag1, tag2);
    }

    [Fact]
    public void GenerateTag_DifferentInputs_ProduceDifferentTags()
    {
        var tag1 = _generator.GenerateTag("Academy Awards", "Winner", "Best Picture", 1999);
        var tag2 = _generator.GenerateTag("Academy Awards", "Nominee", "Best Picture", 1999);
        Assert.NotEqual(tag1, tag2);
    }

    [Fact]
    public void GenerateTag_CaseInsensitivePlaceholders()
    {
        var generator = new TagGenerator("award-{AWARDTYPE}-{AwardResult}-{awardcategory}-{AwardYear}", _normalizer);
        var tag = generator.GenerateTag("Academy Awards", "Winner", "Best Picture", 1999);
        Assert.Equal("award-academy-awards-winner-best-picture-1999", tag);
    }
}
