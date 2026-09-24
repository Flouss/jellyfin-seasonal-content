using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Lists.MdbList;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Lists.MdbList;

public class MdbListSourceTests
{
    private static MdbListResponse LoadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return JsonSerializer.Deserialize<MdbListResponse>(File.ReadAllText(path))!;
    }

    [Fact]
    public async Task FollowsPaginationUntilHasMoreIsFalse()
    {
        var fake = new FakeMdbListApiClient();
        fake.EnqueueResponse(LoadFixture("mdblist-page1-of-2.json"));
        fake.EnqueueResponse(LoadFixture("mdblist-page2-of-2.json"));
        var source = new MdbListSource(fake);

        var items = await source.GetItemsAsync(
            new ListSourceRequest("list-1", "hdlists", "the-top-100-halloween-movies-of-all-time", "fake-key", 3),
            CancellationToken.None);

        // 3 real movies on page 1 + 2 real movies on page 2, no duplicates, no drops.
        Assert.Equal(5, items.Count);
        Assert.Equal(5, items.Select(i => i.TmdbId).Distinct().Count());
        Assert.Contains(items, i => i.Title == "Ernest Scared Stupid");
        Assert.Contains(items, i => i.Title == "The Witches");
    }

    [Fact]
    public async Task StopsAfterOnePageWhenHasMoreIsFalse()
    {
        // Only one response is queued. If the source asked for a second page, the fake would
        // throw "no more queued responses" and this test would fail for that reason instead.
        var fake = new FakeMdbListApiClient();
        fake.EnqueueResponse(LoadFixture("mdblist-page2-of-2.json")); // has_more: false

        var source = new MdbListSource(fake);
        var items = await source.GetItemsAsync(
            new ListSourceRequest("list-1", "hdlists", "slug", "fake-key", 3),
            CancellationToken.None);

        Assert.Single(fake.Calls);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task PassesClampedLimitAndOffsetToTheApiClient()
    {
        var fake = new FakeMdbListApiClient();
        fake.EnqueueResponse(LoadFixture("mdblist-page1-of-2.json"));
        fake.EnqueueResponse(LoadFixture("mdblist-page2-of-2.json"));
        var source = new MdbListSource(fake);

        await source.GetItemsAsync(
            new ListSourceRequest("list-1", "hdlists", "slug", "fake-key", 999), // above the 500 cap
            CancellationToken.None);

        Assert.Equal(2, fake.Calls.Count);
        Assert.Equal(500, fake.Calls[0].Limit);
        Assert.Equal(0, fake.Calls[0].Offset);
        Assert.Equal(3, fake.Calls[1].Offset); // page 1's offset(0) + limit(3) from the fixture
    }

    [Fact]
    public async Task DoesNotSilentlyReturnEmptyOnFetchFailure()
    {
        // The POC's bug: a failed fetch returned an empty list, which combined with stub
        // cleanup would wipe everything after one network blip. A failure must surface as an
        // exception, never as "the list is empty".
        var fake = new FakeMdbListApiClient();
        fake.EnqueueFailure(new HttpRequestException("503 Service Unavailable"));
        var source = new MdbListSource(fake);

        await Assert.ThrowsAsync<MdbListFetchException>(() => source.GetItemsAsync(
            new ListSourceRequest("list-1", "hdlists", "slug", "fake-key", 100),
            CancellationToken.None));
    }

    [Fact]
    public async Task NeverIncludesTheApiKeyInAFailureMessage()
    {
        var fake = new FakeMdbListApiClient();
        fake.EnqueueFailure(new HttpRequestException("401 Unauthorized"));
        var source = new MdbListSource(fake);

        var ex = await Assert.ThrowsAsync<MdbListFetchException>(() => source.GetItemsAsync(
            new ListSourceRequest("list-1", "hdlists", "slug", "super-secret-key-value", 100),
            CancellationToken.None));

        Assert.DoesNotContain("super-secret-key-value", ex.ToString(), StringComparison.Ordinal);
    }
}
