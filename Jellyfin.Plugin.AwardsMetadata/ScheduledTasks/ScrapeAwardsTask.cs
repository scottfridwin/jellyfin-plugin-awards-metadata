using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Configuration;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Downloading;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Parsing;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Persistence;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Services;

namespace Jellyfin.Plugin.AwardsMetadata.ScheduledTasks;

/// <summary>
/// Scheduled task that performs a full scrape of award data from TMDB.
/// </summary>
public sealed class ScrapeAwardsTask : IScheduledTask
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ScrapeAwardsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScrapeAwardsTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory.</param>
    public ScrapeAwardsTask(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ScrapeAwardsTask>();
    }

    /// <inheritdoc />
    public string Name => "Scrape Awards Data";

    /// <inheritdoc />
    public string Key => "AwardsMetadataScrape";

    /// <inheritdoc />
    public string Description => "Downloads and parses award data from TMDB for all enabled organizations.";

    /// <inheritdoc />
    public string Category => "Awards Metadata";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run weekly by default
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.WeeklyTrigger,
                DayOfWeek = DayOfWeek.Sunday,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
            },
        ];
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            _logger.LogError("Plugin instance is not available");
            return;
        }

        var config = plugin.Configuration;

        if (config.EnabledOrganizations.Count == 0)
        {
            _logger.LogWarning("No organizations are enabled for scraping. Configure organizations first.");
            return;
        }

        var scraperOptions = new ScraperOptions
        {
            BaseUrl = config.TmdbBaseUrl,
            RateLimitDelayMs = config.RateLimitDelayMs,
            MaxRetryCount = config.MaxRetryCount,
            RequestTimeoutSeconds = config.RequestTimeoutSeconds,
        };

        using var httpClient = new HttpClient();
        var downloader = new HttpHtmlDownloader(
            httpClient,
            scraperOptions,
            _loggerFactory.CreateLogger<HttpHtmlDownloader>());

        var indexParser = new TmdbAwardsIndexParser(_loggerFactory.CreateLogger<TmdbAwardsIndexParser>());
        var ceremonyParser = new TmdbCeremonyParser(_loggerFactory.CreateLogger<TmdbCeremonyParser>());

        var scraper = new TmdbAwardsScraperService(
            downloader,
            indexParser,
            ceremonyParser,
            scraperOptions,
            _loggerFactory.CreateLogger<TmdbAwardsScraperService>());

        var scrapeOptions = new ScrapeOptions
        {
            OrganizationSlugs = config.EnabledOrganizations,
        };

        var scrapeProgress = new Progress<ScrapeProgress>(p =>
        {
            progress.Report(p.ProgressPercent);
            _logger.LogDebug("{Message}", p.Message);
        });

        _logger.LogInformation("Starting awards scrape for {Count} organizations", config.EnabledOrganizations.Count);

        var database = await scraper.ScrapeAsync(scrapeOptions, scrapeProgress, cancellationToken).ConfigureAwait(false);

        // Persist the database
        var store = new JsonAwardStore(
            plugin.GetAwardsDatabasePath(),
            _loggerFactory.CreateLogger<JsonAwardStore>());

        await store.SaveAsync(database, cancellationToken).ConfigureAwait(false);

        var totalNominations = database.Ceremonies.Sum(c => c.Categories.Sum(cat => cat.Nominations.Count));
        _logger.LogInformation(
            "Awards scrape complete: {Ceremonies} ceremonies, {Nominations} nominations saved",
            database.Ceremonies.Count,
            totalNominations);

        progress.Report(100);
    }
}
