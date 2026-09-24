namespace Jellyfin.Plugin.SeasonalContent.Jellyseerr;

/// <summary>
/// What the playback interceptor should do about a title, based on its current Jellyseerr status.
/// </summary>
public enum RequestAction
{
    /// <summary>Not yet requested (or was deleted) - create a new request.</summary>
    CreateRequest,

    /// <summary>Already requested and awaiting approval or processing - don't duplicate it.</summary>
    AlreadyPending,

    /// <summary>Already available (fully or partially) - don't request it.</summary>
    AlreadyAvailable
}

/// <summary>
/// Pure decision: given a title's current Jellyseerr media status (or none at all, if Jellyseerr
/// has never seen it), decide whether to create a request. Per docs/implementation-plan.md §3.6:
/// "Deduplicate before requesting: check the title's media/request status in Jellyseerr first."
/// </summary>
public static class RequestDecision
{
    /// <summary>
    /// Decides the action for one title.
    /// </summary>
    /// <param name="currentStatus">The title's current <c>MediaInfo.status</c> from Jellyseerr, or
    /// <see langword="null"/> if Jellyseerr returned no <c>mediaInfo</c> at all (never seen this title).</param>
    /// <returns>The action to take.</returns>
    public static RequestAction Decide(JellyseerrMediaStatus? currentStatus) => currentStatus switch
    {
        null or JellyseerrMediaStatus.Unknown or JellyseerrMediaStatus.Deleted => RequestAction.CreateRequest,
        JellyseerrMediaStatus.Pending or JellyseerrMediaStatus.Processing => RequestAction.AlreadyPending,
        JellyseerrMediaStatus.PartiallyAvailable or JellyseerrMediaStatus.Available => RequestAction.AlreadyAvailable,
        _ => RequestAction.CreateRequest
    };
}
