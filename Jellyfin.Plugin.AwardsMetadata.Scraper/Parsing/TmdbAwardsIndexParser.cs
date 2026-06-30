using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Parsing;

/// <summary>
/// Parses the TMDB awards index page to discover organizations and ceremony lists.
/// </summary>
public sealed partial class TmdbAwardsIndexParser : IAwardsIndexParser
{
    private readonly ILogger<TmdbAwardsIndexParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbAwardsIndexParser"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public TmdbAwardsIndexParser(ILogger<TmdbAwardsIndexParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IReadOnlyList<AwardOrganization> ParseIndex(string html)
    {
        ArgumentException.ThrowIfNullOrEmpty(html);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var organizations = new List<AwardOrganization>();

        // TMDB awards index typically lists organizations as links
        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/award/')]");
        if (links is null)
        {
            _logger.LogWarning("No award organization links found on the awards index page");
            return organizations;
        }

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", string.Empty);
            var name = link.InnerText?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(name))
            {
                continue;
            }

            // Skip links that are just /award/ (the index itself) or contain ceremony years
            if (href == "/award" || href == "/award/")
            {
                continue;
            }

            // Extract slug from href like "/award/academy-awards"
            var match = AwardSlugRegex().Match(href);
            if (!match.Success)
            {
                continue;
            }

            var slug = match.Groups[1].Value;

            // Avoid duplicates
            if (organizations.Exists(o => o.Slug == slug))
            {
                continue;
            }

            organizations.Add(new AwardOrganization
            {
                Name = name,
                Slug = slug,
                RelativeUrl = href,
            });

            _logger.LogDebug("Discovered award organization: {Name} ({Slug})", name, slug);
        }

        _logger.LogInformation("Discovered {Count} award organizations", organizations.Count);
        return organizations;
    }

    /// <inheritdoc />
    public IReadOnlyList<CeremonyReference> ParseCeremonyList(string html, AwardOrganization organization)
    {
        ArgumentException.ThrowIfNullOrEmpty(html);
        ArgumentNullException.ThrowIfNull(organization);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var ceremonies = new List<CeremonyReference>();

        // Look for ceremony links within the organization page
        // TMDB uses URLs like /award/1-academy-awards/ceremony/97
        var links = doc.DocumentNode.SelectNodes($"//a[contains(@href, '/award/{organization.Slug}/ceremony/')]");
        if (links is null)
        {
            _logger.LogWarning("No ceremony links found for organization {Organization}", organization.Name);
            return ceremonies;
        }

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", string.Empty);

            // Match links like /award/1-academy-awards/ceremony/97
            var ceremonyMatch = CeremonyUrlRegex().Match(href);
            if (!ceremonyMatch.Success)
            {
                continue;
            }

            // Extract year from link text like "97th Academy Awards (2025)"
            var linkText = link.InnerText?.Trim() ?? string.Empty;
            var yearMatch = YearInTextRegex().Match(linkText);
            if (!yearMatch.Success)
            {
                _logger.LogDebug(
                    "Ceremony link found but could not extract year from text: '{Text}' (href: {Href})",
                    linkText,
                    href);
                continue;
            }

            var year = int.Parse(yearMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

            // Avoid duplicates
            if (ceremonies.Exists(c => c.Year == year))
            {
                continue;
            }

            ceremonies.Add(new CeremonyReference
            {
                Year = year,
                RelativeUrl = href,
            });
        }

        ceremonies.Sort((a, b) => a.Year.CompareTo(b.Year));
        _logger.LogInformation("Found {Count} ceremonies for {Organization}", ceremonies.Count, organization.Name);
        return ceremonies;
    }

    [GeneratedRegex(@"/award/([a-z0-9-]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AwardSlugRegex();

    [GeneratedRegex(@"/award/[a-z0-9-]+/ceremony/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CeremonyUrlRegex();

    [GeneratedRegex(@"\((\d{4})\)")]
    private static partial Regex YearInTextRegex();
}
