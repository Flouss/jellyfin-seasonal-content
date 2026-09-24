using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// Pagination info on an MDBList response. Verified live against a 125-item list
/// (docs/decisions.md) - contradicts the plan's original "pagination is not proven" note.
/// </summary>
public sealed class MdbListPagination
{
    /// <summary>
    /// Gets or sets the offset of this page.
    /// </summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    /// <summary>
    /// Gets or sets the page size requested.
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>
    /// Gets or sets the total item count across movies+shows+seasons+episodes.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether more pages remain.
    /// </summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}
