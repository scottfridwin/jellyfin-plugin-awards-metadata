using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Models;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Normalization;
using Jellyfin.Plugin.AwardsMetadata.Scraper.Persistence;

namespace Jellyfin.Plugin.AwardsMetadata.ScheduledTasks;

/// <summary>
/// Scheduled task that applies award metadata tags to Jellyfin library items.
/// </summary>
public sealed class ApplyTagsTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ApplyTagsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplyTagsTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public ApplyTagsTask(ILibraryManager libraryManager, ILoggerFactory loggerFactory)
    {
        _libraryManager = libraryManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ApplyTagsTask>();
    }

    /// <inheritdoc />
    public string Name => "Apply Award Tags";

    /// <inheritdoc />
    public string Key => "AwardsMetadataApplyTags";

    /// <inheritdoc />
    public string Description => "Applies award metadata tags to movies in the library based on scraped data.";

    /// <inheritdoc />
    public string Category => "Awards Metadata";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(4).Ticks,
            },
        ];
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

        var config = plugin.Configuration;
        var normalizer = new SlugTextNormalizer();
        var tagGenerator = new TagGeneration.TagGenerator(config.TagFormat, normalizer);

        // Load awards database
        var store = new JsonAwardStore(
            plugin.GetAwardsDatabasePath(),
            _loggerFactory.CreateLogger<JsonAwardStore>());

        var database = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (database is null)
        {
            _logger.LogWarning("No awards database found. Run the scrape task first.");
            return;
        }

        // Load managed tag store
        var managedTagStore = new TagGeneration.JsonManagedTagStore(plugin.GetManagedTagsPath());
        await managedTagStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        // Build a lookup of TMDB movie ID -> nominations
        var movieNominations = BuildMovieNominationLookup(database, config);

        if (movieNominations.Count == 0)
        {
            _logger.LogInformation("No applicable nominations found in the awards database");
            progress.Report(100);
            return;
        }

        // Get all movies from the library
        var movies = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie],
            Recursive = true,
        });

        _logger.LogInformation("Processing {Count} movies from library", movies.Count);

        var tagsApplied = 0;
        var moviesUpdated = 0;

        for (var i = 0; i < movies.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report((double)i / movies.Count * 100);

            var movie = movies[i];
            var tmdbId = GetTmdbId(movie);
            if (tmdbId is null)
            {
                continue;
            }

            if (!movieNominations.TryGetValue(tmdbId.Value, out var nominations))
            {
                continue;
            }

            // Generate tags for this movie
            var newTags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (ceremony, category, nomination) in nominations)
            {
                var tag = tagGenerator.GenerateTag(
                    ceremony.OrganizationName,
                    nomination.Result.ToString(),
                    category.Name,
                    ceremony.Year);
                newTags.Add(tag);
            }

            // Get previously managed tags for this item
            var previousManagedTags = managedTagStore.GetManagedTags(movie.Id);
            var currentTags = new HashSet<string>(movie.Tags, StringComparer.Ordinal);

            // Remove previously managed tags that are no longer applicable
            var tagsToRemove = previousManagedTags.Except(newTags);
            foreach (var tagToRemove in tagsToRemove)
            {
                currentTags.Remove(tagToRemove);
            }

            // Add new managed tags (without duplicating user tags)
            var tagsAdded = false;
            foreach (var newTag in newTags)
            {
                if (currentTags.Add(newTag))
                {
                    tagsAdded = true;
                    tagsApplied++;
                }
            }

            // Update managed tag tracking
            managedTagStore.SetManagedTags(movie.Id, newTags);

            // Save changes to the movie if modified
            if (tagsAdded || previousManagedTags.Except(newTags).Any())
            {
                movie.Tags = [.. currentTags];
                await _libraryManager.UpdateItemAsync(movie, movie.GetParent()!, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                moviesUpdated++;
            }
        }

        // Persist managed tags
        await managedTagStore.SaveAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Tag application complete: {TagsApplied} tags applied to {MoviesUpdated} movies",
            tagsApplied,
            moviesUpdated);

        progress.Report(100);
    }

    private static int? GetTmdbId(BaseItem item)
    {
        if (item.TryGetProviderId("Tmdb", out var tmdbIdStr)
            && int.TryParse(tmdbIdStr, out var tmdbId))
        {
            return tmdbId;
        }

        return null;
    }

    private static Dictionary<int, List<(AwardCeremony Ceremony, AwardCategory Category, AwardNomination Nomination)>> BuildMovieNominationLookup(
        AwardsDatabase database,
        Configuration.PluginConfiguration config)
    {
        var lookup = new Dictionary<int, List<(AwardCeremony, AwardCategory, AwardNomination)>>();

        foreach (var ceremony in database.Ceremonies)
        {
            // Only process enabled organizations
            if (config.EnabledOrganizations.Count > 0 &&
                !config.EnabledOrganizations.Contains(ceremony.OrganizationSlug, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var category in ceremony.Categories)
            {
                foreach (var nomination in category.Nominations)
                {
                    // Filter by winner/nominee config
                    if (nomination.Result == AwardResult.Winner && !config.EnableWinnerTagging)
                    {
                        continue;
                    }

                    if (nomination.Result == AwardResult.Nominee && !config.EnableNomineeTagging)
                    {
                        continue;
                    }

                    if (nomination.TmdbMovieId is null)
                    {
                        continue;
                    }

                    var movieId = nomination.TmdbMovieId.Value;
                    if (!lookup.TryGetValue(movieId, out var list))
                    {
                        list = [];
                        lookup[movieId] = list;
                    }

                    list.Add((ceremony, category, nomination));
                }
            }
        }

        return lookup;
    }
}
