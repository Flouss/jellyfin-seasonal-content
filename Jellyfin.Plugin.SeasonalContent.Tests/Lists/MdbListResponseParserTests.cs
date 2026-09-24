using System;
using System.IO;
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
        // Fixture: 5 real movies captured live from api.mdblist.com/lists/hdlists/the-top-100-halloween-movies-of-all-time
        var response = LoadFixture("mdblist-real-sample.json");

        var items = MdbListResponseParser.ToListItems(response, "test-list-id");

        Assert.Equal(5, items.Count);

        var ernest = Assert.Single(items, i => i.Title == "Ernest Scared Stupid");
        Assert.Equal(32685, ernest.TmdbId);
        Assert.Equal("tt0101821", ernest.ImdbId);
        Assert.Equal(1991, ernest.Year);
        Assert.Equal("test-list-id", ernest.SourceListId);
    }

    [Fact]
    public void IgnoresTheSeparateShowsArray()
    {
        // The response also carries a top-level "shows" array (confirmed live) - v1 is movies only.
        // Fixture's shows are "Chilling Adventures of Sabrina" and "Ash vs Evil Dead" - neither
        // should leak into the parsed movie items.
        var response = LoadFixture("mdblist-real-sample.json");

        var items = MdbListResponseParser.ToListItems(response, "test-list-id");

        Assert.Equal(response.Movies.Count, items.Count);
        Assert.DoesNotContain(items, i => i.Title == "Chilling Adventures of Sabrina" || i.Title == "Ash vs Evil Dead");
    }

    [Fact]
    public void SkipsMoviesWithNoTmdbId()
    {
        var response = LoadFixture("mdblist-one-missing-tmdb.json");

        var items = MdbListResponseParser.ToListItems(response, "test-list-id");

        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, i => i.Title == "The Final Girls");
    }
}
