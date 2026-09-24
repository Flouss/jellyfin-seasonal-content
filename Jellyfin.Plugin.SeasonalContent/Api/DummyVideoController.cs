using System;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SeasonalContent.Api;

/// <summary>
/// Serves the single placeholder video that every stub `.strm` points at.
/// </summary>
/// <remarks>
/// Security invariant: this controller takes NO parameters (no path, no id, no query) and must
/// never gain any. It is deliberately anonymous — Jellyfin's own server-side ffmpeg process and
/// direct-play clients fetch it with no auth token — so any parameter added here would be an
/// unauthenticated input straight into request handling. If this endpoint ever needs to vary its
/// response, add a new plugin config value instead of a parameter, or use a different route.
/// </remarks>
[Route("SeasonalContent")]
public class DummyVideoController : ControllerBase
{
    private const string ResourceName = "Jellyfin.Plugin.SeasonalContent.Resources.dummy.mp4";

    /// <summary>
    /// Streams the embedded placeholder video, with HTTP range support so both direct-play
    /// clients and the server's own ffmpeg transcoder can seek it.
    /// </summary>
    /// <returns>The video file.</returns>
    [HttpGet("Dummy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetDummyVideo()
    {
        var assembly = GetType().Assembly;
        var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "Embedded resource '{0}' not found.", ResourceName));

        return File(stream, "video/mp4", enableRangeProcessing: true);
    }
}
