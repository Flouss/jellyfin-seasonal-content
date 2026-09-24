using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SeasonalContent.Jellyseerr;

/// <summary>
/// The result of <see cref="IJellyseerrClient.TestConnectionAsync"/>.
/// </summary>
/// <param name="Success">Whether the configured URL and API key both work.</param>
/// <param name="Message">A human-readable outcome description.</param>
public sealed record JellyseerrConnectionTestResult(bool Success, string Message);

/// <summary>
/// The result of <see cref="IJellyseerrClient.GetMovieStatusAsync"/>. <see cref="Success"/> is
/// <see langword="false"/> only when the check itself failed (network error, non-success HTTP
/// status) - never conflated with "no media info" (which is <see cref="Success"/> = true,
/// <see cref="Status"/> = null), so a caller can never mistake a failed dedup check for "safe to
/// request" (docs/implementation-plan.md §3.6's dedup requirement would be defeated by that).
/// </summary>
/// <param name="Success">Whether the check itself succeeded.</param>
/// <param name="Status">The title's current status, or <see langword="null"/> if Jellyseerr has never seen it.</param>
/// <param name="ErrorMessage">Set when <see cref="Success"/> is <see langword="false"/>.</param>
public sealed record JellyseerrMovieStatusResult(bool Success, JellyseerrMediaStatus? Status, string? ErrorMessage);

/// <summary>
/// The result of <see cref="IJellyseerrClient.FindUserByJellyfinIdAsync"/>.
/// </summary>
/// <param name="Found">Whether a Jellyseerr user is linked to this Jellyfin user id.</param>
/// <param name="JellyseerrUserId">The Jellyseerr numeric user id, when <see cref="Found"/> is <see langword="true"/>.</param>
/// <param name="ErrorMessage">Set when the lookup itself failed (distinct from a clean 404).</param>
public sealed record JellyseerrUserLookupResult(bool Found, int? JellyseerrUserId, string? ErrorMessage);

/// <summary>
/// The result of <see cref="IJellyseerrClient.CreateRequestAsync"/>.
/// </summary>
/// <param name="Success">Whether the request was created.</param>
/// <param name="Status">The created request's own status, when <see cref="Success"/> is <see langword="true"/>.</param>
/// <param name="ErrorMessage">Set when <see cref="Success"/> is <see langword="false"/>.</param>
public sealed record JellyseerrCreateRequestResult(bool Success, JellyseerrRequestStatus? Status, string? ErrorMessage);

/// <summary>
/// Thin client over Jellyseerr's public API (docs/decisions.md "M5 API surface" - shapes verified
/// against the real published OpenAPI spec, not guessed). A thin IO shell, verified live
/// (docs/implementation-plan.md §8), not unit tested.
/// </summary>
public interface IJellyseerrClient
{
    /// <summary>
    /// Tests that the configured URL and API key both work, via <c>GET /auth/me</c> - not
    /// <c>GET /status</c> (which the plan and POC assumed): that endpoint requires no
    /// authentication at all, so it can't actually prove the key is valid (docs/decisions.md).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The test result.</returns>
    Task<JellyseerrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a movie's current Jellyseerr status, for the dedup check (§3.6).
    /// </summary>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The status result.</returns>
    Task<JellyseerrMovieStatusResult> GetMovieStatusAsync(int tmdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the Jellyseerr user linked to a Jellyfin user id, via <c>GET /user/jellyfin/{id}</c>.
    /// Never auto-imports (policy (ii), §3.6's default) - a not-found result means the caller
    /// should tell the user to ask an admin to import them.
    /// </summary>
    /// <param name="jellyfinUserId">The Jellyfin user's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lookup result.</returns>
    Task<JellyseerrUserLookupResult> FindUserByJellyfinIdAsync(Guid jellyfinUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a movie request attributed to the given Jellyseerr user, per Option A+
    /// (docs/implementation-plan.md §5): fire-and-forget, left for an approver to adjust and
    /// approve. Optionally targets a configured Radarr server/profile.
    /// </summary>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <param name="jellyseerrUserId">The requesting Jellyseerr user's numeric id.</param>
    /// <param name="radarrServerId">The configured Radarr server id, if any.</param>
    /// <param name="radarrProfileId">The configured Radarr quality profile id, if any.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The create result.</returns>
    Task<JellyseerrCreateRequestResult> CreateRequestAsync(int tmdbId, int jellyseerrUserId, int? radarrServerId, int? radarrProfileId, CancellationToken cancellationToken);
}
