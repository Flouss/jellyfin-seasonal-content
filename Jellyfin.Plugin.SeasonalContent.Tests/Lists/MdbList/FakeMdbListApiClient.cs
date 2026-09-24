using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Lists.MdbList;

/// <summary>
/// Hand-rolled fake instead of a mocking framework - returns queued responses (or throws a
/// queued exception) in order, and records every call's arguments for assertions.
/// </summary>
public sealed class FakeMdbListApiClient : IMdbListApiClient
{
    private readonly Queue<Func<MdbListResponse>> _responses = new();

    public List<(string Username, string Slug, string ApiKey, int Limit, int Offset)> Calls { get; } = [];

    public void EnqueueResponse(MdbListResponse response) => _responses.Enqueue(() => response);

    public void EnqueueFailure(Exception exception) => _responses.Enqueue(() => throw exception);

    public Task<MdbListResponse> GetItemsPageAsync(string username, string slug, string apiKey, int limit, int offset, CancellationToken cancellationToken)
    {
        Calls.Add((username, slug, apiKey, limit, offset));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("FakeMdbListApiClient: no more queued responses.");
        }

        return Task.FromResult(_responses.Dequeue()());
    }
}
