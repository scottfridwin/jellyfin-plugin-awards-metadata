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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ScrapeAwardsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScrapeAwardsTask"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public ScrapeAwardsTask(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
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

        string validatedBaseUrl;
        try
        {
            validatedBaseUrl = config.GetValidatedTmdbBaseUrl();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid TmdbBaseUrl configuration. Aborting scrape");
            return;
        }

        _logger.LogDebug(
            "Scrape configuration: BaseUrl={BaseUrl}, RateLimitDelayMs={RateLimit}, MaxRetryCount={MaxRetry}, RequestTimeoutSeconds={Timeout}, Organizations=[{Organizations}]",
            validatedBaseUrl,
            config.RateLimitDelayMs,
            config.MaxRetryCount,
            config.RequestTimeoutSeconds,
            string.Join(", ", config.EnabledOrganizations));

        var scraperOptions = new ScraperOptions
        {
            BaseUrl = validatedBaseUrl,
            RateLimitDelayMs = config.RateLimitDelayMs,
            MaxRetryCount = config.MaxRetryCount,
            RequestTimeoutSeconds = config.RequestTimeoutSeconds,
        };

        using var httpClient = _httpClientFactory.CreateClient(nameof(ScrapeAwardsTask));
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

        try
        {
            var database = await scraper.ScrapeAsync(scrapeOptions, scrapeProgress, cancellationToken).ConfigureAwait(false);

            // Persist the database
            var dbPath = plugin.GetAwardsDatabasePath();
            _logger.LogDebug("Saving awards database to {Path}", dbPath);
            var store = new JsonAwardStore(
                dbPath,
                _loggerFactory.CreateLogger<JsonAwardStore>());

            await store.SaveAsync(database, cancellationToken).ConfigureAwait(false);

            var totalNominations = database.Ceremonies.Sum(c => c.Categories.Sum(cat => cat.Nominations.Count));
            var totalWinners = database.Ceremonies.Sum(c => c.Categories.Sum(cat => cat.Nominations.Count(n => n.Result == Jellyfin.Plugin.AwardsMetadata.Scraper.Models.AwardResult.Winner)));
            _logger.LogInformation(
                "Awards scrape complete: {Ceremonies} ceremonies, {Nominations} nominations ({Winners} winners) saved to {Path}",
                database.Ceremonies.Count,
                totalNominations,
                totalWinners,
                dbPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Awards scrape was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Awards scrape failed");
            throw;
        }

        progress.Report(100);
    }
}
