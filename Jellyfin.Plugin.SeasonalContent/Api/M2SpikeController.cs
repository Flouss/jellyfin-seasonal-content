using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SeasonalContent.Api;

/// <summary>
/// THROWAWAY spike code for docs/implementation-plan.md M2. Proves that
/// <see cref="ICollectionManager"/> can build a BoxSet containing one stub item and one real,
/// owned item, and records the actual <see cref="CollectionCreationOptions"/> shape. Superseded
/// by the real collection builder in M4 (§3.7) — delete this controller then.
/// </summary>
[Route("SeasonalContent")]
[Authorize(Policy = Policies.RequiresElevation)]
public class M2SpikeController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="M2SpikeController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="collectionManager">Instance of the <see cref="ICollectionManager"/> interface.</param>
    public M2SpikeController(ILibraryManager libraryManager, ICollectionManager collectionManager)
    {
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
    }

    /// <summary>
    /// Creates a spike BoxSet from a hardcoded owned TMDb id and a hardcoded stub TMDb id.
    /// </summary>
    /// <param name="ownedTmdbId">TMDb id of a real, owned movie already in the library.</param>
    /// <param name="stubTmdbId">TMDb id of a stub movie already scanned into the spike library.</param>
    /// <returns>Details of what was found and created.</returns>
    [HttpPost("SpikeCreateCollection")]
    public async Task<ActionResult> SpikeCreateCollection([FromQuery] string ownedTmdbId, [FromQuery] string stubTmdbId)
    {
        var owned = FindMovieByTmdbId(ownedTmdbId);
        var stub = FindMovieByTmdbId(stubTmdbId);

        if (owned is null || stub is null)
        {
            return NotFound(new
            {
                ownedFound = owned is not null,
                stubFound = stub is not null
            });
        }

        var boxSet = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
        {
            Name = "Seasonal Content Spike (M2)",
            ItemIdList = [owned.Id.ToString("N"), stub.Id.ToString("N")]
        }).ConfigureAwait(false);

        return Ok(new
        {
            collectionId = boxSet.Id,
            collectionName = boxSet.Name,
            ownedItem = new { owned.Id, owned.Name, owned.Path },
            stubItem = new { stub.Id, stub.Name, stub.Path }
        });
    }

    private BaseItem? FindMovieByTmdbId(string tmdbId)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie],
            Recursive = true,
            HasAnyProviderId = new() { ["Tmdb"] = tmdbId }
        };

        return _libraryManager.GetItemList(query).FirstOrDefault();
    }
}
