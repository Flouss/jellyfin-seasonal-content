using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Jellyseerr;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SeasonalContent.Api;

/// <summary>
/// Admin-only endpoints backing the config page's Jellyseerr section: connection test and the
/// Radarr server/profile dropdowns (docs/implementation-plan.md §3.6, §4).
/// </summary>
[Route("SeasonalContent")]
[Authorize(Policy = Policies.RequiresElevation)]
public class JellyseerrController : ControllerBase
{
    private readonly IJellyseerrClient _jellyseerrClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyseerrController"/> class.
    /// </summary>
    /// <param name="jellyseerrClient">Instance of the <see cref="IJellyseerrClient"/> interface.</param>
    public JellyseerrController(IJellyseerrClient jellyseerrClient)
    {
        _jellyseerrClient = jellyseerrClient;
    }

    /// <summary>
    /// Tests the configured Jellyseerr URL and API key via <c>GET /auth/me</c> (not <c>/status</c>
    /// - see docs/decisions.md for why that wouldn't actually validate the key).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The test result.</returns>
    [HttpGet("Jellyseerr/TestConnection")]
    public async Task<ActionResult> TestConnection(CancellationToken cancellationToken)
    {
        var result = await _jellyseerrClient.TestConnectionAsync(cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Lists Jellyseerr's configured Radarr servers, for the config page's server dropdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The servers, or a 400 with an error message.</returns>
    [HttpGet("Jellyseerr/RadarrServers")]
    public async Task<ActionResult> GetRadarrServers(CancellationToken cancellationToken)
    {
        var result = await _jellyseerrClient.GetRadarrServersAsync(cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result.Servers) : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>
    /// Lists a Radarr server's quality profiles, for the config page's profile dropdown.
    /// </summary>
    /// <param name="radarrServerId">The Radarr server's Jellyseerr-assigned id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The profiles, or a 400 with an error message.</returns>
    [HttpGet("Jellyseerr/RadarrServers/{radarrServerId}/Profiles")]
    public async Task<ActionResult> GetRadarrProfiles([FromRoute] int radarrServerId, CancellationToken cancellationToken)
    {
        var result = await _jellyseerrClient.GetRadarrProfilesAsync(radarrServerId, cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result.Profiles) : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>
    /// Lists Jellyseerr's configured Sonarr servers, for the config page's server dropdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The servers, or a 400 with an error message.</returns>
    [HttpGet("Jellyseerr/SonarrServers")]
    public async Task<ActionResult> GetSonarrServers(CancellationToken cancellationToken)
    {
        var result = await _jellyseerrClient.GetSonarrServersAsync(cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result.Servers) : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>
    /// Lists a Sonarr server's quality profiles, for the config page's profile dropdown.
    /// </summary>
    /// <param name="sonarrServerId">The Sonarr server's Jellyseerr-assigned id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The profiles, or a 400 with an error message.</returns>
    [HttpGet("Jellyseerr/SonarrServers/{sonarrServerId}/Profiles")]
    public async Task<ActionResult> GetSonarrProfiles([FromRoute] int sonarrServerId, CancellationToken cancellationToken)
    {
        var result = await _jellyseerrClient.GetSonarrProfilesAsync(sonarrServerId, cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result.Profiles) : BadRequest(new { message = result.ErrorMessage });
    }
}
