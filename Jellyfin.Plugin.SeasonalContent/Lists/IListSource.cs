using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SeasonalContent.Lists;

/// <summary>
/// Turns one configured list into a flat set of movie items. Exists so a second source (a
/// static JSON URL, a different list provider) could be added later without touching the sync
/// pipeline (docs/implementation-plan.md §3.1) - v1 ships only <see cref="MdbList.MdbListSource"/>.
/// </summary>
public interface IListSource
{
    /// <summary>
    /// Fetches every item in the requested list, following pagination to completion.
    /// </summary>
    /// <param name="request">The list to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Every item in the list with a usable TMDb id.</returns>
    /// <exception cref="MdbList.MdbListFetchException">The fetch failed. Callers must not treat
    /// this the same as a legitimately empty list - see docs/implementation-plan.md §3.1.</exception>
    Task<IReadOnlyList<ListItem>> GetItemsAsync(ListSourceRequest request, CancellationToken cancellationToken);
}
