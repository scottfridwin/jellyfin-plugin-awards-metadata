using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Parsing;

/// <summary>
/// Parses the TMDB awards index page to discover available award organizations.
/// </summary>
public interface IAwardsIndexParser
{
    /// <summary>
    /// Parses the awards index HTML to extract available organizations.
    /// </summary>
    /// <param name="html">The HTML content of the awards index page.</param>
    /// <returns>A list of discovered award organizations.</returns>
    IReadOnlyList<AwardOrganization> ParseIndex(string html);

    /// <summary>
    /// Parses an organization page to discover available ceremony years.
    /// </summary>
    /// <param name="html">The HTML content of the organization page.</param>
    /// <param name="organization">The organization being parsed.</param>
    /// <returns>A list of relative URLs for each ceremony year.</returns>
    IReadOnlyList<CeremonyReference> ParseCeremonyList(string html, AwardOrganization organization);
}

/// <summary>
/// A reference to a specific ceremony year page.
/// </summary>
public sealed class CeremonyReference
{
    /// <summary>
    /// Gets or sets the year of the ceremony.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the relative URL path to the ceremony page.
    /// </summary>
    public string RelativeUrl { get; set; } = string.Empty;
}
