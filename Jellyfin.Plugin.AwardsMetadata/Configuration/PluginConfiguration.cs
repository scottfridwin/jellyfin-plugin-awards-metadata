using System.Text.Json.Serialization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AwardsMetadata.Configuration;

/// <summary>
/// Plugin configuration for the Awards Metadata plugin.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Minimum allowed rate limit delay in milliseconds.
    /// </summary>
    public const int MinRateLimitDelayMs = 100;

    /// <summary>
    /// Maximum allowed rate limit delay in milliseconds.
    /// </summary>
    public const int MaxRateLimitDelayMs = 60000;

    /// <summary>
    /// Maximum allowed retry count.
    /// </summary>
    public const int MaxAllowedRetryCount = 10;

    /// <summary>
    /// Minimum allowed request timeout in seconds.
    /// </summary>
    public const int MinRequestTimeoutSeconds = 5;

    /// <summary>
    /// Maximum allowed request timeout in seconds.
    /// </summary>
    public const int MaxRequestTimeoutSeconds = 120;

    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "www.themoviedb.org",
        "themoviedb.org",
    };

    private int _rateLimitDelayMs = 1000;
    private int _maxRetryCount = 3;
    private int _requestTimeoutSeconds = 30;

    /// <summary>
    /// Gets or sets the minimum delay between HTTP requests in milliseconds.
    /// </summary>
    public int RateLimitDelayMs
    {
        get => _rateLimitDelayMs;
        set => _rateLimitDelayMs = Math.Clamp(value, MinRateLimitDelayMs, MaxRateLimitDelayMs);
    }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed requests.
    /// </summary>
    public int MaxRetryCount
    {
        get => _maxRetryCount;
        set => _maxRetryCount = Math.Clamp(value, 0, MaxAllowedRetryCount);
    }

    /// <summary>
    /// Gets or sets the HTTP request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds
    {
        get => _requestTimeoutSeconds;
        set => _requestTimeoutSeconds = Math.Clamp(value, MinRequestTimeoutSeconds, MaxRequestTimeoutSeconds);
    }

    /// <summary>
    /// Gets or sets the path for awards database storage.
    /// When empty, uses the plugin data directory.
    /// </summary>
    public string AwardsStoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the organization slugs to scrape.
    /// Empty means none (user must select after discovery).
    /// </summary>
    public List<string> EnabledOrganizations { get; set; } = [];

    /// <summary>
    /// Gets or sets the discovered available organizations (populated by the discover action).
    /// </summary>
    public List<DiscoveredOrganization> AvailableOrganizations { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether winner tags should be generated.
    /// </summary>
    public bool EnableWinnerTagging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether nominee tags should be generated.
    /// </summary>
    public bool EnableNomineeTagging { get; set; } = true;

    /// <summary>
    /// Gets or sets the tag format template.
    /// Available placeholders: {awardType}, {awardResult}, {awardCategory}, {awardYear}
    /// </summary>
    public string TagFormat { get; set; } = "award-{awardType}-{awardResult}-{awardCategory}-{awardYear}";

    /// <summary>
    /// Gets or sets the TMDB base URL. Not exposed in the UI; intended for testing.
    /// </summary>
    public string TmdbBaseUrl { get; set; } = "https://www.themoviedb.org";

    /// <summary>
    /// Validates that the configured TMDB base URL is safe to use (HTTPS + allowed host).
    /// </summary>
    /// <returns>The validated base URL.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the URL is invalid or targets a disallowed host.</exception>
    public string GetValidatedTmdbBaseUrl()
    {
        if (string.IsNullOrWhiteSpace(TmdbBaseUrl))
        {
            return "https://www.themoviedb.org";
        }

        if (!Uri.TryCreate(TmdbBaseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"TmdbBaseUrl is not a valid absolute URL: {TmdbBaseUrl}");
        }

        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"TmdbBaseUrl must use HTTPS. Got: {uri.Scheme}");
        }

        if (!AllowedHosts.Contains(uri.Host))
        {
            throw new InvalidOperationException($"TmdbBaseUrl host '{uri.Host}' is not in the allow list. Allowed: {string.Join(", ", AllowedHosts)}");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}

/// <summary>
/// Represents a discovered award organization available for scraping.
/// </summary>
public sealed class DiscoveredOrganization
{
    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the slug identifier.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
}
