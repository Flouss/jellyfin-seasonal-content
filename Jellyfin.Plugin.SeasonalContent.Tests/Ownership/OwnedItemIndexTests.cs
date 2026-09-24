using System;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Ownership;

public class OwnedItemIndexTests
{
    private const string MovieStubRoot = "/config/data/plugins/Jellyfin.Plugin.SeasonalContent/movies_seasonal";
    private const string TvStubRoot = "/config/data/plugins/Jellyfin.Plugin.SeasonalContent/tv_seasonal";
    private static readonly string[] StubRoots = [MovieStubRoot, TvStubRoot];

    [Fact]
    public void IncludesARealMovieOutsideTheStubRoots()
    {
        var id = Guid.NewGuid();
        var items = new[] { new CatalogedItem(389, MediaKind.Movie, "/mnt/Movies/12 Angry Men (1957)/12 Angry Men (1957).mp4", id) };

        var index = OwnedItemIndex.Build(items, StubRoots);

        Assert.Equal(id, index[(MediaKind.Movie, 389)]);
    }

    [Fact]
    public void ExcludesAnItemUnderTheMovieStubRoot()
    {
        // The P0 defect this whole ownership check exists to fix (docs/implementation-plan.md
        // §3.3): a stub must never count as "owned" just because it has a TMDb id.
        var items = new[]
        {
            new CatalogedItem(4977, MediaKind.Movie, $"{MovieStubRoot}/Paprika (2006) [tmdbid-4977].strm", Guid.NewGuid())
        };

        var index = OwnedItemIndex.Build(items, StubRoots);

        Assert.False(index.ContainsKey((MediaKind.Movie, 4977)));
    }

    [Fact]
    public void ExcludesAnItemUnderTheTvStubRoot()
    {
        var items = new[]
        {
            new CatalogedItem(79242, MediaKind.Series, $"{TvStubRoot}/Chilling Adventures of Sabrina (2018) [tmdbid-79242]/Season 01/ep.strm", Guid.NewGuid())
        };

        var index = OwnedItemIndex.Build(items, StubRoots);

        Assert.False(index.ContainsKey((MediaKind.Series, 79242)));
    }

    [Fact]
    public void EmptyCatalogProducesAnEmptyIndex()
    {
        var index = OwnedItemIndex.Build(Array.Empty<CatalogedItem>(), StubRoots);

        Assert.Empty(index);
    }

    [Fact]
    public void DuplicateTmdbIdAcrossTwoLibrariesDoesNotThrow()
    {
        var items = new[]
        {
            new CatalogedItem(389, MediaKind.Movie, "/mnt/Movies/12 Angry Men (1957)/12 Angry Men (1957).mp4", Guid.NewGuid()),
            new CatalogedItem(389, MediaKind.Movie, "/mnt/Movies4K/12 Angry Men (1957)/12 Angry Men (1957).mkv", Guid.NewGuid())
        };

        var index = OwnedItemIndex.Build(items, StubRoots);

        Assert.True(index.ContainsKey((MediaKind.Movie, 389)));
    }

    [Fact]
    public void AMovieAndAShowSharingTheSameTmdbIdAreBothOwnedIndependently()
    {
        // Pins the TMDb-collision constraint (docs/rename-tv-globalkey-plan.md): reverting the key
        // from (MediaKind, TmdbId) back to a bare TmdbId must fail this test - a movie owning id
        // 4977 must never make a same-numbered show look owned, and vice versa.
        var movieItemId = Guid.NewGuid();
        var showItemId = Guid.NewGuid();
        var items = new[]
        {
            new CatalogedItem(4977, MediaKind.Movie, "/mnt/Movies/Some Movie/movie.mkv", movieItemId),
            new CatalogedItem(4977, MediaKind.Series, "/mnt/TV/Some Show/Season 01/ep.mkv", showItemId)
        };

        var index = OwnedItemIndex.Build(items, StubRoots);

        Assert.Equal(movieItemId, index[(MediaKind.Movie, 4977)]);
        Assert.Equal(showItemId, index[(MediaKind.Series, 4977)]);
    }
}
