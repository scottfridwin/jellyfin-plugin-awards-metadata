using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Serialization;

/// <summary>
/// Provides JSON serialization and deserialization helpers for awards data.
/// </summary>
public static class AwardsJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Serializes an awards database to a JSON string.
    /// </summary>
    /// <param name="database">The database to serialize.</param>
    /// <returns>A JSON string representation.</returns>
    public static string Serialize(AwardsDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return JsonSerializer.Serialize(database, Options);
    }

    /// <summary>
    /// Deserializes a JSON string into an awards database.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized awards database.</returns>
    public static AwardsDatabase? Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        return JsonSerializer.Deserialize<AwardsDatabase>(json, Options);
    }

    /// <summary>
    /// Gets the shared serializer options for consistent serialization behavior.
    /// </summary>
    /// <returns>The JSON serializer options.</returns>
    public static JsonSerializerOptions GetOptions() => Options;
}
