using Microsoft.Extensions.Logging.Abstractions;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Parsing;
using Xunit;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Tests;

public class AwardsIndexParserTests
{
    private readonly TmdbAwardsIndexParser _parser;

    public AwardsIndexParserTests()
    {
        _parser = new TmdbAwardsIndexParser(NullLogger<TmdbAwardsIndexParser>.Instance);
    }

    [Fact]
    public void ParseIndex_WithValidHtml_ReturnsOrganizations()
    {
        var html = File.ReadAllText("Fixtures/awards-index.html");

        var organizations = _parser.ParseIndex(html);

        Assert.NotEmpty(organizations);
        Assert.Contains(organizations, o => o.Slug == "1-academy-awards");
        Assert.Contains(organizations, o => o.Slug == "4-golden-globes");
        Assert.Contains(organizations, o => o.Slug == "5-bafta-film-awards");
    }

    [Fact]
    public void ParseIndex_ExtractsCorrectNames()
    {
        var html = File.ReadAllText("Fixtures/awards-index.html");

        var organizations = _parser.ParseIndex(html);

        var academyAwards = organizations.First(o => o.Slug == "1-academy-awards");
        Assert.Equal("Academy Awards", academyAwards.Name);
        Assert.Equal("/award/1-academy-awards", academyAwards.RelativeUrl);
    }

    [Fact]
    public void ParseIndex_NoDuplicates()
    {
        var html = File.ReadAllText("Fixtures/awards-index.html");

        var organizations = _parser.ParseIndex(html);

        var slugs = organizations.Select(o => o.Slug).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }

    [Fact]
    public void ParseIndex_WithEmptyHtml_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _parser.ParseIndex(string.Empty));
    }

    [Fact]
    public void ParseCeremonyList_WithValidHtml_ReturnsCeremonies()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-org.html");
        var org = new Models.AwardOrganization
        {
            Name = "Academy Awards",
            Slug = "1-academy-awards",
            RelativeUrl = "/award/1-academy-awards",
        };

        var ceremonies = _parser.ParseCeremonyList(html, org);

        Assert.NotEmpty(ceremonies);
        Assert.Contains(ceremonies, c => c.Year == 2024);
        Assert.Contains(ceremonies, c => c.Year == 1999);
    }

    [Fact]
    public void ParseCeremonyList_ReturnsCorrectUrls()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-org.html");
        var org = new Models.AwardOrganization
        {
            Name = "Academy Awards",
            Slug = "1-academy-awards",
            RelativeUrl = "/award/1-academy-awards",
        };

        var ceremonies = _parser.ParseCeremonyList(html, org);

        var ceremony2024 = ceremonies.First(c => c.Year == 2024);
        Assert.Equal("/award/1-academy-awards/ceremony/96", ceremony2024.RelativeUrl);
    }

    [Fact]
    public void ParseCeremonyList_NoDuplicateYears()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-org.html");
        var org = new Models.AwardOrganization
        {
            Name = "Academy Awards",
            Slug = "1-academy-awards",
            RelativeUrl = "/award/1-academy-awards",
        };

        var ceremonies = _parser.ParseCeremonyList(html, org);

        var years = ceremonies.Select(c => c.Year).ToList();
        Assert.Equal(years.Count, years.Distinct().Count());
    }

    [Fact]
    public void ParseCeremonyList_IsSortedByYear()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-org.html");
        var org = new Models.AwardOrganization
        {
            Name = "Academy Awards",
            Slug = "1-academy-awards",
            RelativeUrl = "/award/1-academy-awards",
        };

        var ceremonies = _parser.ParseCeremonyList(html, org);

        var years = ceremonies.Select(c => c.Year).ToList();
        Assert.Equal(years.OrderBy(y => y), years);
    }
}
