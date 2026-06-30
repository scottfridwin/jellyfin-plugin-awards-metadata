using TmdbAwardsScraper.Normalization;
using TmdbAwardsScraper.Serialization;
using TmdbAwardsScraper.Models;
using Xunit;

namespace TmdbAwardsScraper.Tests;

public class SerializationTests
{
    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var database = new AwardsDatabase
        {
            SchemaVersion = 1,
            Source = "TMDB",
            SourceBaseUrl = "https://www.themoviedb.org",
            Organizations =
            [
                new AwardOrganization
                {
                    Name = "Academy Awards",
                    Slug = "academy-awards",
                    RelativeUrl = "/award/academy-awards",
                },
            ],
            Ceremonies = [],
        };

        var json = AwardsJsonSerializer.Serialize(database);

        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"source\": \"TMDB\"", json);
        Assert.Contains("\"academy-awards\"", json);
    }

    [Fact]
    public void Deserialize_RoundTrips()
    {
        var database = new AwardsDatabase
        {
            SchemaVersion = 1,
            Source = "TMDB",
            SourceBaseUrl = "https://www.themoviedb.org",
            Organizations =
            [
                new AwardOrganization
                {
                    Name = "Test",
                    Slug = "test",
                    RelativeUrl = "/award/test",
                },
            ],
            Ceremonies =
            [
                new AwardCeremony
                {
                    OrganizationSlug = "test",
                    OrganizationName = "Test",
                    Year = 2024,
                    RelativeUrl = "/award/test/2024",
                    Categories =
                    [
                        new AwardCategory
                        {
                            Name = "Best Film",
                            Nominations =
                            [
                                new AwardNomination
                                {
                                    Name = "Movie A",
                                    Result = AwardResult.Winner,
                                    TmdbMovieId = 123,
                                    TmdbPersonIds = [456],
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        var json = AwardsJsonSerializer.Serialize(database);
        var deserialized = AwardsJsonSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(1, deserialized.SchemaVersion);
        Assert.Single(deserialized.Organizations);
        Assert.Single(deserialized.Ceremonies);

        var ceremony = deserialized.Ceremonies[0];
        Assert.Equal(2024, ceremony.Year);

        var nomination = ceremony.Categories[0].Nominations[0];
        Assert.Equal("Movie A", nomination.Name);
        Assert.Equal(AwardResult.Winner, nomination.Result);
        Assert.Equal(123, nomination.TmdbMovieId);
        Assert.Contains(456, nomination.TmdbPersonIds);
    }

    [Fact]
    public void Serialize_EnumsAsStrings()
    {
        var database = new AwardsDatabase
        {
            Ceremonies =
            [
                new AwardCeremony
                {
                    OrganizationSlug = "test",
                    OrganizationName = "Test",
                    Year = 2024,
                    Categories =
                    [
                        new AwardCategory
                        {
                            Name = "Best",
                            Nominations =
                            [
                                new AwardNomination
                                {
                                    Name = "Film",
                                    Result = AwardResult.Winner,
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        var json = AwardsJsonSerializer.Serialize(database);

        Assert.Contains("\"winner\"", json);
    }
}
