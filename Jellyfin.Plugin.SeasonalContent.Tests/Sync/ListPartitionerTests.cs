using System;
using System.Collections.Generic;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Sync;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Sync;

public class ListPartitionerTests
{
    [Fact]
    public void PutsAnItemWhoseTmdbIdIsInTheIndexIntoOwned()
    {
        var ownedId = Guid.NewGuid();
        var items = new[] { new ListItem(389, "tt0050083", "12 Angry Men", 1957, "list-1") };
        var index = new Dictionary<int, Guid> { [389] = ownedId };

        var result = ListPartitioner.Partition(items, index);

        var owned = Assert.Single(result.Owned);
        Assert.Equal(ownedId, owned.OwnedItemId);
        Assert.Empty(result.NotOwned);
    }

    [Fact]
    public void PutsAnItemWhoseTmdbIdIsNotInTheIndexIntoNotOwned()
    {
        var items = new[] { new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1") };
        var index = new Dictionary<int, Guid>();

        var result = ListPartitioner.Partition(items, index);

        Assert.Empty(result.Owned);
        var notOwned = Assert.Single(result.NotOwned);
        Assert.Equal(4977, notOwned.TmdbId);
    }

    [Fact]
    public void EmptyIndexPutsEverythingInNotOwned()
    {
        // Presence companion to the empty-list case: a correctly-populated list against an empty
        // index must not be mistaken for "nothing to partition".
        var items = new[]
        {
            new ListItem(389, "tt0050083", "12 Angry Men", 1957, "list-1"),
            new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1")
        };

        var result = ListPartitioner.Partition(items, new Dictionary<int, Guid>());

        Assert.Empty(result.Owned);
        Assert.Equal(2, result.NotOwned.Count);
    }
}
