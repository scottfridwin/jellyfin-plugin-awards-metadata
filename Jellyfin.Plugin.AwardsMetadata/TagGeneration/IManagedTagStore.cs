namespace Jellyfin.Plugin.AwardsMetadata.TagGeneration;

/// <summary>
/// Tracks which tags are managed by this plugin so they can be safely updated or removed.
/// </summary>
public interface IManagedTagStore
{
    /// <summary>
    /// Gets all managed tags for a specific Jellyfin item.
    /// </summary>
    /// <param name="itemId">The Jellyfin item ID.</param>
    /// <returns>The set of tags managed by this plugin for the item.</returns>
    IReadOnlySet<string> GetManagedTags(Guid itemId);

    /// <summary>
    /// Sets the managed tags for a specific Jellyfin item, replacing any previous set.
    /// </summary>
    /// <param name="itemId">The Jellyfin item ID.</param>
    /// <param name="tags">The new set of managed tags.</param>
    void SetManagedTags(Guid itemId, IEnumerable<string> tags);

    /// <summary>
    /// Removes all managed tag records for a specific item.
    /// </summary>
    /// <param name="itemId">The Jellyfin item ID.</param>
    void RemoveManagedTags(Guid itemId);

    /// <summary>
    /// Gets all item IDs that have managed tags.
    /// </summary>
    /// <returns>All item IDs with managed tags.</returns>
    IReadOnlyCollection<Guid> GetAllTrackedItemIds();

    /// <summary>
    /// Persists the managed tag store to disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the managed tag store from disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
