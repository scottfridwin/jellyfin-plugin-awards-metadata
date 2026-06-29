namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Downloading;

/// <summary>
/// Abstraction for downloading HTML content from a URL.
/// </summary>
public interface IHtmlDownloader
{
    /// <summary>
    /// Downloads the HTML content from the specified URL.
    /// </summary>
    /// <param name="url">The absolute URL to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTML content as a string.</returns>
    Task<string> DownloadAsync(string url, CancellationToken cancellationToken = default);
}
