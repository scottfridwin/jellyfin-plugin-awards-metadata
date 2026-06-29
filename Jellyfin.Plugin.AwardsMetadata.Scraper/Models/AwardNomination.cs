namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

/// <summary>
/// Represents an individual nomination within a category.
/// </summary>
public sealed class AwardNomination
{
    /// <summary>
    /// Gets or sets the display name of the nominated item (movie title or person name).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this nomination won or was just nominated.
    /// </summary>
    public AwardResult Result { get; set; } = AwardResult.Nominee;

    /// <summary>
    /// Gets or sets the TMDB movie ID if available.
    /// </summary>
    public int? TmdbMovieId { get; set; }

    /// <summary>
    /// Gets or sets the TMDB person IDs associated with this nomination.
    /// </summary>
    public List<int> TmdbPersonIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the movie title when different from the nomination name.
    /// </summary>
    public string? MovieTitle { get; set; }
}
