using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Configuration;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Downloading;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Parsing;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Services;

/// <summary>
/// Orchestrates the TMDB awards scraping workflow.
/// </summary>
public sealed class TmdbAwardsScraperService : IAwardsScraper
{
    private readonly IHtmlDownloader _downloader;
    private readonly IAwardsIndexParser _indexParser;
    private readonly ICeremonyParser _ceremonyParser;
    private readonly ScraperOptions _options;
    private readonly ILogger<TmdbAwardsScraperService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbAwardsScraperService"/> class.
    /// </summary>
    public TmdbAwardsScraperService(
        IHtmlDownloader downloader,
        IAwardsIndexParser indexParser,
        ICeremonyParser ceremonyParser,
        ScraperOptions options,
        ILogger<TmdbAwardsScraperService> logger)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _indexParser = indexParser ?? throw new ArgumentNullException(nameof(indexParser));
        _ceremonyParser = ceremonyParser ?? throw new ArgumentNullException(nameof(ceremonyParser));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AwardOrganization>> DiscoverOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Discovering award organizations from {BaseUrl}", _options.BaseUrl);

        var indexUrl = $"{_options.BaseUrl.TrimEnd('/')}/award";
        var html = await _downloader.DownloadAsync(indexUrl, cancellationToken).ConfigureAwait(false);
        var organizations = _indexParser.ParseIndex(html);

        _logger.LogInformation("Discovered {Count} award organizations", organizations.Count);
        return organizations;
    }

    /// <inheritdoc />
    public async Task<AwardsDatabase> ScrapeAsync(
        ScrapeOptions? options = null,
        IProgress<ScrapeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ScrapeOptions();

        _logger.LogInformation("Starting awards scrape from {BaseUrl}", _options.BaseUrl);

        // Step 1: Discover organizations
        var allOrganizations = await DiscoverOrganizationsAsync(cancellationToken).ConfigureAwait(false);

        // Filter to requested organizations if specified
        var targetOrganizations = options.OrganizationSlugs.Count > 0
            ? allOrganizations.Where(o => options.OrganizationSlugs.Contains(o.Slug, StringComparer.OrdinalIgnoreCase)).ToList()
            : allOrganizations.ToList();

        if (targetOrganizations.Count == 0)
        {
            _logger.LogWarning("No matching organizations found to scrape");
            return new AwardsDatabase
            {
                SourceBaseUrl = _options.BaseUrl,
                Organizations = allOrganizations.ToList(),
            };
        }

        var database = new AwardsDatabase
        {
            SourceBaseUrl = _options.BaseUrl,
            Organizations = allOrganizations.ToList(),
        };

        // Step 2: Scrape each organization
        var totalOrgs = targetOrganizations.Count;
        for (var orgIndex = 0; orgIndex < totalOrgs; orgIndex++)
        {
            var org = targetOrganizations[orgIndex];
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new ScrapeProgress
            {
                CurrentOrganization = org.Name,
                ProgressPercent = (double)orgIndex / totalOrgs * 100,
                Message = $"Scraping {org.Name}...",
            });

            try
            {
                await ScrapeOrganizationAsync(org, options, database, progress, orgIndex, totalOrgs, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to scrape organization {Organization}. Continuing with next.", org.Name);
            }
        }

        database.GeneratedUtc = DateTimeOffset.UtcNow;

        var totalCeremonies = database.Ceremonies.Count;
        var totalNominations = database.Ceremonies.Sum(c => c.Categories.Sum(cat => cat.Nominations.Count));
        _logger.LogInformation(
            "Scrape complete: {Ceremonies} ceremonies, {Nominations} total nominations",
            totalCeremonies,
            totalNominations);

        progress?.Report(new ScrapeProgress
        {
            ProgressPercent = 100,
            Message = $"Scrape complete: {totalCeremonies} ceremonies, {totalNominations} nominations",
        });

        return database;
    }

    private async Task ScrapeOrganizationAsync(
        AwardOrganization org,
        ScrapeOptions options,
        AwardsDatabase database,
        IProgress<ScrapeProgress>? progress,
        int orgIndex,
        int totalOrgs,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scraping organization: {Organization} ({Slug})", org.Name, org.Slug);

        // Download organization page to get ceremony list
        var orgUrl = $"{_options.BaseUrl.TrimEnd('/')}{org.RelativeUrl}";
        var orgHtml = await _downloader.DownloadAsync(orgUrl, cancellationToken).ConfigureAwait(false);
        var ceremonyRefs = _indexParser.ParseCeremonyList(orgHtml, org);

        // Filter to requested years if specified
        var targetCeremonies = options.Years.Count > 0
            ? ceremonyRefs.Where(c => options.Years.Contains(c.Year)).ToList()
            : ceremonyRefs.ToList();

        _logger.LogInformation("Scraping {Count} ceremonies for {Organization}", targetCeremonies.Count, org.Name);

        for (var i = 0; i < targetCeremonies.Count; i++)
        {
            var ceremonyRef = targetCeremonies[i];
            cancellationToken.ThrowIfCancellationRequested();

            var overallProgress = ((double)orgIndex / totalOrgs * 100)
                + ((double)i / targetCeremonies.Count / totalOrgs * 100);

            progress?.Report(new ScrapeProgress
            {
                CurrentOrganization = org.Name,
                CurrentYear = ceremonyRef.Year,
                ProgressPercent = overallProgress,
                Message = $"Scraping {org.Name} {ceremonyRef.Year}...",
            });

            try
            {
                var ceremonyUrl = $"{_options.BaseUrl.TrimEnd('/')}{ceremonyRef.RelativeUrl}";
                var ceremonyHtml = await _downloader.DownloadAsync(ceremonyUrl, cancellationToken).ConfigureAwait(false);
                var ceremony = _ceremonyParser.ParseCeremony(ceremonyHtml, org, ceremonyRef);

                // Idempotency: remove existing ceremony for this org/year before adding
                database.Ceremonies.RemoveAll(c =>
                    c.OrganizationSlug == org.Slug && c.Year == ceremonyRef.Year);

                database.Ceremonies.Add(ceremony);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Failed to scrape {Organization} {Year}. Continuing with next ceremony.",
                    org.Name,
                    ceremonyRef.Year);
            }
        }
    }
}
