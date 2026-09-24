using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Lists.MdbList;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Lists;

public class MdbListResponseParserTests
{
    private static MdbListResponse LoadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MdbListResponse>(json)!;
    }

    [Fact]
    public void ParsesRealMoviesWithCorrectFields()
    {
        // Fixture: 5 real movies + 2 real shows captured live from
        // api.mdblist.com/lists/hdlists/the-top-100-halloween-movies-of-all-time
        var response = LoadFixture("mdblist-real-sample.json");

        var items = MdbListResponseParser.ToListItems(response, "test-list-id");

        Assert.Equal(5, items.Count(i => i.Kind == MediaKind.Movie));
        Assert.Equal(7, items.Count);

        var ernest = Assert.Single(items, i => i.Title == "Ernest Scared Stupid");
        Assert.Equal(32685, ernest.TmdbId);
        Assert.Equal("tt0101821", ernest.ImdbId);
        Assert.Equal(1991, ernest.Year);
        Assert.Equal("test-list-id", ernest.SourceListId);
    }

    [Fact]
    public void ParsesRealShowsWithCorrectFieldsAndSeriesKind()
    {
        // Fixture's shows are "Chilling Adventures of Sabrina" and "Ash vs Evil Dead", same shape
        // as movies (docs/rename-tv-globalkey-plan.md - verified live).
        var response = LoadFixture("mdblist-real-sample.json");

        var items = MdbListResponseParser.ToListItems(response, "test-list-id");

        Assert.Equal(response.Movies.Count + response.Shows.Count, items.Count);

        var sabrina = Assert.Single(items, i => i.Title == "Chilling Adventures of Sabrina");
        Assert.Equal(79242, sabrina.TmdbId);
        Assert.Equal(MediaKind.Series, sabrina.Kind);
        Assert.Equal(2018, sabrina.Year);

        Assert.All(items.Where(i => i.Title == "Ernest Scared Stupid"), i => Assert.Equal(MediaKind.Movie, i.Kind));
    }

    [Fact]
    public void SkipsMoviesWithNoTmdbId()
    {
        var response = LoadFixture("mdblist-one-missing-tmdb.json");

        var items = MdbListResponseParser.ToListItems(response, "test-list-id");

        Assert.Equal(2, items.Count(i => i.Kind == MediaKind.Movie));
        Assert.DoesNotContain(items, i => i.Title == "The Final Girls");
    }

    [Fact]
    public void SkipsShowsWithNoTmdbId()
    {
        var response = LoadFixture("mdblist-one-show-missing-tmdb.json");

        var items = MdbListResponseParser.ToListItems(response, "test-list-id");

        var only = Assert.Single(items);
        Assert.Equal("Chilling Adventures of Sabrina", only.Title);
        Assert.Equal(MediaKind.Series, only.Kind);
    }
}
