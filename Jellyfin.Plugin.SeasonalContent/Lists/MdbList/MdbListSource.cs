using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// <see cref="IListSource"/> implementation backed by the MDBList API. Follows pagination to
/// completion (verified live against a 125-item list - see docs/decisions.md) and never treats a
/// failed fetch as an empty list.
/// </summary>
public sealed class MdbListSource : IListSource
{
    private const int MinLimit = 1;
    private const int MaxLimit = 500;

    private readonly IMdbListApiClient _apiClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MdbListSource"/> class.
    /// </summary>
    /// <param name="apiClient">The API client to fetch pages with.</param>
    public MdbListSource(IMdbListApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ListItem>> GetItemsAsync(ListSourceRequest request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, MinLimit, MaxLimit);
        var items = new List<ListItem>();
        var offset = 0;

        while (true)
        {
            MdbListResponse page;
            try
            {
                page = await _apiClient
                    .GetItemsPageAsync(request.Username, request.Slug, request.ApiKey, limit, offset, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new MdbListFetchException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to fetch MDBList list '{0}/{1}' at offset {2}.",
                        request.Username,
                        request.Slug,
                        offset),
                    ex);
            }

            items.AddRange(MdbListResponseParser.ToListItems(page, request.ListId));

            if (page.Pagination is null || !page.Pagination.HasMore)
            {
                break;
            }

            offset = page.Pagination.Offset + page.Pagination.Limit;
        }

        return items;
    }
}
