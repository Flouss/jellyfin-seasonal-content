using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.Jellyseerr;

/// <summary>
/// The result of <see cref="IJellyseerrClient.TestConnectionAsync"/>.
/// </summary>
/// <param name="Success">Whether the configured URL and API key both work.</param>
/// <param name="Message">A human-readable outcome description.</param>
public sealed record JellyseerrConnectionTestResult(bool Success, string Message);

/// <summary>
/// The result of <see cref="IJellyseerrClient.GetMovieStatusAsync"/>/<see cref="IJellyseerrClient.GetTvStatusAsync"/>.
/// <see cref="Success"/> is <see langword="false"/> only when the check itself failed (network
/// error, non-success HTTP status) - never conflated with "no media info" (which is
/// <see cref="Success"/> = true, <see cref="Status"/> = null), so a caller can never mistake a
/// failed dedup check for "safe to request" (docs/implementation-plan.md §3.6's dedup requirement
/// would be defeated by that).
/// </summary>
/// <param name="Success">Whether the check itself succeeded.</param>
/// <param name="Status">The title's current status, or <see langword="null"/> if Jellyseerr has never seen it.</param>
/// <param name="ErrorMessage">Set when <see cref="Success"/> is <see langword="false"/>.</param>
public sealed record JellyseerrMediaStatusResult(bool Success, JellyseerrMediaStatus? Status, string? ErrorMessage);

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
/// One configured Radarr server, for the config page's server dropdown. Deliberately excludes the
/// server's own Radarr API key - confirmed live (docs/decisions.md) that Jellyseerr's
/// <c>GET /service/radarr</c> never returns it despite the shared schema listing an
/// <c>apiKey</c> property.
/// </summary>
/// <param name="Id">The Radarr server's Jellyseerr-assigned id.</param>
/// <param name="Name">The display name.</param>
/// <param name="IsDefault">Whether this is Jellyseerr's default server.</param>
public sealed record JellyseerrRadarrServer(int Id, string Name, bool IsDefault);

/// <summary>
/// One quality profile on a configured Radarr server.
/// </summary>
/// <param name="Id">The profile's id.</param>
/// <param name="Name">The display name.</param>
public sealed record JellyseerrRadarrProfile(int Id, string Name);

/// <summary>
/// The result of <see cref="IJellyseerrClient.GetRadarrServersAsync"/> and
/// <see cref="IJellyseerrClient.GetRadarrProfilesAsync"/>.
/// </summary>
/// <param name="Success">Whether the call succeeded.</param>
/// <param name="Servers">The Radarr servers, when listing servers.</param>
/// <param name="Profiles">The server's quality profiles, when listing profiles.</param>
/// <param name="ErrorMessage">Set when <see cref="Success"/> is <see langword="false"/>.</param>
public sealed record JellyseerrRadarrLookupResult(
    bool Success,
    IReadOnlyList<JellyseerrRadarrServer>? Servers,
    IReadOnlyList<JellyseerrRadarrProfile>? Profiles,
    string? ErrorMessage);

/// <summary>
/// One configured Sonarr server, for the config page's server dropdown. Same shape as
/// <see cref="JellyseerrRadarrServer"/>, kept as a separate type since Sonarr and Radarr servers
/// have independent id spaces in Jellyseerr - never interchangeable (docs/rename-tv-globalkey-plan.md).
/// </summary>
/// <param name="Id">The Sonarr server's Jellyseerr-assigned id.</param>
/// <param name="Name">The display name.</param>
/// <param name="IsDefault">Whether this is Jellyseerr's default server.</param>
public sealed record JellyseerrSonarrServer(int Id, string Name, bool IsDefault);

/// <summary>
/// One quality profile on a configured Sonarr server.
/// </summary>
/// <param name="Id">The profile's id.</param>
/// <param name="Name">The display name.</param>
public sealed record JellyseerrSonarrProfile(int Id, string Name);

/// <summary>
/// The result of <see cref="IJellyseerrClient.GetSonarrServersAsync"/> and
/// <see cref="IJellyseerrClient.GetSonarrProfilesAsync"/>.
/// </summary>
/// <param name="Success">Whether the call succeeded.</param>
/// <param name="Servers">The Sonarr servers, when listing servers.</param>
/// <param name="Profiles">The server's quality profiles, when listing profiles.</param>
/// <param name="ErrorMessage">Set when <see cref="Success"/> is <see langword="false"/>.</param>
public sealed record JellyseerrSonarrLookupResult(
    bool Success,
    IReadOnlyList<JellyseerrSonarrServer>? Servers,
    IReadOnlyList<JellyseerrSonarrProfile>? Profiles,
    string? ErrorMessage);

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
    Task<JellyseerrMediaStatusResult> GetMovieStatusAsync(int tmdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a TV series' current Jellyseerr status, for the dedup check (§3.6) - the TV
    /// counterpart of <see cref="GetMovieStatusAsync"/> (<c>GET /tv/{tmdbId}</c>).
    /// </summary>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The status result.</returns>
    Task<JellyseerrMediaStatusResult> GetTvStatusAsync(int tmdbId, CancellationToken cancellationToken);

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
    /// Creates a request attributed to the given Jellyseerr user, per Option A+
    /// (docs/implementation-plan.md §5): fire-and-forget, left for an approver to adjust and
    /// approve. Optionally targets a configured Radarr (movie) or Sonarr (TV) server/profile - the
    /// two id spaces are independent, so the caller must pass the one matching <paramref name="kind"/>,
    /// never the other (docs/rename-tv-globalkey-plan.md). A TV request always requests every
    /// season (<c>seasons: "all"</c>) - no per-season granularity in v1.
    /// </summary>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <param name="kind">Whether this is a movie or a TV series request.</param>
    /// <param name="jellyseerrUserId">The requesting Jellyseerr user's numeric id.</param>
    /// <param name="serverId">The configured Radarr/Sonarr server id (matching <paramref name="kind"/>), if any.</param>
    /// <param name="profileId">The configured Radarr/Sonarr quality profile id (matching <paramref name="kind"/>), if any.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The create result.</returns>
    Task<JellyseerrCreateRequestResult> CreateRequestAsync(int tmdbId, MediaKind kind, int jellyseerrUserId, int? serverId, int? profileId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists Jellyseerr's configured Radarr servers, for the config page's server dropdown
    /// (docs/implementation-plan.md §4).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lookup result, with <see cref="JellyseerrRadarrLookupResult.Servers"/> set.</returns>
    Task<JellyseerrRadarrLookupResult> GetRadarrServersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists a Radarr server's quality profiles, for the config page's profile dropdown.
    /// </summary>
    /// <param name="radarrServerId">The Radarr server's Jellyseerr-assigned id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lookup result, with <see cref="JellyseerrRadarrLookupResult.Profiles"/> set.</returns>
    Task<JellyseerrRadarrLookupResult> GetRadarrProfilesAsync(int radarrServerId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists Jellyseerr's configured Sonarr servers, for the config page's server dropdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lookup result, with <see cref="JellyseerrSonarrLookupResult.Servers"/> set.</returns>
    Task<JellyseerrSonarrLookupResult> GetSonarrServersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists a Sonarr server's quality profiles, for the config page's profile dropdown.
    /// </summary>
    /// <param name="sonarrServerId">The Sonarr server's Jellyseerr-assigned id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lookup result, with <see cref="JellyseerrSonarrLookupResult.Profiles"/> set.</returns>
    Task<JellyseerrSonarrLookupResult> GetSonarrProfilesAsync(int sonarrServerId, CancellationToken cancellationToken);
}
