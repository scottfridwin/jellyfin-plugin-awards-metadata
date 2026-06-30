using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AwardsMetadata.TagGeneration;

/// <summary>
/// JSON file-based implementation of managed tag tracking.
/// </summary>
public sealed class JsonManagedTagStore : IManagedTagStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly ILogger<JsonManagedTagStore> _logger;
    private Dictionary<Guid, HashSet<string>> _managedTags = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonManagedTagStore"/> class.
    /// </summary>
    /// <param name="filePath">Path to the managed tags JSON file.</param>
    /// <param name="logger">Logger instance.</param>
    public JsonManagedTagStore(string filePath, ILogger<JsonManagedTagStore> logger)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("Managed tag store initialized with path: {Path}", _filePath);
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetManagedTags(Guid itemId)
    {
        return _managedTags.TryGetValue(itemId, out var tags)
            ? tags
            : new HashSet<string>();
    }

    /// <inheritdoc />
    public void SetManagedTags(Guid itemId, IEnumerable<string> tags)
    {
        _managedTags[itemId] = [.. tags];
    }

    /// <inheritdoc />
    public void RemoveManagedTags(Guid itemId)
    {
        _managedTags.Remove(itemId);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> GetAllTrackedItemIds()
    {
        return _managedTags.Keys;
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            _logger.LogDebug("Creating managed tags directory: {Directory}", directory);
            Directory.CreateDirectory(directory);
        }

        // Convert to serializable format
        var data = _managedTags.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value.ToList());

        var totalTags = _managedTags.Values.Sum(v => v.Count);
        _logger.LogDebug("Saving managed tags: {Items} items, {Tags} total tags to {Path}", _managedTags.Count, totalTags, _filePath);

        var tempPath = _filePath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(data, SerializerOptions);

            // Atomic write: write to temp file, then rename to prevent corruption on crash
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _filePath, overwrite: true);

            _logger.LogInformation("Managed tags saved: {Items} items, {Tags} total tags", _managedTags.Count, totalTags);
        }
        catch (Exception ex)
        {
            // Clean up temp file if it was created but move failed
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
                // Best effort cleanup
            }

            _logger.LogError(ex, "Failed to save managed tags to {Path}", _filePath);
            throw;
        }
    }

    /// <summary>
    /// Maximum allowed file size for the managed tags file (50 MB).
    /// </summary>
    private const long MaxFileSize = 50 * 1024 * 1024;

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogDebug("Managed tags file not found at {Path}, starting with empty store", _filePath);
            _managedTags = [];
            return;
        }

        try
        {
            var fileInfo = new FileInfo(_filePath);
            if (fileInfo.Length > MaxFileSize)
            {
                _logger.LogError(
                    "Managed tags file at {Path} is too large ({Size} bytes, max {Max} bytes). Starting with empty store",
                    _filePath,
                    fileInfo.Length,
                    MaxFileSize);
                _managedTags = [];
                return;
            }

            _logger.LogDebug("Loading managed tags from {Path} ({Size} bytes)", _filePath, fileInfo.Length);
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, SerializerOptions);

            _managedTags = data?.ToDictionary(
                kvp => Guid.Parse(kvp.Key),
                kvp => new HashSet<string>(kvp.Value, StringComparer.Ordinal)) ?? [];

            var totalTags = _managedTags.Values.Sum(v => v.Count);
            _logger.LogInformation("Managed tags loaded: {Items} items, {Tags} total tags from {Path}", _managedTags.Count, totalTags, _filePath);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize managed tags from {Path}. Starting with empty store", _filePath);
            _managedTags = [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load managed tags from {Path}", _filePath);
            throw;
        }
    }
}
