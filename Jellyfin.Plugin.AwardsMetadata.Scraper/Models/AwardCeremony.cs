namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

/// <summary>
/// Represents a specific ceremony instance (e.g., "Academy Awards 2024").
/// </summary>
public sealed class AwardCeremony
{
    /// <summary>
    /// Gets or sets the slug of the parent organization.
    /// </summary>
    public string OrganizationSlug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the parent organization.
    /// </summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the year of the ceremony.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the relative URL for this ceremony on TMDB.
    /// </summary>
    public string RelativeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the categories within this ceremony.
    /// </summary>
    public List<AwardCategory> Categories { get; set; } = [];
}
