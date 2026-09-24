using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.RequestProfiles;

/// <summary>
/// The result of <see cref="IRequestProfileResolver.ResolveAsync"/>. <see cref="Success"/> is
/// <see langword="false"/> only when a live Jellyseerr call itself failed (network error,
/// non-success HTTP status) - never for an individual stale profile id, which is skipped and logged
/// instead (a renamed/deleted Radarr profile must not take down every other configured version).
/// </summary>
/// <param name="Success">Whether resolution succeeded well enough to use the result.</param>
/// <param name="Profiles">The resolved, labeled profiles - empty when <paramref name="Success"/> is
/// <see langword="false"/>, or when the caller passed no profiles to resolve.</param>
/// <param name="ErrorMessage">Set when <see cref="Success"/> is <see langword="false"/>.</param>
public sealed record RequestProfileResolveResult(bool Success, IReadOnlyList<ResolvedRequestProfile> Profiles, string? ErrorMessage);

/// <summary>
/// Resolves a list of admin-configured <see cref="RequestProfile"/> entries (bare server/profile id
/// pairs) into labeled, disambiguated versions, by fetching each entry's current display name live
/// from Jellyseerr. See <see cref="RequestProfileLabeler"/> for the pure labeling step.
/// </summary>
public interface IRequestProfileResolver
{
    /// <summary>
    /// Resolves one kind's profiles.
    /// </summary>
    /// <param name="kind">Whether <paramref name="profiles"/> are Radarr (movie) or Sonarr (TV) profiles.</param>
    /// <param name="profiles">The profiles to resolve, in the order they should be listed - callers
    /// that want a "default" version first must put it first in this list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolve result.</returns>
    Task<RequestProfileResolveResult> ResolveAsync(MediaKind kind, IReadOnlyList<RequestProfile> profiles, CancellationToken cancellationToken);
}
