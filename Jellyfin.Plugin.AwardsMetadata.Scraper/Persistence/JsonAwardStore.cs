using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Persistence;

/// <summary>
/// Persists the awards database as a JSON file.
/// </summary>
public sealed class JsonAwardStore : IAwardStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _filePath;
    private readonly ILogger<JsonAwardStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAwardStore"/> class.
    /// </summary>
    /// <param name="filePath">The full path to the JSON file.</param>
    /// <param name="logger">Logger instance.</param>
    public JsonAwardStore(string filePath, ILogger<JsonAwardStore> logger)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SaveAsync(AwardsDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            _logger.LogDebug("Creating awards database directory: {Directory}", directory);
            Directory.CreateDirectory(directory);
        }

        database.GeneratedUtc = DateTimeOffset.UtcNow;

        _logger.LogDebug("Serializing awards database to JSON");
        var json = JsonSerializer.Serialize(database, SerializerOptions);
        _logger.LogDebug("Awards database serialized ({Size} characters), writing to {Path}", json.Length, _filePath);

        try
        {
            // Atomic write: write to temp file, then rename to prevent corruption on crash
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write awards database to {Path}", _filePath);
            throw;
        }

        var totalNominations = database.Ceremonies.Sum(c => c.Categories.Sum(cat => cat.Nominations.Count));
        _logger.LogInformation(
            "Awards database saved to {Path} ({Organizations} organizations, {Ceremonies} ceremonies, {Nominations} nominations)",
            _filePath,
            database.Organizations.Count,
            database.Ceremonies.Count,
            totalNominations);
    }

    /// <summary>
    /// Maximum allowed file size for the awards database file (200 MB).
    /// </summary>
    private const long MaxFileSize = 200 * 1024 * 1024;

    /// <inheritdoc />
    public async Task<AwardsDatabase?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogDebug("Awards database file not found at {Path}", _filePath);
            return null;
        }

        var fileInfo = new FileInfo(_filePath);
        if (fileInfo.Length > MaxFileSize)
        {
            _logger.LogError(
                "Awards database file at {Path} is too large ({Size} bytes, max {Max} bytes). Refusing to load",
                _filePath,
                fileInfo.Length,
                MaxFileSize);
            return null;
        }

        _logger.LogDebug("Reading awards database file ({Size} bytes)", fileInfo.Length);
        var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);

        AwardsDatabase? database;
        try
        {
            database = JsonSerializer.Deserialize<AwardsDatabase>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize awards database from {Path}. The file may be corrupt", _filePath);
            return null;
        }

        if (database is not null)
        {
            var totalNominations = database.Ceremonies.Sum(c => c.Categories.Sum(cat => cat.Nominations.Count));
            _logger.LogInformation(
                "Awards database loaded from {Path} (schema v{Version}, generated {Timestamp}, {Organizations} organizations, {Ceremonies} ceremonies, {Nominations} nominations)",
                _filePath,
                database.SchemaVersion,
                database.GeneratedUtc,
                database.Organizations.Count,
                database.Ceremonies.Count,
                totalNominations);
        }
        else
        {
            _logger.LogWarning("Awards database deserialized as null from {Path}", _filePath);
        }

        return database;
    }

    /// <inheritdoc />
    public bool Exists() => File.Exists(_filePath);
}
