using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Persistence;

/// <summary>
/// Abstraction for persisting and loading awards databases.
/// </summary>
public interface IAwardStore
{
    /// <summary>
    /// Saves the awards database to persistent storage.
    /// </summary>
    /// <param name="database">The database to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(AwardsDatabase database, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the awards database from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded database, or null if no database exists.</returns>
    Task<AwardsDatabase?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a persisted awards database exists.
    /// </summary>
    /// <returns>True if a database exists; otherwise false.</returns>
    bool Exists();
}
