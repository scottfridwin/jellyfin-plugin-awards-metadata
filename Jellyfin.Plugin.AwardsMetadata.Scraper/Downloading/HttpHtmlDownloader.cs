using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Configuration;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Downloading;

/// <summary>
/// Downloads HTML from TMDB with rate limiting and retry support.
/// </summary>
public sealed class HttpHtmlDownloader : IHtmlDownloader, IDisposable
{
    /// <summary>
    /// Maximum allowed response size (10 MB). Prevents memory exhaustion from oversized responses.
    /// </summary>
    private const long MaxResponseSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum Retry-After delay to honor (60 seconds). Prevents a malicious server from stalling the task.
    /// </summary>
    private static readonly TimeSpan MaxRetryAfterDelay = TimeSpan.FromSeconds(60);

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

    private HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (!string.IsNullOrEmpty(_options.UserAgent))
        {
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);
        }

        return request;
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

                using var request = CreateRequest(url);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt > _options.MaxRetryCount)
                    {
                        _logger.LogError("Rate limited on {Url} after {Attempts} attempts", url, attempt);
                        response.EnsureSuccessStatusCode();
                    }

                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                    if (retryAfter > MaxRetryAfterDelay)
                    {
                        _logger.LogWarning(
                            "Retry-After on {Url} is {RetryAfterSeconds}s, capping to {MaxSeconds}s",
                            url,
                            retryAfter.TotalSeconds,
                            MaxRetryAfterDelay.TotalSeconds);
                        retryAfter = MaxRetryAfterDelay;
                    }

                    _logger.LogWarning("Rate limited on {Url}. Retry-After: {RetryAfterSeconds}s", url, retryAfter.TotalSeconds);
                    await Task.Delay(retryAfter, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                // Check content length before reading
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength > MaxResponseSizeBytes)
                {
                    throw new InvalidOperationException(
                        $"Response from {url} is too large ({contentLength} bytes, max {MaxResponseSizeBytes} bytes)");
                }

                // Read with a size-limited stream to handle cases where Content-Length is absent
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var limitedReader = new StreamReader(new LimitedStream(stream, MaxResponseSizeBytes));
                return await limitedReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// A stream wrapper that throws if more than the specified number of bytes are read.
    /// </summary>
    private sealed class LimitedStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maxBytes;
        private long _totalRead;

        public LimitedStream(Stream inner, long maxBytes)
        {
            _inner = inner;
            _maxBytes = maxBytes;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _inner.Read(buffer, offset, count);
            _totalRead += bytesRead;
            if (_totalRead > _maxBytes)
            {
                throw new InvalidOperationException($"Response body exceeded maximum allowed size of {_maxBytes} bytes");
            }

            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesRead = await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            _totalRead += bytesRead;
            if (_totalRead > _maxBytes)
            {
                throw new InvalidOperationException($"Response body exceeded maximum allowed size of {_maxBytes} bytes");
            }

            return bytesRead;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytesRead = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _totalRead += bytesRead;
            if (_totalRead > _maxBytes)
            {
                throw new InvalidOperationException($"Response body exceeded maximum allowed size of {_maxBytes} bytes");
            }

            return bytesRead;
        }

        public override void Flush() => _inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
