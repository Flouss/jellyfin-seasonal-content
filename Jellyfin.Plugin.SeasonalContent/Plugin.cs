using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent;

/// <summary>
/// The main plugin entry point.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        var migration = ConfigMigration.MigrateMdbListApiKey(Configuration);
        if (migration.ListsDisagreed)
        {
            logger.LogWarning(
                "Multiple lists had different MDBList API keys before this upgrade - one of them was kept as the new global key (Dashboard -> Plugins -> {Name}); double-check it's the right one.",
                Name);
        }

        if (migration.Changed)
        {
            SaveConfiguration();
        }
    }

    /// <inheritdoc />
    public override string Name => "Smarter Collections";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("3ca6023b-866a-4acb-a1e9-d30c07b4f3aa");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = "Smarter Collections Settings",
                EnableInMainMenu = true,
                MenuIcon = "collections",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }
}
