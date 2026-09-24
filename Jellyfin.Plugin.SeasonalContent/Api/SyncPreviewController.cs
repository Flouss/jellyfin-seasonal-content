using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Jellyfin.Plugin.SeasonalContent.Sync;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SeasonalContent.Api;

/// <summary>
/// Admin diagnostic: runs the M3 pipeline (fetch list -&gt; build owned index -&gt; partition)
/// against a real list with no side effects, so the ownership check (docs/implementation-plan.md
/// §3.3, P0) can be verified live before the M4 stub reconciler exists. Takes the list to preview
/// in the request body rather than reading saved config, since the Lists config UI itself is M6.
/// </summary>
[Route("SeasonalContent")]
[Authorize(Policy = Policies.RequiresElevation)]
public class SyncPreviewController : ControllerBase
{
    private readonly IListSource _listSource;
    private readonly IMovieCatalog _movieCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPreviewController"/> class.
    /// </summary>
    /// <param name="listSource">Instance of the <see cref="IListSource"/> interface.</param>
    /// <param name="movieCatalog">Instance of the <see cref="IMovieCatalog"/> interface.</param>
    public SyncPreviewController(IListSource listSource, IMovieCatalog movieCatalog)
    {
        _listSource = listSource;
        _movieCatalog = movieCatalog;
    }

    /// <summary>
    /// Fetches a list and reports which of its items the server already owns, with no writes.
    /// POST with the key in the body, not a query string: a GET query string ends up in browser
    /// history, Kestrel/proxy access logs and this endpoint's own request path - a body does not.
    /// </summary>
    /// <param name="body">The list to preview, including its API key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Owned/not-owned counts and a small sample of each.</returns>
    [HttpPost("SyncPreview")]
    public async Task<ActionResult> SyncPreview([FromBody] SyncPreviewRequest body, CancellationToken cancellationToken)
    {
        var request = new ListSourceRequest("preview-list", body.Username, body.Slug, body.ApiKey, body.Limit);
        var items = await _listSource.GetItemsAsync(request, cancellationToken).ConfigureAwait(false);

        // No stub-writer exists yet (that's M4); this is the default stub root path it will use
        // (docs/implementation-plan.md §3.2), computed the same way so this preview's exclusion
        // logic matches what M4 will actually do.
        var stubRootPath = Path.Combine(Plugin.Instance!.DataFolderPath, "movies_seasonal");

        var ownedIndex = OwnedMovieIndex.Build(_movieCatalog.GetAllMoviesWithTmdbId(), stubRootPath);

        var partition = ListPartitioner.Partition(items, ownedIndex);

        return Ok(new
        {
            totalFetched = items.Count,
            ownedCount = partition.Owned.Count,
            notOwnedCount = partition.NotOwned.Count,
            ownedSample = partition.Owned.Take(25).Select(o => new { o.Item.Title, o.Item.TmdbId, ownedItemId = o.OwnedItemId }),
            notOwnedSample = partition.NotOwned.Take(10).Select(i => new { i.Title, i.TmdbId, i.Year })
        });
    }
}

/// <summary>
/// Request body for <see cref="SyncPreviewController.SyncPreview"/>.
/// </summary>
/// <param name="Username">MDBList username.</param>
/// <param name="Slug">MDBList list slug.</param>
/// <param name="ApiKey">MDBList API key. Sent in the body, never the query string.</param>
/// <param name="Limit">Page size to request.</param>
public sealed record SyncPreviewRequest(string Username, string Slug, string ApiKey, int Limit);
