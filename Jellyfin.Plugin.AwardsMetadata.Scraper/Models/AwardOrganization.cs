namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

/// <summary>
/// Represents an award-granting organization discovered from TMDB.
/// </summary>
public sealed class AwardOrganization
{
    /// <summary>
    /// Gets or sets the display name of the organization (e.g., "Academy Awards").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL slug used by TMDB (e.g., "academy-awards").
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full relative URL path on TMDB.
    /// </summary>
    public string RelativeUrl { get; set; } = string.Empty;
}
