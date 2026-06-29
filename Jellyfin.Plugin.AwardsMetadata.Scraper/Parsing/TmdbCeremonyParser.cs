using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Parsing;

/// <summary>
/// Parses a TMDB ceremony page into structured award categories and nominations.
/// </summary>
public sealed partial class TmdbCeremonyParser : ICeremonyParser
{
    private readonly ILogger<TmdbCeremonyParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbCeremonyParser"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public TmdbCeremonyParser(ILogger<TmdbCeremonyParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public AwardCeremony ParseCeremony(string html, AwardOrganization organization, CeremonyReference ceremonyReference)
    {
        ArgumentException.ThrowIfNullOrEmpty(html);
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(ceremonyReference);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var ceremony = new AwardCeremony
        {
            OrganizationSlug = organization.Slug,
            OrganizationName = organization.Name,
            Year = ceremonyReference.Year,
            RelativeUrl = ceremonyReference.RelativeUrl,
        };

        ParseCategories(doc, ceremony);

        var totalNominations = ceremony.Categories.Sum(c => c.Nominations.Count);
        var totalWinners = ceremony.Categories.Sum(c => c.Nominations.Count(n => n.Result == AwardResult.Winner));

        _logger.LogInformation(
            "Parsed {Organization} {Year}: {Categories} categories, {Nominations} nominations, {Winners} winners",
            organization.Name,
            ceremonyReference.Year,
            ceremony.Categories.Count,
            totalNominations,
            totalWinners);

        return ceremony;
    }

    private void ParseCategories(HtmlDocument doc, AwardCeremony ceremony)
    {
        // TMDB ceremony pages use panels/sections for each category
        // Look for category sections - TMDB typically uses a structure with category headers and nominee lists
        var categorySections = doc.DocumentNode.SelectNodes("//div[contains(@class, 'award_category')]")
            ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'category')]")
            ?? doc.DocumentNode.SelectNodes("//section[contains(@class, 'panel')]");

        if (categorySections is null)
        {
            // Fallback: try to find h3 headers that denote categories
            ParseCategoriesByHeaders(doc, ceremony);
            return;
        }

        foreach (var section in categorySections)
        {
            var category = ParseCategorySection(section, ceremony);
            if (category is not null && category.Nominations.Count > 0)
            {
                ceremony.Categories.Add(category);
            }
        }
    }

    private void ParseCategoriesByHeaders(HtmlDocument doc, AwardCeremony ceremony)
    {
        // Alternative parsing strategy using header elements
        var headers = doc.DocumentNode.SelectNodes("//h3") ?? doc.DocumentNode.SelectNodes("//h2");
        if (headers is null)
        {
            _logger.LogWarning(
                "No category headers found for {Organization} {Year}",
                ceremony.OrganizationName,
                ceremony.Year);
            return;
        }

        foreach (var header in headers)
        {
            var categoryName = header.InnerText?.Trim();
            if (string.IsNullOrEmpty(categoryName))
            {
                continue;
            }

            var category = new AwardCategory { Name = categoryName };

            // Look for nomination items following this header
            var nextSibling = header.NextSibling;
            while (nextSibling is not null)
            {
                if (nextSibling.Name == "h3" || nextSibling.Name == "h2")
                {
                    break;
                }

                if (nextSibling.NodeType == HtmlNodeType.Element)
                {
                    ParseNominationsFromNode(nextSibling, category);
                }

                nextSibling = nextSibling.NextSibling;
            }

            if (category.Nominations.Count > 0)
            {
                ceremony.Categories.Add(category);
            }
        }
    }

    private AwardCategory? ParseCategorySection(HtmlNode section, AwardCeremony ceremony)
    {
        // Extract category name from header within section
        var header = section.SelectSingleNode(".//h3")
            ?? section.SelectSingleNode(".//h2")
            ?? section.SelectSingleNode(".//*[contains(@class, 'title')]");

        var categoryName = header?.InnerText?.Trim();
        if (string.IsNullOrEmpty(categoryName))
        {
            _logger.LogDebug(
                "Skipping category section with no name in {Organization} {Year}",
                ceremony.OrganizationName,
                ceremony.Year);
            return null;
        }

        var category = new AwardCategory { Name = categoryName };

        // Find nomination items within this section
        var nomineeNodes = section.SelectNodes(".//*[contains(@class, 'nominee')]")
            ?? section.SelectNodes(".//li")
            ?? section.SelectNodes(".//*[contains(@class, 'card')]");

        if (nomineeNodes is not null)
        {
            foreach (var nomineeNode in nomineeNodes)
            {
                var nomination = ParseNomination(nomineeNode);
                if (nomination is not null)
                {
                    category.Nominations.Add(nomination);
                }
            }
        }

        return category;
    }

    private void ParseNominationsFromNode(HtmlNode node, AwardCategory category)
    {
        var items = node.SelectNodes(".//li") ?? node.SelectNodes(".//*[contains(@class, 'nominee')]");
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            var nomination = ParseNomination(item);
            if (nomination is not null)
            {
                category.Nominations.Add(nomination);
            }
        }
    }

    private AwardNomination? ParseNomination(HtmlNode node)
    {
        // Determine if this is a winner
        var isWinner = node.GetAttributeValue("class", string.Empty).Contains("winner", StringComparison.OrdinalIgnoreCase)
            || node.SelectSingleNode(".//*[contains(@class, 'winner')]") is not null
            || node.SelectSingleNode(".//*[contains(@class, 'trophy')]") is not null;

        // Extract movie link and TMDB ID
        var movieLink = node.SelectSingleNode(".//a[contains(@href, '/movie/')]");
        var personLink = node.SelectSingleNode(".//a[contains(@href, '/person/')]");

        // Get display name
        var nameNode = movieLink ?? personLink ?? node.SelectSingleNode(".//*[contains(@class, 'name')]");
        var name = nameNode?.InnerText?.Trim() ?? node.InnerText?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var nomination = new AwardNomination
        {
            Name = HtmlEntity.DeEntitize(name) ?? name,
            Result = isWinner ? AwardResult.Winner : AwardResult.Nominee,
        };

        // Extract TMDB movie ID from URL
        if (movieLink is not null)
        {
            var movieHref = movieLink.GetAttributeValue("href", string.Empty);
            var movieIdMatch = TmdbMovieIdRegex().Match(movieHref);
            if (movieIdMatch.Success)
            {
                nomination.TmdbMovieId = int.Parse(movieIdMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                nomination.MovieTitle = HtmlEntity.DeEntitize(movieLink.InnerText?.Trim() ?? string.Empty);
            }
        }

        // Extract TMDB person IDs
        var personLinks = node.SelectNodes(".//a[contains(@href, '/person/')]");
        if (personLinks is not null)
        {
            foreach (var pLink in personLinks)
            {
                var personHref = pLink.GetAttributeValue("href", string.Empty);
                var personIdMatch = TmdbPersonIdRegex().Match(personHref);
                if (personIdMatch.Success)
                {
                    var personId = int.Parse(personIdMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (!nomination.TmdbPersonIds.Contains(personId))
                    {
                        nomination.TmdbPersonIds.Add(personId);
                    }
                }
            }
        }

        return nomination;
    }

    [GeneratedRegex(@"/movie/(\d+)")]
    private static partial Regex TmdbMovieIdRegex();

    [GeneratedRegex(@"/person/(\d+)")]
    private static partial Regex TmdbPersonIdRegex();
}
