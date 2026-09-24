using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Lists.MdbList;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Lists.MdbList;

public class MdbListApiClientTests
{
    private const string ApiKey = "super-secret-key-value";

    [Fact]
    public async Task RequestsTheDocumentedEndpointWithUsernameSlugLimitAndOffset()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"movies":[],"pagination":{"offset":20,"limit":10,"total":0,"has_more":false}}""")
        });
        var client = new MdbListApiClient(new FakeHttpClientFactory(handler));

        await client.GetItemsPageAsync("hdlists", "the-top-100-halloween-movies-of-all-time", ApiKey, 10, 20, CancellationToken.None);

        var uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("api.mdblist.com", uri.Host);
        Assert.Equal("/lists/hdlists/the-top-100-halloween-movies-of-all-time/items", uri.AbsolutePath);
        Assert.Contains("limit=10", uri.Query, StringComparison.Ordinal);
        Assert.Contains("offset=20", uri.Query, StringComparison.Ordinal);
        Assert.Contains($"apikey={ApiKey}", uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeserializesARealResponseBody()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mdblist-real-sample.json"));
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });
        var client = new MdbListApiClient(new FakeHttpClientFactory(handler));

        var page = await client.GetItemsPageAsync("hdlists", "slug", ApiKey, 7, 0, CancellationToken.None);

        Assert.Equal(5, page.Movies.Count);
        Assert.True(page.Pagination!.HasMore);
    }

    [Fact]
    public async Task ThrowsOnFailureWithoutLeakingTheApiKeyOrFullUrl()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Invalid API key")
        });
        var client = new MdbListApiClient(new FakeHttpClientFactory(handler));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetItemsPageAsync("hdlists", "slug", ApiKey, 10, 0, CancellationToken.None));

        Assert.DoesNotContain(ApiKey, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("apikey=", ex.Message, StringComparison.Ordinal);
    }
}
