using System.Text.Json;

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
    private Dictionary<Guid, HashSet<string>> _managedTags = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonManagedTagStore"/> class.
    /// </summary>
    /// <param name="filePath">Path to the managed tags JSON file.</param>
    public JsonManagedTagStore(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
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
            Directory.CreateDirectory(directory);
        }

        // Convert to serializable format
        var data = _managedTags.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value.ToList());

        var json = JsonSerializer.Serialize(data, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _managedTags = [];
            return;
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
        var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, SerializerOptions);

        _managedTags = data?.ToDictionary(
            kvp => Guid.Parse(kvp.Key),
            kvp => new HashSet<string>(kvp.Value, StringComparer.Ordinal)) ?? [];
    }
}
