using System.Reflection;
using Jellyfin.Plugin.AwardsMetadata.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AwardsMetadata;

/// <summary>
/// The Awards Metadata plugin for Jellyfin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of <see cref="IApplicationPaths"/>.</param>
    /// <param name="xmlSerializer">Instance of <see cref="IXmlSerializer"/>.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
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
        }

        return storagePath;
    }

    /// <summary>
    /// Gets the path to the managed tags tracking file.
    /// </summary>
    /// <returns>The full path to the managed tags JSON file.</returns>
    public string GetManagedTagsPath()
    {
        return Path.Combine(DataFolderPath, "managed-tags.json");
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
