using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Services;

/// <summary>
/// Options for controlling the scope of a scrape operation.
/// </summary>
public sealed class ScrapeOptions
{
    /// <summary>
    /// Gets or sets the organization slugs to scrape. If empty, all discovered organizations are scraped.
    /// </summary>
    public List<string> OrganizationSlugs { get; set; } = [];

    /// <summary>
    /// Gets or sets specific years to scrape. If empty, all available years are scraped.
    /// </summary>
    public List<int> Years { get; set; } = [];
}

/// <summary>
/// Progress information for scrape operations.
/// </summary>
public sealed class ScrapeProgress
{
    /// <summary>
    /// Gets or sets the current organization being scraped.
    /// </summary>
    public string? CurrentOrganization { get; set; }

    /// <summary>
    /// Gets or sets the current ceremony year being scraped.
    /// </summary>
    public int? CurrentYear { get; set; }

    /// <summary>
    /// Gets or sets the overall progress percentage (0-100).
    /// </summary>
    public double ProgressPercent { get; set; }

    /// <summary>
    /// Gets or sets a descriptive status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Main scraper service interface for orchestrating the full scrape workflow.
/// </summary>
public interface IAwardsScraper
{
    /// <summary>
    /// Discovers all available award organizations from TMDB.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of discovered organizations.</returns>
    Task<IReadOnlyList<AwardOrganization>> DiscoverOrganizationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a full scrape of awards data according to the specified options.
    /// </summary>
    /// <param name="options">Options controlling scrape scope.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete awards database.</returns>
    Task<AwardsDatabase> ScrapeAsync(
        ScrapeOptions? options = null,
        IProgress<ScrapeProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
