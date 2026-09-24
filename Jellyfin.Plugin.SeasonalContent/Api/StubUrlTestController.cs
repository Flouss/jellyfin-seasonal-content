using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SeasonalContent.Api;

/// <summary>
/// Admin-only endpoint backing the config page's "Test stub URL" button.
/// </summary>
[Route("SeasonalContent")]
[Authorize(Policy = Policies.RequiresElevation)]
public class StubUrlTestController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubUrlTestController"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    public StubUrlTestController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Fetches the configured stub base URL's dummy video endpoint from the server side and
    /// reports whether it succeeded. This proves the server can reach the URL; it does NOT
    /// prove LAN client devices can (docs/implementation-plan.md §3.4).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A JSON result describing the outcome.</returns>
    [HttpGet("TestStubUrl")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> TestStubUrl(CancellationToken cancellationToken)
    {
        var baseUrl = Plugin.Instance?.Configuration.StubBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return BadRequest(new { success = false, message = "Stub base URL is not configured." });
        }

        Uri dummyUrl;
        try
        {
            dummyUrl = new Uri(new Uri(baseUrl, UriKind.Absolute), "SeasonalContent/Dummy");
        }
        catch (UriFormatException)
        {
            return BadRequest(new { success = false, message = "Stub base URL is not a valid absolute URL." });
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            using var response = await client
                .GetAsync(dummyUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return Ok(new
            {
                success = response.IsSuccessStatusCode,
                statusCode = (int)response.StatusCode,
                url = dummyUrl.ToString()
            });
        }
        catch (HttpRequestException ex)
        {
            return Ok(new { success = false, message = ex.Message, url = dummyUrl.ToString() });
        }
        catch (TaskCanceledException)
        {
            return Ok(new { success = false, message = "Request timed out.", url = dummyUrl.ToString() });
        }
    }
}
