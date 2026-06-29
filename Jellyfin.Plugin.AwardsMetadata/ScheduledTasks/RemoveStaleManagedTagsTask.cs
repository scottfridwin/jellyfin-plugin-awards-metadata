using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AwardsMetadata.ScheduledTasks;

/// <summary>
/// Scheduled task that removes stale managed tags that are no longer applicable.
/// </summary>
public sealed class RemoveStaleManagedTagsTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<RemoveStaleManagedTagsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveStaleManagedTagsTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public RemoveStaleManagedTagsTask(ILibraryManager libraryManager, ILoggerFactory loggerFactory)
    {
        _libraryManager = libraryManager;
        _logger = loggerFactory.CreateLogger<RemoveStaleManagedTagsTask>();
    }

    /// <inheritdoc />
    public string Name => "Remove Stale Award Tags";

    /// <inheritdoc />
    public string Key => "AwardsMetadataRemoveStaleTags";

    /// <inheritdoc />
    public string Description => "Removes award metadata tags that are no longer managed by the plugin.";

    /// <inheritdoc />
    public string Category => "Awards Metadata";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [];
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            _logger.LogError("Plugin instance is not available");
            return;
        }

        var managedTagStore = new TagGeneration.JsonManagedTagStore(plugin.GetManagedTagsPath());
        await managedTagStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        var trackedItems = managedTagStore.GetAllTrackedItemIds();
        if (trackedItems.Count == 0)
        {
            _logger.LogInformation("No managed tags to remove");
            progress.Report(100);
            return;
        }

        _logger.LogInformation("Checking {Count} items for stale managed tags", trackedItems.Count);

        var itemsUpdated = 0;
        var tagsRemoved = 0;
        var processed = 0;

        foreach (var itemId in trackedItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            progress.Report((double)processed / trackedItems.Count * 100);

            var item = _libraryManager.GetItemById(itemId);
            if (item is null)
            {
                // Item no longer exists, clean up tracking
                managedTagStore.RemoveManagedTags(itemId);
                continue;
            }

            var managedTags = managedTagStore.GetManagedTags(itemId);
            if (managedTags.Count == 0)
            {
                continue;
            }

            var currentTags = new HashSet<string>(item.Tags, StringComparer.Ordinal);
            var modified = false;

            foreach (var managedTag in managedTags)
            {
                if (currentTags.Remove(managedTag))
                {
                    tagsRemoved++;
                    modified = true;
                }
            }

            if (modified)
            {
                item.Tags = [.. currentTags];
                await _libraryManager.UpdateItemAsync(item, item.GetParent()!, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                itemsUpdated++;
            }

            managedTagStore.RemoveManagedTags(itemId);
        }

        await managedTagStore.SaveAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Stale tag removal complete: {TagsRemoved} tags removed from {ItemsUpdated} items",
            tagsRemoved,
            itemsUpdated);

        progress.Report(100);
    }
}
