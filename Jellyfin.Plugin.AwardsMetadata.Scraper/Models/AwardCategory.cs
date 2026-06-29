namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

/// <summary>
/// Represents a category within an award ceremony (e.g., "Best Picture").
/// </summary>
public sealed class AwardCategory
{
    /// <summary>
    /// Gets or sets the display name of the category.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the nominations in this category.
    /// </summary>
    public List<AwardNomination> Nominations { get; set; } = [];
}
