using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// One entry in an MDBList response's <c>shows</c> array. Same shape as <see cref="MdbListMovie"/>
/// - verified live against the real API (docs/rename-tv-globalkey-plan.md): a show carries the same
/// <c>title</c>/<c>release_year</c>/<c>imdb_id</c>/<c>ids</c> fields as a movie.
/// </summary>
public sealed class MdbListShow
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release year (the show's first-aired year).
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
