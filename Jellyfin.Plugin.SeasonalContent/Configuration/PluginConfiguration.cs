using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SeasonalContent.Configuration;

/// <summary>
/// Plugin configuration. Fields are added milestone by milestone per
/// docs/implementation-plan.md §4.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the scheme://host:port that every stub `.strm` file points at. Must be
    /// reachable both by the server's own ffmpeg process and by LAN client devices. Empty by
    /// default: an empty value must fail loudly rather than silently write a dead URL into stubs
    /// (docs/implementation-plan.md §3.4).
    /// </summary>
    public string StubBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configured seasonal lists. Persisted here as a bare-bones stand-in for the
    /// full config-UI (list add/remove, paste-a-URL parsing) that ships in M6 - see
    /// docs/m4-plan.md.
    /// </summary>
    public List<SeasonalListConfig> Lists { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether disabling or deleting a list also removes its
    /// BoxSet on the next sync (docs/implementation-plan.md §3.7, §4).
    /// </summary>
    public bool RemoveCollectionWhenListDisabled { get; set; } = true;
}
