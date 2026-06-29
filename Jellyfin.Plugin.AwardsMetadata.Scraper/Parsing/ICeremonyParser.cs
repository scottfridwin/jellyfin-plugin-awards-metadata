using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Parsing;

/// <summary>
/// Parses a TMDB ceremony page into structured award data.
/// </summary>
public interface ICeremonyParser
{
    /// <summary>
    /// Parses the HTML of a ceremony page into an <see cref="AwardCeremony"/> model.
    /// </summary>
    /// <param name="html">The HTML content of the ceremony page.</param>
    /// <param name="organization">The parent organization.</param>
    /// <param name="ceremonyReference">Reference to the ceremony being parsed.</param>
    /// <returns>A parsed ceremony with categories and nominations.</returns>
    AwardCeremony ParseCeremony(string html, AwardOrganization organization, CeremonyReference ceremonyReference);
}
