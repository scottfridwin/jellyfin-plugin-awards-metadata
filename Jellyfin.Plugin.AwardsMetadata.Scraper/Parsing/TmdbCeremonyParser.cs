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
        // TMDB ceremony pages use div elements with id="category-N" for each category
        // Each contains an h4 header and nomination cards
        var categorySections = doc.DocumentNode.SelectNodes("//div[starts-with(@id, 'category-')]");

        if (categorySections is null)
        {
            _logger.LogDebug(
                "No category-N sections found for {Organization} {Year}, trying h4 header fallback",
                ceremony.OrganizationName,
                ceremony.Year);
            ParseCategoriesByH4(doc, ceremony);
            return;
        }

        _logger.LogDebug(
            "Found {Count} category sections for {Organization} {Year}",
            categorySections.Count,
            ceremony.OrganizationName,
            ceremony.Year);

        foreach (var section in categorySections)
        {
            var category = ParseCategorySection(section, ceremony);
            if (category is not null && category.Nominations.Count > 0)
            {
                ceremony.Categories.Add(category);
                _logger.LogDebug(
                    "Parsed category '{Category}': {NominationCount} nominations",
                    category.Name,
                    category.Nominations.Count);
            }
        }
    }

    private void ParseCategoriesByH4(HtmlDocument doc, AwardCeremony ceremony)
    {
        // Fallback: find h4 headers and look for nomination cards in their parent containers
        var headers = doc.DocumentNode.SelectNodes("//h4");
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
            var categoryName = HtmlEntity.DeEntitize(header.InnerText?.Trim() ?? string.Empty);
            if (string.IsNullOrEmpty(categoryName))
            {
                continue;
            }

            // Use the header's parent container to find nomination cards
            var container = header.ParentNode;
            if (container is null)
            {
                continue;
            }

            var category = new AwardCategory { Name = categoryName };
            ParseNominationCards(container, category);

            if (category.Nominations.Count > 0)
            {
                ceremony.Categories.Add(category);
            }
        }
    }

    private AwardCategory? ParseCategorySection(HtmlNode section, AwardCeremony ceremony)
    {
        // Extract category name from h4 header within section
        var header = section.SelectSingleNode(".//h4")
            ?? section.SelectSingleNode(".//h3")
            ?? section.SelectSingleNode(".//h2");

        var categoryName = HtmlEntity.DeEntitize(header?.InnerText?.Trim() ?? string.Empty);
        if (string.IsNullOrEmpty(categoryName))
        {
            _logger.LogDebug(
                "Skipping category section with no name in {Organization} {Year}",
                ceremony.OrganizationName,
                ceremony.Year);
            return null;
        }

        var category = new AwardCategory { Name = categoryName };

        ParseNominationCards(section, category);

        return category;
    }

    private void ParseNominationCards(HtmlNode container, AwardCategory category)
    {
        // TMDB uses div elements with class "comp:nomination-card" for each nomination
        var cards = container.SelectNodes(".//*[contains(@class, 'comp:nomination-card')]");

        if (cards is null)
        {
            _logger.LogDebug("No nomination cards found in category '{Category}'", category.Name);
            return;
        }

        foreach (var card in cards)
        {
            var nomination = ParseNominationCard(card);
            if (nomination is not null)
            {
                category.Nominations.Add(nomination);
            }
        }
    }

    private AwardNomination? ParseNominationCard(HtmlNode card)
    {
        // Determine winner/nominee status from the <bdi> text within the status badge
        // Winners have <p class="status ... bg-accent-green ..."><bdi>Winner</bdi></p>
        // Nominees have <p class="status ... bg-gray-500 ..."><bdi>Nominee</bdi></p>
        var statusNode = card.SelectSingleNode(".//bdi");
        var statusText = statusNode?.InnerText?.Trim() ?? string.Empty;
        var isWinner = statusText.Equals("Winner", StringComparison.OrdinalIgnoreCase)
            || card.GetAttributeValue("class", string.Empty).Contains("shadow-accent-green", StringComparison.OrdinalIgnoreCase);

        // Extract movie link - TMDB uses <a href="/movie/872585-oppenheimer">
        var movieLink = card.SelectSingleNode(".//a[contains(@href, '/movie/')]");

        if (movieLink is null)
        {
            _logger.LogDebug("No movie link found in nomination card, skipping");
            return null;
        }

        var movieHref = movieLink.GetAttributeValue("href", string.Empty);
        var movieIdMatch = TmdbMovieIdRegex().Match(movieHref);
        if (!movieIdMatch.Success)
        {
            _logger.LogDebug("Could not extract TMDB movie ID from href: {Href}", movieHref);
            return null;
        }

        var tmdbMovieId = int.Parse(movieIdMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        // Extract movie title from <h2><span>Title</span></h2> inside the card
        var titleNode = card.SelectSingleNode(".//h2//span")
            ?? card.SelectSingleNode(".//h2");
        var movieTitle = HtmlEntity.DeEntitize(titleNode?.InnerText?.Trim() ?? string.Empty);

        if (string.IsNullOrEmpty(movieTitle))
        {
            // Fallback: use the alt text from the poster image
            var imgNode = card.SelectSingleNode(".//img[@alt]");
            movieTitle = HtmlEntity.DeEntitize(imgNode?.GetAttributeValue("alt", string.Empty) ?? string.Empty);
        }

        if (string.IsNullOrEmpty(movieTitle))
        {
            _logger.LogDebug("No title found for nomination card with movie ID {MovieId}", tmdbMovieId);
            return null;
        }

        var nomination = new AwardNomination
        {
            Name = movieTitle,
            MovieTitle = movieTitle,
            Result = isWinner ? AwardResult.Winner : AwardResult.Nominee,
            TmdbMovieId = tmdbMovieId,
        };

        // Extract TMDB person IDs from person links
        var personLinks = card.SelectNodes(".//a[contains(@href, '/person/')]");
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

        _logger.LogDebug(
            "Parsed nomination: '{Title}' (TMDB movie {MovieId}), Result={Result}, Persons={PersonCount}",
            nomination.Name,
            nomination.TmdbMovieId,
            nomination.Result,
            nomination.TmdbPersonIds.Count);

        return nomination;
    }

    [GeneratedRegex(@"/movie/(\d+)")]
    private static partial Regex TmdbMovieIdRegex();

    [GeneratedRegex(@"/person/(\d+)")]
    private static partial Regex TmdbPersonIdRegex();
}
