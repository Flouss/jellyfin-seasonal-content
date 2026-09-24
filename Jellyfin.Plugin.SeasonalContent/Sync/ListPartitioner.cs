using System;
using System.Collections.Generic;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.Sync;

/// <summary>
/// Splits a list's items into owned (server already has this title) and not-owned (needs a
/// stub), per docs/implementation-plan.md §3.2 step 1.
/// </summary>
public static class ListPartitioner
{
    /// <summary>
    /// Partitions <paramref name="items"/> against an owned-movie index.
    /// </summary>
    /// <param name="items">The list's items.</param>
    /// <param name="ownedIndex">TMDb id to item id, from <see cref="Ownership.OwnedMovieIndex"/>.</param>
    /// <returns>The partitioned result.</returns>
    public static PartitionResult Partition(IReadOnlyList<ListItem> items, IReadOnlyDictionary<int, Guid> ownedIndex)
    {
        var owned = new List<OwnedListItem>();
        var notOwned = new List<ListItem>();

        foreach (var item in items)
        {
            if (ownedIndex.TryGetValue(item.TmdbId, out var ownedItemId))
            {
                owned.Add(new OwnedListItem(item, ownedItemId));
            }
            else
            {
                notOwned.Add(item);
            }
        }

        return new PartitionResult(owned, notOwned);
    }
}
