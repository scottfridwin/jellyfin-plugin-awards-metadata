using System.Net;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Configuration;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Downloading;

/// <summary>
/// Downloads HTML from TMDB with rate limiting and retry support.
/// </summary>
public sealed class HttpHtmlDownloader : IHtmlDownloader, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ScraperOptions _options;
    private readonly ILogger<HttpHtmlDownloader> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTimeOffset _lastRequestTime = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpHtmlDownloader"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="options">Scraper configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public HttpHtmlDownloader(HttpClient httpClient, ScraperOptions options, ILogger<HttpHtmlDownloader> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
        if (!string.IsNullOrEmpty(_options.UserAgent))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        }
    }

    /// <inheritdoc />
    public async Task<string> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ApplyRateLimitAsync(cancellationToken).ConfigureAwait(false);
            return await DownloadWithRetryAsync(url, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _rateLimitSemaphore.Dispose();
    }

    private async Task ApplyRateLimitAsync(CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - _lastRequestTime;
        var requiredDelay = TimeSpan.FromMilliseconds(_options.RateLimitDelayMs);

        if (elapsed < requiredDelay)
        {
            var waitTime = requiredDelay - elapsed;
            _logger.LogDebug("Rate limiting: waiting {WaitMs}ms before next request", waitTime.TotalMilliseconds);
            await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> DownloadWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;
            try
            {
                _logger.LogDebug("Downloading {Url} (attempt {Attempt}/{MaxRetry})", url, attempt, _options.MaxRetryCount + 1);
                _lastRequestTime = DateTimeOffset.UtcNow;

                var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt > _options.MaxRetryCount)
                    {
                        _logger.LogError("Rate limited on {Url} after {Attempts} attempts", url, attempt);
                        response.EnsureSuccessStatusCode();
                    }

                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                    _logger.LogWarning("Rate limited on {Url}. Retry-After: {RetryAfterSeconds}s", url, retryAfter.TotalSeconds);
                    await Task.Delay(retryAfter, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt <= _options.MaxRetryCount)
            {
                _logger.LogWarning(ex, "Request to {Url} failed (attempt {Attempt}/{MaxRetry}). Retrying...", url, attempt, _options.MaxRetryCount + 1);
                var backoffDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                await Task.Delay(backoffDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt <= _options.MaxRetryCount)
            {
                _logger.LogWarning(ex, "Request to {Url} timed out (attempt {Attempt}/{MaxRetry}). Retrying...", url, attempt, _options.MaxRetryCount + 1);
                var backoffDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                await Task.Delay(backoffDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
