using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Jellyseerr;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SeasonalContent.Api;

/// <summary>
/// Admin-only endpoint backing a future config page's "Test connection" button for Jellyseerr
/// (docs/implementation-plan.md §3.6).
/// </summary>
[Route("SeasonalContent")]
[Authorize(Policy = Policies.RequiresElevation)]
public class JellyseerrTestConnectionController : ControllerBase
{
    private readonly IJellyseerrClient _jellyseerrClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyseerrTestConnectionController"/> class.
    /// </summary>
    /// <param name="jellyseerrClient">Instance of the <see cref="IJellyseerrClient"/> interface.</param>
    public JellyseerrTestConnectionController(IJellyseerrClient jellyseerrClient)
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
}
