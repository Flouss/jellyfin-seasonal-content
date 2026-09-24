namespace Jellyfin.Plugin.SeasonalContent.Jellyseerr;

/// <summary>
/// Mirrors Jellyseerr's <c>MediaInfo.status</c> numeric values exactly (verified against the real
/// published OpenAPI spec, <c>Fallenbagel/jellyseerr</c> - see docs/decisions.md, "M5 API
/// surface"). The numeric values matter: they're deserialized directly from Jellyseerr's JSON.
/// </summary>
public enum JellyseerrMediaStatus
{
    /// <summary>Not requested and not in Radarr/Sonarr.</summary>
    Unknown = 1,

    /// <summary>Requested, not yet approved or processing.</summary>
    Pending = 2,

    /// <summary>Approved and being processed (e.g. downloading).</summary>
    Processing = 3,

    /// <summary>Some but not all of the media is available (TV only in practice).</summary>
    PartiallyAvailable = 4,

    /// <summary>Fully available.</summary>
    Available = 5,

    /// <summary>Previously available, since removed.</summary>
    Deleted = 6
}
