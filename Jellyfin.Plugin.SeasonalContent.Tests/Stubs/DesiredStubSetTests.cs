using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Stubs;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Stubs;

public class DesiredStubSetTests
{
    [Fact]
    public void EmptyInputProducesAnEmptySet()
    {
        // Vacuum check companion to the below tests: an empty desired set must be distinguishable
        // from "everything survived dedup" - both would otherwise look like a passing test.
        var result = DesiredStubSet.Build([]);

        Assert.Empty(result);
    }

    [Fact]
    public void KeepsEveryItemWhenNoTmdbIdRepeats()
    {
        var items = new[]
        {
            new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1"),
            new ListItem(603, "tt0133093", "The Matrix", 1999, "list-2")
        };

        var result = DesiredStubSet.Build(items);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CollapsesTheSameTmdbIdFromTwoDifferentListsIntoOneEntry()
    {
        // The whole reason a shared stub folder exists (docs/implementation-plan.md §3.2): a
        // title in two lists must produce exactly one .strm file, not two.
        var items = new[]
        {
            new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1"),
            new ListItem(4977, "tt0851578", "Paprika", 2006, "list-2")
        };

        var result = DesiredStubSet.Build(items);

        var only = Assert.Single(result);
        Assert.Equal(4977, only.TmdbId);
    }
}
