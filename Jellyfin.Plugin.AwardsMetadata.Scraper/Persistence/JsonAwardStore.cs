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
            Directory.CreateDirectory(directory);
        }

        database.GeneratedUtc = DateTimeOffset.UtcNow;

        var json = JsonSerializer.Serialize(database, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Awards database saved to {Path} ({Organizations} organizations, {Ceremonies} ceremonies)",
            _filePath,
            database.Organizations.Count,
            database.Ceremonies.Count);
    }

    /// <inheritdoc />
    public async Task<AwardsDatabase?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogDebug("Awards database file not found at {Path}", _filePath);
            return null;
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
        var database = JsonSerializer.Deserialize<AwardsDatabase>(json, SerializerOptions);

        if (database is not null)
        {
            _logger.LogInformation(
                "Awards database loaded from {Path} (schema v{Version}, generated {Timestamp})",
                _filePath,
                database.SchemaVersion,
                database.GeneratedUtc);
        }

        return database;
    }

    /// <inheritdoc />
    public bool Exists() => File.Exists(_filePath);
}
