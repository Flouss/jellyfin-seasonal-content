using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.Jellyseerr;

/// <inheritdoc />
public sealed class JellyseerrClient : IJellyseerrClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // Jellyseerr's schema validation rejects an explicit JSON null for a field typed as
        // `number` (not `number|null`) - e.g. serverId/profileId when neither is configured -
        // confirmed live 2026-09-24 (400 "request/body/serverId must be number"). The field must
        // be omitted entirely, not sent as null.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JellyseerrClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyseerrClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public JellyseerrClient(IHttpClientFactory httpClientFactory, ILogger<JellyseerrClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JellyseerrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient(out var baseUrl);
            if (client is null)
            {
                return new JellyseerrConnectionTestResult(false, "JellyseerrUrl or JellyseerrApiKey is not configured.");
            }

            using var response = await client.GetAsync(BuildUrl(baseUrl, "auth/me"), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? new JellyseerrConnectionTestResult(true, "Connected.")
                : new JellyseerrConnectionTestResult(false, string.Format(CultureInfo.InvariantCulture, "HTTP {0}.", (int)response.StatusCode));
        }
        catch (HttpRequestException ex)
        {
            return new JellyseerrConnectionTestResult(false, ex.Message);
        }
        catch (TaskCanceledException)
        {
            return new JellyseerrConnectionTestResult(false, "Request timed out.");
        }
    }

    /// <inheritdoc />
    public Task<JellyseerrMediaStatusResult> GetMovieStatusAsync(int tmdbId, CancellationToken cancellationToken) =>
        GetMediaStatusAsync("movie/" + tmdbId.ToString(CultureInfo.InvariantCulture), cancellationToken);

    /// <inheritdoc />
    public Task<JellyseerrMediaStatusResult> GetTvStatusAsync(int tmdbId, CancellationToken cancellationToken) =>
        GetMediaStatusAsync("tv/" + tmdbId.ToString(CultureInfo.InvariantCulture), cancellationToken);

    private async Task<JellyseerrMediaStatusResult> GetMediaStatusAsync(string relativePath, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient(out var baseUrl);
            if (client is null)
            {
                return new JellyseerrMediaStatusResult(false, null, "Jellyseerr is not configured.");
            }

            using var response = await client.GetAsync(BuildUrl(baseUrl, relativePath), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new JellyseerrMediaStatusResult(false, null, string.Format(CultureInfo.InvariantCulture, "HTTP {0}.", (int)response.StatusCode));
            }

            var dto = await response.Content.ReadFromJsonAsync<MediaDetailsJson>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var status = dto?.MediaInfo is { } mediaInfo ? (JellyseerrMediaStatus)mediaInfo.Status : (JellyseerrMediaStatus?)null;
            return new JellyseerrMediaStatusResult(true, status, null);
        }
        catch (HttpRequestException ex)
        {
            return new JellyseerrMediaStatusResult(false, null, ex.Message);
        }
        catch (JsonException ex)
        {
            return new JellyseerrMediaStatusResult(false, null, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<JellyseerrUserLookupResult> FindUserByJellyfinIdAsync(Guid jellyfinUserId, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient(out var baseUrl);
            if (client is null)
            {
                return new JellyseerrUserLookupResult(false, null, "Jellyseerr is not configured.");
            }

            using var response = await client.GetAsync(BuildUrl(baseUrl, "user/jellyfin/" + jellyfinUserId.ToString("N")), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new JellyseerrUserLookupResult(false, null, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new JellyseerrUserLookupResult(false, null, string.Format(CultureInfo.InvariantCulture, "HTTP {0}.", (int)response.StatusCode));
            }

            var dto = await response.Content.ReadFromJsonAsync<JellyseerrUserJson>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return dto is null
                ? new JellyseerrUserLookupResult(false, null, "Empty response body.")
                : new JellyseerrUserLookupResult(true, dto.Id, null);
        }
        catch (HttpRequestException ex)
        {
            return new JellyseerrUserLookupResult(false, null, ex.Message);
        }
        catch (JsonException ex)
        {
            return new JellyseerrUserLookupResult(false, null, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<JellyseerrCreateRequestResult> CreateRequestAsync(int tmdbId, MediaKind kind, int jellyseerrUserId, int? serverId, int? profileId, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient(out var baseUrl);
            if (client is null)
            {
                return new JellyseerrCreateRequestResult(false, null, "Jellyseerr is not configured.");
            }

            // A TV request always requests every season ("seasons: 'all'", read directly from
            // Jellyseerr's own MediaRequest.ts - see docs/rename-tv-globalkey-plan.md); the field
            // must be entirely absent for a movie request, not null (same JsonIgnoreCondition rule
            // that already applies to ServerId/ProfileId below).
            var body = new CreateRequestJson(
                MediaType: kind == MediaKind.Series ? "tv" : "movie",
                MediaId: tmdbId,
                ServerId: serverId,
                ProfileId: profileId,
                Seasons: kind == MediaKind.Series ? "all" : null);

            // X-Api-User, not a "userId" body field, is what actually makes this request subject
            // to the target user's own permissions. Verified live + against Jellyseerr's own
            // source (2026-09-24, docs/decisions.md): an X-Api-Key-only call authenticates as the
            // key's admin account, and the auto-approve decision in MediaRequest.createRequest
            // checks *that* caller's permissions, not requestBody.userId - so a plain userId body
            // field only changes who gets attributed, not whether the request is auto-approved. A
            // userId body field together with X-Api-User throws a 403 (the target user lacks
            // MANAGE_USERS/MANAGE_REQUESTS to "set the request user" themselves), so it must be
            // omitted here.
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(baseUrl, "request"))
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            request.Headers.Add("X-Api-User", jellyseerrUserId.ToString(CultureInfo.InvariantCulture));

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Jellyseerr POST /request failed with HTTP {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
                return new JellyseerrCreateRequestResult(false, null, string.Format(CultureInfo.InvariantCulture, "HTTP {0}.", (int)response.StatusCode));
            }

            var dto = await response.Content.ReadFromJsonAsync<MediaRequestJson>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return dto is null
                ? new JellyseerrCreateRequestResult(false, null, "Empty response body.")
                : new JellyseerrCreateRequestResult(true, (JellyseerrRequestStatus)dto.Status, null);
        }
        catch (HttpRequestException ex)
        {
            return new JellyseerrCreateRequestResult(false, null, ex.Message);
        }
        catch (JsonException ex)
        {
            return new JellyseerrCreateRequestResult(false, null, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<JellyseerrRadarrLookupResult> GetRadarrServersAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient(out var baseUrl);
            if (client is null)
            {
                return new JellyseerrRadarrLookupResult(false, null, null, "Jellyseerr is not configured.");
            }

            using var response = await client.GetAsync(BuildUrl(baseUrl, "service/radarr"), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new JellyseerrRadarrLookupResult(false, null, null, string.Format(CultureInfo.InvariantCulture, "HTTP {0}.", (int)response.StatusCode));
            }

            var dto = await response.Content.ReadFromJsonAsync<List<RadarrServerJson>>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var servers = (dto ?? []).Select(s => new JellyseerrRadarrServer(s.Id, s.Name, s.IsDefault)).ToList();
            return new JellyseerrRadarrLookupResult(true, servers, null, null);
        }
        catch (HttpRequestException ex)
        {
            return new JellyseerrRadarrLookupResult(false, null, null, ex.Message);
        }
        catch (JsonException ex)
        {
            return new JellyseerrRadarrLookupResult(false, null, null, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<JellyseerrRadarrLookupResult> GetRadarrProfilesAsync(int radarrServerId, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient(out var baseUrl);
            if (client is null)
            {
                return new JellyseerrRadarrLookupResult(false, null, null, "Jellyseerr is not configured.");
            }

            using var response = await client.GetAsync(BuildUrl(baseUrl, "service/radarr/" + radarrServerId.ToString(CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new JellyseerrRadarrLookupResult(false, null, null, string.Format(CultureInfo.InvariantCulture, "HTTP {0}.", (int)response.StatusCode));
            }

            var dto = await response.Content.ReadFromJsonAsync<RadarrServerDetailJson>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var profiles = (dto?.Profiles ?? []).Select(p => new JellyseerrRadarrProfile(p.Id, p.Name)).ToList();
            return new JellyseerrRadarrLookupResult(true, null, profiles, null);
        }
        catch (HttpRequestException ex)
        {
            return new JellyseerrRadarrLookupResult(false, null, null, ex.Message);
        }
        catch (JsonException ex)
        {
            return new JellyseerrRadarrLookupResult(false, null, null, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<JellyseerrSonarrLookupResult> GetSonarrServersAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient(out var baseUrl);
            if (client is null)
            {
                return new JellyseerrSonarrLookupResult(false, null, null, "Jellyseerr is not configured.");
            }

            using var response = await client.GetAsync(BuildUrl(baseUrl, "service/sonarr"), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new JellyseerrSonarrLookupResult(false, null, null, string.Format(CultureInfo.InvariantCulture, "HTTP {0}.", (int)response.StatusCode));
            }

            // Same wire shape as /service/radarr - the private *Json DTOs are reused as-is.
            var dto = await response.Content.ReadFromJsonAsync<List<RadarrServerJson>>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var servers = (dto ?? []).Select(s => new JellyseerrSonarrServer(s.Id, s.Name, s.IsDefault)).ToList();
            return new JellyseerrSonarrLookupResult(true, servers, null, null);
        }
        catch (HttpRequestException ex)
        {
            return new JellyseerrSonarrLookupResult(false, null, null, ex.Message);
        }
        catch (JsonException ex)
        {
            return new JellyseerrSonarrLookupResult(false, null, null, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<JellyseerrSonarrLookupResult> GetSonarrProfilesAsync(int sonarrServerId, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient(out var baseUrl);
            if (client is null)
            {
                return new JellyseerrSonarrLookupResult(false, null, null, "Jellyseerr is not configured.");
            }

            using var response = await client.GetAsync(BuildUrl(baseUrl, "service/sonarr/" + sonarrServerId.ToString(CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new JellyseerrSonarrLookupResult(false, null, null, string.Format(CultureInfo.InvariantCulture, "HTTP {0}.", (int)response.StatusCode));
            }

            var dto = await response.Content.ReadFromJsonAsync<RadarrServerDetailJson>(JsonOptions, cancellationToken).ConfigureAwait(false);
            var profiles = (dto?.Profiles ?? []).Select(p => new JellyseerrSonarrProfile(p.Id, p.Name)).ToList();
            return new JellyseerrSonarrLookupResult(true, null, profiles, null);
        }
        catch (HttpRequestException ex)
        {
            return new JellyseerrSonarrLookupResult(false, null, null, ex.Message);
        }
        catch (JsonException ex)
        {
            return new JellyseerrSonarrLookupResult(false, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Builds the HTTP client, or null if Jellyseerr isn't configured. The API key is set as a
    /// default request header here and nowhere logged (docs/implementation-plan.md §3.1's secrets
    /// rule applies to every external API key this plugin holds, not just MDBList's).
    /// </summary>
    private HttpClient? CreateClient(out string baseUrl)
    {
        var config = Plugin.Instance!.Configuration;
        baseUrl = config.JellyseerrUrl;

        if (string.IsNullOrWhiteSpace(config.JellyseerrUrl) || string.IsNullOrWhiteSpace(config.JellyseerrApiKey))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("X-Api-Key", config.JellyseerrApiKey);
        return client;
    }

    private static string BuildUrl(string baseUrl, string relativePath) =>
        baseUrl.TrimEnd('/') + "/api/v1/" + relativePath.TrimStart('/');

    private sealed record MediaDetailsJson([property: JsonPropertyName("mediaInfo")] MediaInfoJson? MediaInfo);

    private sealed record MediaInfoJson([property: JsonPropertyName("status")] int Status);

    private sealed record JellyseerrUserJson([property: JsonPropertyName("id")] int Id);

    private sealed record MediaRequestJson([property: JsonPropertyName("id")] int Id, [property: JsonPropertyName("status")] int Status);

    private sealed record CreateRequestJson(
        [property: JsonPropertyName("mediaType")] string MediaType,
        [property: JsonPropertyName("mediaId")] int MediaId,
        [property: JsonPropertyName("serverId")] int? ServerId,
        [property: JsonPropertyName("profileId")] int? ProfileId,
        [property: JsonPropertyName("seasons")] string? Seasons);

    private sealed record RadarrServerJson(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("isDefault")] bool IsDefault);

    private sealed record RadarrProfileJson(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record RadarrServerDetailJson([property: JsonPropertyName("profiles")] List<RadarrProfileJson>? Profiles);
}
