namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Normalization;

/// <summary>
/// Provides text normalization for generating consistent slug values.
/// </summary>
public interface ITextNormalizer
{
    /// <summary>
    /// Normalizes a display string into a slug-formatted value.
    /// </summary>
    /// <param name="input">The raw text to normalize.</param>
    /// <returns>A normalized slug string.</returns>
    string Normalize(string input);
}
