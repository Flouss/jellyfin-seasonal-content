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
}
