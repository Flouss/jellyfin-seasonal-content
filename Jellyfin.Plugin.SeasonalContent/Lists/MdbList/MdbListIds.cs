using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// The <c>ids</c> bundle on an MDBList item. Field names verified live (docs/decisions.md).
/// </summary>
public sealed class MdbListIds
{
    /// <summary>
    /// Gets or sets the TMDb id. Items with no TMDb id are skipped by
    /// <see cref="MdbListResponseParser"/>.
    /// </summary>
    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }

    /// <summary>
    /// Gets or sets the IMDb id.
    /// </summary>
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    /// <summary>
    /// Gets or sets the TVDb id.
    /// </summary>
    [JsonPropertyName("tvdb")]
    public int? Tvdb { get; set; }
}
