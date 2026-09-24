using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// Response shape of <c>GET https://api.mdblist.com/lists/{username}/{slug}/items</c>, as verified
/// live against the real API (docs/decisions.md, docs/rename-tv-globalkey-plan.md). Only the
/// fields this plugin uses are mapped; the response also carries top-level <c>seasons</c> and
/// <c>episodes</c> arrays that are deliberately ignored (this plugin requests whole series, not
/// individual seasons/episodes).
/// </summary>
public sealed class MdbListResponse
{
    /// <summary>
    /// Gets or sets the movies in this page of the list.
    /// </summary>
    [JsonPropertyName("movies")]
    public List<MdbListMovie> Movies { get; set; } = [];

    /// <summary>
    /// Gets or sets the TV shows in this page of the list.
    /// </summary>
    [JsonPropertyName("shows")]
    public List<MdbListShow> Shows { get; set; } = [];

    /// <summary>
    /// Gets or sets the pagination info for this page.
    /// </summary>
    [JsonPropertyName("pagination")]
    public MdbListPagination? Pagination { get; set; }
}
