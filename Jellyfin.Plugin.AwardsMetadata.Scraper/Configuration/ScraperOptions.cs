namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Configuration;

/// <summary>
/// Configuration options for the TMDB awards scraper.
/// </summary>
public sealed class ScraperOptions
{
    /// <summary>
    /// Gets or sets the base URL for the TMDB website.
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.themoviedb.org";

    /// <summary>
    /// Gets or sets the minimum delay between HTTP requests in milliseconds.
    /// </summary>
    public int RateLimitDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed requests.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the HTTP request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the user agent string to use for HTTP requests.
    /// </summary>
    public string UserAgent { get; set; } = "TmdbAwardsScraper/1.0";
}
