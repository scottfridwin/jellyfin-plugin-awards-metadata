using Microsoft.Extensions.Logging.Abstractions;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Parsing;
using Xunit;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Tests;

public class CeremonyParserTests
{
    private readonly TmdbCeremonyParser _parser;
    private readonly AwardOrganization _organization;
    private readonly CeremonyReference _ceremonyRef;

    public CeremonyParserTests()
    {
        _parser = new TmdbCeremonyParser(NullLogger<TmdbCeremonyParser>.Instance);
        _organization = new AwardOrganization
        {
            Name = "Academy Awards",
            Slug = "academy-awards",
            RelativeUrl = "/award/academy-awards",
        };
        _ceremonyRef = new CeremonyReference
        {
            Year = 2024,
            RelativeUrl = "/award/academy-awards/2024",
        };
    }

    [Fact]
    public void ParseCeremony_WithValidHtml_ReturnsCeremony()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-2024.html");

        var ceremony = _parser.ParseCeremony(html, _organization, _ceremonyRef);

        Assert.NotNull(ceremony);
        Assert.Equal("academy-awards", ceremony.OrganizationSlug);
        Assert.Equal("Academy Awards", ceremony.OrganizationName);
        Assert.Equal(2024, ceremony.Year);
    }

    [Fact]
    public void ParseCeremony_FindsCategories()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-2024.html");

        var ceremony = _parser.ParseCeremony(html, _organization, _ceremonyRef);

        Assert.NotEmpty(ceremony.Categories);
        Assert.Contains(ceremony.Categories, c => c.Name == "Best Picture");
        Assert.Contains(ceremony.Categories, c => c.Name == "Best Director");
        Assert.Contains(ceremony.Categories, c => c.Name == "Best Actor");
    }

    [Fact]
    public void ParseCeremony_IdentifiesWinners()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-2024.html");

        var ceremony = _parser.ParseCeremony(html, _organization, _ceremonyRef);

        var bestPicture = ceremony.Categories.First(c => c.Name == "Best Picture");
        var winners = bestPicture.Nominations.Where(n => n.Result == AwardResult.Winner).ToList();

        Assert.Single(winners);
        Assert.Contains("Oppenheimer", winners[0].Name);
    }

    [Fact]
    public void ParseCeremony_ExtractsMovieIds()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-2024.html");

        var ceremony = _parser.ParseCeremony(html, _organization, _ceremonyRef);

        var bestPicture = ceremony.Categories.First(c => c.Name == "Best Picture");
        var oppenheimer = bestPicture.Nominations.First(n => n.Result == AwardResult.Winner);

        Assert.Equal(872585, oppenheimer.TmdbMovieId);
    }

    [Fact]
    public void ParseCeremony_ExtractsPersonIds()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-2024.html");

        var ceremony = _parser.ParseCeremony(html, _organization, _ceremonyRef);

        var bestDirector = ceremony.Categories.First(c => c.Name == "Best Director");
        var winner = bestDirector.Nominations.First(n => n.Result == AwardResult.Winner);

        Assert.Contains(525, winner.TmdbPersonIds); // Christopher Nolan
    }

    [Fact]
    public void ParseCeremony_FindsNominees()
    {
        var html = File.ReadAllText("Fixtures/academy-awards-2024.html");

        var ceremony = _parser.ParseCeremony(html, _organization, _ceremonyRef);

        var bestPicture = ceremony.Categories.First(c => c.Name == "Best Picture");
        var nominees = bestPicture.Nominations.Where(n => n.Result == AwardResult.Nominee).ToList();

        Assert.True(nominees.Count >= 4);
    }

    [Fact]
    public void ParseCeremony_WithEmptyHtml_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => _parser.ParseCeremony(string.Empty, _organization, _ceremonyRef));
    }
}
