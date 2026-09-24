namespace Jellyfin.Plugin.SeasonalContent.Lists;

/// <summary>
/// One title from a configured list, already flattened away from whichever source produced it.
/// </summary>
/// <param name="TmdbId">The TMDb movie id. Items with no TMDb id never become a <see cref="ListItem"/>.</param>
/// <param name="ImdbId">The IMDb id, when the source provided one.</param>
/// <param name="Title">Display title, as reported by the source.</param>
/// <param name="Year">Release year, when the source provided one.</param>
/// <param name="SourceListId">Id of the configured list this item came from.</param>
public sealed record ListItem(int TmdbId, string? ImdbId, string Title, int? Year, string SourceListId);
