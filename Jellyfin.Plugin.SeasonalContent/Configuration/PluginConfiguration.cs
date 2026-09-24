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
    /// Gets or sets the MDBList API key, shared by every configured list. Never logged; round-trips
    /// through the standard plugin-configuration GET/POST like <see cref="JellyseerrApiKey"/> (the
    /// config page leaves the field blank to mean "keep the saved key" - see
    /// docs/rename-tv-globalkey-plan.md). Replaces the old per-list <see cref="SeasonalListConfig.ApiKey"/>
    /// field; <see cref="Configuration.ConfigMigration.MigrateMdbListApiKey"/> copies an old
    /// per-list key into this field once, on first load after upgrade.
    /// </summary>
    public string MdbListApiKey { get; set; } = string.Empty;

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

    /// <summary>
    /// Gets or sets the Jellyseerr base URL (e.g. <c>http://host:5055</c>), no trailing slash
    /// required. Empty by default.
    /// </summary>
    public string JellyseerrUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyseerr API key. Never logged (docs/implementation-plan.md §3.1's
    /// secrets rule applies here too, not just to MDBList's key).
    /// </summary>
    public string JellyseerrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Radarr server id to request against, or null to use Jellyseerr's own
    /// default.
    /// </summary>
    public int? JellyseerrRadarrServerId { get; set; }

    /// <summary>
    /// Gets or sets the Radarr quality profile id to request with, or null to use Jellyseerr's
    /// own default.
    /// </summary>
    public int? JellyseerrRadarrProfileId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a playing user with no Jellyseerr account should be
    /// auto-imported (§3.6 policy (i)) rather than told to ask an admin (policy (ii), the default
    /// - and the only one implemented in M5; this flag is reserved for a future opt-in toggle).
    /// </summary>
    public bool AutoImportJellyseerrUsers { get; set; }

    /// <summary>
    /// Gets or sets how many seconds the interceptor waits before stopping a stub's playback, so
    /// slower clients have time to actually start rendering before being stopped.
    /// </summary>
    public int PlaybackStopDelaySeconds { get; set; } = 2;
}
