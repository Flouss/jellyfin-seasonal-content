using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// One entry in an MDBList response's <c>movies</c> array. Field names verified live
/// (docs/decisions.md); several real fields (mediatype, country, runtime, ...) exist but are
/// unused and deliberately not mapped.
/// </summary>
public sealed class MdbListMovie
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release year.
    /// </summary>
    [JsonPropertyName("release_year")]
    public int? ReleaseYear { get; set; }

    /// <summary>
    /// Gets or sets the top-level IMDb id (also present, duplicated, at <c>Ids.Imdb</c>).
    /// </summary>
    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    /// <summary>
    /// Gets or sets the provider id bundle.
    /// </summary>
    [JsonPropertyName("ids")]
    public MdbListIds? Ids { get; set; }
}
