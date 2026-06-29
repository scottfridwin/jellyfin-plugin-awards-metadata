namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

/// <summary>
/// Root container for all scraped awards data with metadata for schema versioning.
/// </summary>
public sealed class AwardsDatabase
{
    /// <summary>
    /// Gets or sets the schema version for future migration support.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the UTC timestamp when this database was generated or last updated.
    /// </summary>
    public DateTimeOffset GeneratedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets information about the source of this data.
    /// </summary>
    public string Source { get; set; } = "TMDB";

    /// <summary>
    /// Gets or sets the base URL that was used when scraping.
    /// </summary>
    public string SourceBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the discovered award organizations.
    /// </summary>
    public List<AwardOrganization> Organizations { get; set; } = [];

    /// <summary>
    /// Gets or sets all scraped ceremonies.
    /// </summary>
    public List<AwardCeremony> Ceremonies { get; set; } = [];
}
