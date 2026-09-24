using System;
using System.Collections.Generic;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Ownership;

public class OwnedMovieIndexTests
{
    private const string StubRoot = "/config/data/plugins/Jellyfin.Plugin.SeasonalContent/movies_seasonal";

    [Fact]
    public void IncludesARealMovieOutsideTheStubRoot()
    {
        var id = Guid.NewGuid();
        var movies = new[] { new CatalogedMovie(389, "/mnt/Movies/12 Angry Men (1957)/12 Angry Men (1957).mp4", id) };

        var index = OwnedMovieIndex.Build(movies, StubRoot);

        Assert.Equal(id, index[389]);
    }

    [Fact]
    public void ExcludesAnItemUnderTheStubRoot()
    {
        // The P0 defect this whole ownership check exists to fix (docs/implementation-plan.md
        // §3.3): a stub must never count as "owned" just because it has a TMDb id.
        var movies = new[]
        {
            new CatalogedMovie(4977, $"{StubRoot}/Paprika (2006) [tmdbid-4977].strm", Guid.NewGuid())
        };

        var index = OwnedMovieIndex.Build(movies, StubRoot);

        Assert.False(index.ContainsKey(4977));
    }

    [Fact]
    public void EmptyCatalogProducesAnEmptyIndex()
    {
        var index = OwnedMovieIndex.Build(Array.Empty<CatalogedMovie>(), StubRoot);

        Assert.Empty(index);
    }

    [Fact]
    public void DuplicateTmdbIdAcrossTwoLibrariesDoesNotThrow()
    {
        var movies = new[]
        {
            new CatalogedMovie(389, "/mnt/Movies/12 Angry Men (1957)/12 Angry Men (1957).mp4", Guid.NewGuid()),
            new CatalogedMovie(389, "/mnt/Movies4K/12 Angry Men (1957)/12 Angry Men (1957).mkv", Guid.NewGuid())
        };

        var index = OwnedMovieIndex.Build(movies, StubRoot);

        Assert.True(index.ContainsKey(389));
    }
}
