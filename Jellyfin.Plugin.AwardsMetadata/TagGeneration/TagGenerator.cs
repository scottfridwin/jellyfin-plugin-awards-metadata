using Jellyfin.Plugin.AwardsMetadata.Scraper.Normalization;

namespace Jellyfin.Plugin.AwardsMetadata.TagGeneration;

/// <summary>
/// Generates tag strings from award data using a configurable format template.
/// </summary>
public interface ITagGenerator
{
    /// <summary>
    /// Generates a tag string for a given nomination.
    /// </summary>
    /// <param name="awardType">The award organization type (e.g., "Academy Awards").</param>
    /// <param name="awardResult">The result (e.g., "Winner" or "Nominee").</param>
    /// <param name="awardCategory">The category name (e.g., "Best Picture").</param>
    /// <param name="awardYear">The ceremony year.</param>
    /// <returns>The generated tag string.</returns>
    string GenerateTag(string awardType, string awardResult, string awardCategory, int awardYear);
}

/// <summary>
/// Generates tags using a configurable format template and text normalization.
/// </summary>
public sealed class TagGenerator : ITagGenerator
{
    private readonly string _format;
    private readonly ITextNormalizer _normalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagGenerator"/> class.
    /// </summary>
    /// <param name="format">The tag format template with placeholders.</param>
    /// <param name="normalizer">Text normalizer for slug generation.</param>
    public TagGenerator(string format, ITextNormalizer normalizer)
    {
        _format = format ?? throw new ArgumentNullException(nameof(format));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
    }

    /// <inheritdoc />
    public string GenerateTag(string awardType, string awardResult, string awardCategory, int awardYear)
    {
        var tag = _format
            .Replace("{awardType}", _normalizer.Normalize(awardType), StringComparison.OrdinalIgnoreCase)
            .Replace("{awardResult}", _normalizer.Normalize(awardResult), StringComparison.OrdinalIgnoreCase)
            .Replace("{awardCategory}", _normalizer.Normalize(awardCategory), StringComparison.OrdinalIgnoreCase)
            .Replace("{awardYear}", awardYear.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

        return tag;
    }
}
