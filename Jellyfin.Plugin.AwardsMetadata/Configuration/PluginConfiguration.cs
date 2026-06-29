using System.Text.Json.Serialization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AwardsMetadata.Configuration;

/// <summary>
/// Plugin configuration for the Awards Metadata plugin.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
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
    /// Gets or sets a value indicating whether the advanced TMDB endpoint setting is visible.
    /// </summary>
    public bool EnableAdvancedMode { get; set; }

    /// <summary>
    /// Gets or sets the TMDB base URL (only used in advanced/debug mode).
    /// </summary>
    public string TmdbBaseUrl { get; set; } = "https://www.themoviedb.org";
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
