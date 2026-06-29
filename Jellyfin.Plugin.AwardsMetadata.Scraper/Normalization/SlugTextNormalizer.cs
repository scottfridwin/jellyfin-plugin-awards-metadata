using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Normalization;

/// <summary>
/// Normalizes text into URL-friendly slug format (lowercase, hyphen-separated).
/// </summary>
public sealed partial class SlugTextNormalizer : ITextNormalizer
{
    /// <inheritdoc />
    public string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Normalize unicode characters
        var normalized = input.Normalize(NormalizationForm.FormKD);

        // Convert to lowercase
        normalized = normalized.ToLower(CultureInfo.InvariantCulture);

        // Remove diacritics
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        normalized = sb.ToString();

        // Replace non-alphanumeric characters with hyphens
        normalized = NonAlphanumericRegex().Replace(normalized, "-");

        // Collapse multiple hyphens
        normalized = MultipleHyphensRegex().Replace(normalized, "-");

        // Trim hyphens from start and end
        normalized = normalized.Trim('-');

        return normalized;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleHyphensRegex();
}
