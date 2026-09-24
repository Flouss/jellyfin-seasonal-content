using System;
using System.Collections.Concurrent;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.Playback;

/// <summary>
/// Suppresses a repeat request for the same (user, kind, TMDb id) within a short window, per
/// docs/implementation-plan.md §3.5: "Rate limit per (user, TMDb id) with a short in-memory
/// window, so repeated taps or retries don't send duplicate requests." Deliberately in-memory
/// only - a server restart resetting the window is an acceptable tradeoff for a plugin, not a
/// correctness issue (Jellyseerr's own dedup, <see cref="Jellyseerr.RequestDecision"/>, is what
/// actually prevents duplicate requests long-term). Keyed on <see cref="MediaKind"/> as well as
/// TMDb id since a movie and a show can share a numeric TMDb id (docs/rename-tv-globalkey-plan.md).
/// </summary>
public sealed class PlaybackRateLimiter
{
    private readonly IClock _clock;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<(Guid UserId, MediaKind Kind, int TmdbId), DateTimeOffset> _lastAllowedAt = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackRateLimiter"/> class.
    /// </summary>
    /// <param name="clock">Instance of the <see cref="IClock"/> interface.</param>
    /// <param name="window">How long a (user, TMDb id) pair stays suppressed after being allowed.</param>
    public PlaybackRateLimiter(IClock clock, TimeSpan window)
    {
        _clock = clock;
        _window = window;
    }

    /// <summary>
    /// Checks whether a request for this (user, kind, TMDb id) triple should proceed right now,
    /// and - if so - starts a new suppression window for it.
    /// </summary>
    /// <param name="userId">The playing user's id.</param>
    /// <param name="kind">Whether the title is a movie or a TV series.</param>
    /// <param name="tmdbId">The title's TMDb id.</param>
    /// <returns><see langword="true"/> if this call should proceed.</returns>
    public bool ShouldAllow(Guid userId, MediaKind kind, int tmdbId)
    {
        var now = _clock.UtcNow;
        var key = (userId, kind, tmdbId);

        if (_lastAllowedAt.TryGetValue(key, out var last) && now - last < _window)
        {
            return false;
        }

        _lastAllowedAt[key] = now;
        return true;
    }
}
