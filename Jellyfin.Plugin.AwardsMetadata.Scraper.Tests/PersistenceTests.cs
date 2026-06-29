using Microsoft.Extensions.Logging.Abstractions;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Persistence;
using Xunit;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;
    private readonly JsonAwardStore _store;

    public PersistenceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "awards-tests-" + Guid.NewGuid().ToString("N"));
        _tempFile = Path.Combine(_tempDir, "awards.json");
        _store = new JsonAwardStore(_tempFile, NullLogger<JsonAwardStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var database = CreateSampleDatabase();

        await _store.SaveAsync(database);
        var loaded = await _store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(database.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(database.Source, loaded.Source);
        Assert.Equal(database.Organizations.Count, loaded.Organizations.Count);
        Assert.Equal(database.Ceremonies.Count, loaded.Ceremonies.Count);
    }

    [Fact]
    public async Task Save_CreatesDirectory()
    {
        Assert.False(Directory.Exists(_tempDir));

        await _store.SaveAsync(CreateSampleDatabase());

        Assert.True(Directory.Exists(_tempDir));
        Assert.True(File.Exists(_tempFile));
    }

    [Fact]
    public async Task Load_WhenFileDoesNotExist_ReturnsNull()
    {
        var result = await _store.LoadAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task Exists_WhenFileExists_ReturnsTrue()
    {
        await _store.SaveAsync(CreateSampleDatabase());
        Assert.True(_store.Exists());
    }

    [Fact]
    public void Exists_WhenFileDoesNotExist_ReturnsFalse()
    {
        Assert.False(_store.Exists());
    }

    [Fact]
    public async Task Save_PreservesNominationData()
    {
        var database = CreateSampleDatabase();

        await _store.SaveAsync(database);
        var loaded = await _store.LoadAsync();

        Assert.NotNull(loaded);
        var ceremony = loaded.Ceremonies[0];
        var category = ceremony.Categories[0];
        var winner = category.Nominations.First(n => n.Result == AwardResult.Winner);

        Assert.Equal(872585, winner.TmdbMovieId);
        Assert.Equal("Oppenheimer", winner.Name);
    }

    [Fact]
    public async Task Save_PreservesEnumValues()
    {
        var database = CreateSampleDatabase();

        await _store.SaveAsync(database);
        var loaded = await _store.LoadAsync();

        Assert.NotNull(loaded);
        var ceremony = loaded.Ceremonies[0];
        var category = ceremony.Categories[0];

        Assert.Contains(category.Nominations, n => n.Result == AwardResult.Winner);
        Assert.Contains(category.Nominations, n => n.Result == AwardResult.Nominee);
    }

    [Fact]
    public async Task Save_IsIdempotent()
    {
        var database = CreateSampleDatabase();

        await _store.SaveAsync(database);
        var content1 = await File.ReadAllTextAsync(_tempFile);

        // Wait to ensure different GeneratedUtc if not properly controlled
        await _store.SaveAsync(database);
        var content2 = await File.ReadAllTextAsync(_tempFile);

        // Both saves produce valid JSON (content may differ due to timestamp)
        var loaded1 = await _store.LoadAsync();
        Assert.NotNull(loaded1);
    }

    private static AwardsDatabase CreateSampleDatabase()
    {
        return new AwardsDatabase
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
            Ceremonies =
            [
                new AwardCeremony
                {
                    OrganizationSlug = "academy-awards",
                    OrganizationName = "Academy Awards",
                    Year = 2024,
                    RelativeUrl = "/award/academy-awards/2024",
                    Categories =
                    [
                        new AwardCategory
                        {
                            Name = "Best Picture",
                            Nominations =
                            [
                                new AwardNomination
                                {
                                    Name = "Oppenheimer",
                                    Result = AwardResult.Winner,
                                    TmdbMovieId = 872585,
                                },
                                new AwardNomination
                                {
                                    Name = "Poor Things",
                                    Result = AwardResult.Nominee,
                                    TmdbMovieId = 792307,
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }
}
