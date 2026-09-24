using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Collections;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SeasonalContent.Api;

/// <summary>
/// Admin-only, bare-bones JSON list configuration endpoint (docs/m4-plan.md). Stands in for the
/// full M6 config UI (list add/remove, paste-a-URL parsing) - it exists only so the scheduled
/// task has saved config to read.
/// </summary>
[Route("SeasonalContent")]
[Authorize(Policy = Policies.RequiresElevation)]
public class ListsController : ControllerBase
{
    private readonly ICollectionReconciler _collectionReconciler;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListsController"/> class.
    /// </summary>
    /// <param name="collectionReconciler">Instance of the <see cref="ICollectionReconciler"/> interface.</param>
    public ListsController(ICollectionReconciler collectionReconciler)
    {
        _collectionReconciler = collectionReconciler;
    }

    /// <summary>
    /// Returns every saved list, with <see cref="SeasonalListConfig.ApiKey"/> masked
    /// (docs/decisions.md security note: never echo a full key back).
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
            ApiKey = Mask(l.ApiKey),
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

    private static string Mask(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return string.Empty;
        }

        return apiKey.Length <= 4
            ? new string('*', apiKey.Length)
            : string.Concat(new string('*', apiKey.Length - 4), apiKey.AsSpan(apiKey.Length - 4));
    }
}
