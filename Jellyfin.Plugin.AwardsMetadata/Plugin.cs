using Jellyfin.Plugin.AwardsMetadata.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AwardsMetadata;

/// <summary>
/// The Awards Metadata plugin for Jellyfin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of <see cref="IApplicationPaths"/>.</param>
    /// <param name="xmlSerializer">Instance of <see cref="IXmlSerializer"/>.</param>
    /// <param name="logger">Logger instance.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;

        _logger.LogInformation("Awards Metadata plugin initialized. Version: {Version}, DataFolder: {DataFolder}", Version, DataFolderPath);
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Awards Metadata";

    /// <inheritdoc />
    public override Guid Id => new("38895a18-37d8-4a0a-92f8-c38335820627");

    /// <inheritdoc />
    public override string Description => "Applies custom award metadata tags to media items in Jellyfin.";

    /// <summary>
    /// Gets the path to the awards database file.
    /// </summary>
    /// <returns>The full path to the awards database JSON file.</returns>
    public string GetAwardsDatabasePath()
    {
        var storagePath = Configuration.AwardsStoragePath;
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            storagePath = Path.Combine(DataFolderPath, "awards-database.json");
            _logger.LogDebug("Using default awards database path: {Path}", storagePath);
        }
        else
        {
            // Validate the configured path is within the plugin data folder to prevent path traversal
            var resolvedPath = Path.GetFullPath(storagePath);
            var dataFolderFull = Path.GetFullPath(DataFolderPath + Path.DirectorySeparatorChar);

            if (!resolvedPath.StartsWith(dataFolderFull, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Configured AwardsStoragePath '{ConfiguredPath}' is outside the plugin data folder. Falling back to default",
                    storagePath);
                storagePath = Path.Combine(DataFolderPath, "awards-database.json");
            }
            else
            {
                storagePath = resolvedPath;
            }

            _logger.LogDebug("Using awards database path: {Path}", storagePath);
        }

        return storagePath;
    }

    /// <summary>
    /// Gets the path to the managed tags tracking file.
    /// </summary>
    /// <returns>The full path to the managed tags JSON file.</returns>
    public string GetManagedTagsPath()
    {
        var path = Path.Combine(DataFolderPath, "managed-tags.json");
        _logger.LogDebug("Managed tags path: {Path}", path);
        return path;
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
            },
        ];
    }
}
