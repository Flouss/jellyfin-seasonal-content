using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Collections;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Jellyfin.Plugin.SeasonalContent.Setup;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SeasonalContent.Api;

/// <summary>
/// Admin-only list configuration endpoint backing the M6 config page's Lists section (list
/// add/remove, paste-a-URL parsing happen client-side; this is the save/load API).
/// </summary>
[Route("SeasonalContent")]
[Authorize(Policy = Policies.RequiresElevation)]
public class ListsController : ControllerBase
{
    private readonly ICollectionReconciler _collectionReconciler;
    private readonly ILibrarySetupService _librarySetupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListsController"/> class.
    /// </summary>
    /// <param name="collectionReconciler">Instance of the <see cref="ICollectionReconciler"/> interface.</param>
    /// <param name="librarySetupService">Instance of the <see cref="ILibrarySetupService"/> interface.</param>
    public ListsController(ICollectionReconciler collectionReconciler, ILibrarySetupService librarySetupService)
    {
        _collectionReconciler = collectionReconciler;
        _librarySetupService = librarySetupService;
    }

    /// <summary>
    /// Returns every saved list. No secret on this DTO anymore - the MDBList API key is now global
    /// (<see cref="PluginConfiguration.MdbListApiKey"/>, round-tripped via the standard plugin
    /// configuration endpoint like <c>JellyseerrApiKey</c> already was), not per-list.
    /// </summary>
    /// <returns>The saved lists.</returns>
    [HttpGet("Lists")]
    public ActionResult GetLists()
    {
        var lists = Plugin.Instance!.Configuration.Lists.Select(l => new
        {
            l.Id,
            l.Enabled,
            l.DisplayName,
            l.Username,
            l.Slug,
            l.Limit,
            l.CollectionId
        });

        return Ok(lists);
    }

    /// <summary>
    /// Replaces the entire saved list set. Every existing list whose id is not present anywhere in
    /// <paramref name="body"/> is treated as deleted: if it has a saved
    /// <see cref="SeasonalListConfig.CollectionId"/> and
    /// <see cref="PluginConfiguration.RemoveCollectionWhenListDisabled"/> is on, its BoxSet is
    /// removed immediately as part of this save, rather than waiting for the next sync (which has
    /// no way to know about a list that's no longer in config at all). A merely-disabled list stays
    /// in config, so the scheduled task handles that case instead (docs/implementation-plan.md §3.7).
    /// </summary>
    /// <param name="body">The full replacement list set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved lists (ApiKey masked) on success, or 400 with validation errors.</returns>
    [HttpPost("Lists")]
    public async Task<ActionResult> SaveLists([FromBody] IReadOnlyList<SeasonalListInput> body, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;
        var result = SeasonalListConfigMerger.Merge(config.Lists, body);

        if (result.Errors.Count > 0)
        {
            return BadRequest(new { errors = result.Errors });
        }

        if (config.RemoveCollectionWhenListDisabled)
        {
            var keptIds = new HashSet<Guid>(result.Lists.Select(l => l.Id));
            var removed = config.Lists.Where(l => !keptIds.Contains(l.Id) && l.CollectionId.HasValue);

            foreach (var list in removed)
            {
                await _collectionReconciler.RemoveCollectionAsync(list.CollectionId!.Value, cancellationToken).ConfigureAwait(false);
            }
        }

        config.Lists = [.. result.Lists];
        Plugin.Instance!.SaveConfiguration();

        return GetLists();
    }

    /// <summary>
    /// Returns both stub roots' full paths, for the config page's "add this as a library" hints
    /// (docs/implementation-plan.md §3.2, docs/rename-tv-globalkey-plan.md §3).
    /// </summary>
    /// <returns>The movie and TV stub roots' full paths.</returns>
    [HttpGet("StubRootPath")]
    public ActionResult GetStubRootPath()
    {
        return Ok(new { path = StubPath.GetRootPath(), tvPath = StubPath.GetTvRootPath() });
    }

    /// <summary>
    /// Creates whichever of the stub library and the Collections library don't already exist,
    /// for the config page's "Set up libraries" button. Idempotent - safe to click repeatedly.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What was created vs. already present.</returns>
    [HttpPost("SetupLibraries")]
    public async Task<ActionResult> SetupLibraries(CancellationToken cancellationToken)
    {
        var result = await _librarySetupService.SetupAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
