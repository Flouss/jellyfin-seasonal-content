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
        var index = new Dictionary<(MediaKind, int), Guid> { [(MediaKind.Movie, 389)] = ownedId };

        var result = ListPartitioner.Partition(items, index);

        var owned = Assert.Single(result.Owned);
        Assert.Equal(ownedId, owned.OwnedItemId);
        Assert.Empty(result.NotOwned);
    }

    [Fact]
    public void PutsAnItemWhoseTmdbIdIsNotInTheIndexIntoNotOwned()
    {
        var items = new[] { new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1") };
        var index = new Dictionary<(MediaKind, int), Guid>();

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

        var result = ListPartitioner.Partition(items, new Dictionary<(MediaKind, int), Guid>());

        Assert.Empty(result.Owned);
        Assert.Equal(2, result.NotOwned.Count);
    }

    [Fact]
    public void AShowIsNotConsideredOwnedByAMovieSharingTheSameTmdbId()
    {
        // Pins the TMDb-collision constraint (docs/rename-tv-globalkey-plan.md): a list can
        // contain a movie and a show with the same numeric TMDb id (their id spaces are
        // independent) - only the matching kind's ownership entry should satisfy the other.
        var movieOwnedId = Guid.NewGuid();
        var items = new[]
        {
            new ListItem(4977, "tt0851578", "Some Movie", 2006, "list-1", MediaKind.Movie),
            new ListItem(4977, "tt9999999", "Some Show", 2006, "list-1", MediaKind.Series)
        };
        var index = new Dictionary<(MediaKind, int), Guid> { [(MediaKind.Movie, 4977)] = movieOwnedId };

        var result = ListPartitioner.Partition(items, index);

        var owned = Assert.Single(result.Owned);
        Assert.Equal(MediaKind.Movie, owned.Item.Kind);
        var notOwned = Assert.Single(result.NotOwned);
        Assert.Equal(MediaKind.Series, notOwned.Kind);
    }
}
