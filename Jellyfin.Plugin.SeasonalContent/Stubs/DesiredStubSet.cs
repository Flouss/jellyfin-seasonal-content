using System.Collections.Generic;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <summary>
/// Deduplicates not-owned items from every list that fetched successfully this sync into one
/// desired stub set, keyed by TMDb id. Per docs/implementation-plan.md §3.2: one stub folder for
/// all lists, so a title present on two lists gets exactly one stub.
/// </summary>
public static class DesiredStubSet
{
    /// <summary>
    /// Builds the deduplicated desired stub set.
    /// </summary>
    /// <param name="notOwnedItems">Not-owned items from every list that fetched successfully this sync.</param>
    /// <returns>One <see cref="ListItem"/> per distinct TMDb id.</returns>
    public static IReadOnlyList<ListItem> Build(IEnumerable<ListItem> notOwnedItems)
    {
        var byTmdbId = new Dictionary<int, ListItem>();

        foreach (var item in notOwnedItems)
        {
            // Same convention as OwnedItemIndex.Build: last one wins, arbitrarily.
            byTmdbId[item.TmdbId] = item;
        }

        return [.. byTmdbId.Values];
    }
}
