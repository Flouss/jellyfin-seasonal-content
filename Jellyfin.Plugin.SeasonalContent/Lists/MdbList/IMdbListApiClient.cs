using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// Thin boundary around the MDBList HTTP API - one page per call. Kept separate from
/// <see cref="MdbListSource"/> so the pagination loop can be unit tested against a fake.
/// </summary>
public interface IMdbListApiClient
{
    /// <summary>
    /// Fetches one page of a list's items.
    /// </summary>
    /// <param name="username">MDBList username that owns the list.</param>
    /// <param name="slug">The list's slug.</param>
    /// <param name="apiKey">The MDBList API key. Never include this in a log message or exception.</param>
    /// <param name="limit">Page size, already clamped to 1-500 by the caller.</param>
    /// <param name="offset">Zero-based item offset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response page.</returns>
    Task<MdbListResponse> GetItemsPageAsync(string username, string slug, string apiKey, int limit, int offset, CancellationToken cancellationToken);
}
