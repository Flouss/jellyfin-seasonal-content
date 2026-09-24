using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// Real HTTP implementation of <see cref="IMdbListApiClient"/>. Endpoint verified live
/// (docs/decisions.md): <c>GET https://api.mdblist.com/lists/{username}/{slug}/items?apikey=...&amp;limit=&amp;offset=</c>.
/// </summary>
public sealed class MdbListApiClient : IMdbListApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MdbListApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Factory used to create the underlying <see cref="HttpClient"/>.</param>
    public MdbListApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task<MdbListResponse> GetItemsPageAsync(string username, string slug, string apiKey, int limit, int offset, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(MdbListApiClient));

        var url = string.Format(
            CultureInfo.InvariantCulture,
            "https://api.mdblist.com/lists/{0}/{1}/items?apikey={2}&limit={3}&offset={4}",
            Uri.EscapeDataString(username),
            Uri.EscapeDataString(slug),
            Uri.EscapeDataString(apiKey),
            limit,
            offset);

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Never include the URL here - it contains the API key.
            throw new HttpRequestException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "MDBList returned HTTP {0} for list '{1}/{2}'.",
                    (int)response.StatusCode,
                    username,
                    slug));
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<MdbListResponse>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? new MdbListResponse();
    }
}
