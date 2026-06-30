using System.Net.Mime;
using Jellyfin.Plugin.AwardsMetadata.Configuration;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Configuration;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Downloading;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Parsing;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Services;

namespace Jellyfin.Plugin.AwardsMetadata.Api;

/// <summary>
/// API controller for Awards Metadata plugin actions.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("AwardsMetadata")]
[Produces(MediaTypeNames.Application.Json)]
public class AwardsMetadataController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AwardsMetadataController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AwardsMetadataController"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public AwardsMetadataController(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AwardsMetadataController>();
    }

    /// <summary>
    /// Discovers available award organizations from TMDB.
    /// </summary>
    /// <returns>Success status.</returns>
    [HttpPost("DiscoverOrganizations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DiscoverOrganizations()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            _logger.LogError("DiscoverOrganizations called but Plugin.Instance is null. The plugin may not have been initialized");
            return StatusCode(StatusCodes.Status500InternalServerError, "Plugin not initialized");
        }

        var config = plugin.Configuration;

        string validatedBaseUrl;
        try
        {
            validatedBaseUrl = config.GetValidatedTmdbBaseUrl();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid TmdbBaseUrl configuration");
            return StatusCode(StatusCodes.Status500InternalServerError, "Invalid TmdbBaseUrl configuration");
        }

        _logger.LogDebug(
            "DiscoverOrganizations: BaseUrl={BaseUrl}, RateLimitDelayMs={RateLimit}, MaxRetryCount={MaxRetry}, RequestTimeoutSeconds={Timeout}",
            validatedBaseUrl,
            config.RateLimitDelayMs,
            config.MaxRetryCount,
            config.RequestTimeoutSeconds);

        var scraperOptions = new ScraperOptions
        {
            BaseUrl = validatedBaseUrl,
            RateLimitDelayMs = config.RateLimitDelayMs,
            MaxRetryCount = config.MaxRetryCount,
            RequestTimeoutSeconds = config.RequestTimeoutSeconds,
        };

        using var httpClient = _httpClientFactory.CreateClient(nameof(AwardsMetadataController));
        using var downloader = new HttpHtmlDownloader(
            httpClient,
            scraperOptions,
            _loggerFactory.CreateLogger<HttpHtmlDownloader>());

        var indexParser = new TmdbAwardsIndexParser(_loggerFactory.CreateLogger<TmdbAwardsIndexParser>());
        var ceremonyParser = new TmdbCeremonyParser(_loggerFactory.CreateLogger<TmdbCeremonyParser>());

        var scraper = new TmdbAwardsScraperService(
            downloader,
            indexParser,
            ceremonyParser,
            scraperOptions,
            _loggerFactory.CreateLogger<TmdbAwardsScraperService>());

        try
        {
            var organizations = await scraper.DiscoverOrganizationsAsync(HttpContext.RequestAborted).ConfigureAwait(false);

            config.AvailableOrganizations = organizations
                .Select(o => new DiscoveredOrganization { Name = o.Name, Slug = o.Slug })
                .ToList();

            plugin.SaveConfiguration();

            _logger.LogInformation("Discovered {Count} organizations", organizations.Count);
            return Ok(new { Count = organizations.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover organizations");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to discover organizations");
        }
    }
}
