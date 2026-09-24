using System.Collections.Generic;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// Flattens an <see cref="MdbListResponse"/> page into <see cref="ListItem"/>s.
/// </summary>
public static class MdbListResponseParser
{
    /// <summary>
    /// Converts a response page into list items, skipping any movie or show with no TMDb id.
    /// </summary>
    /// <param name="response">The deserialized MDBList response.</param>
    /// <param name="sourceListId">Id of the configured list this response came from.</param>
    /// <returns>The parsed list items.</returns>
    public static IReadOnlyList<ListItem> ToListItems(MdbListResponse response, string sourceListId)
    {
        var items = new List<ListItem>(response.Movies.Count + response.Shows.Count);

        foreach (var movie in response.Movies)
        {
            var tmdbId = movie.Ids?.Tmdb;
            if (tmdbId is null || tmdbId <= 0)
            {
                continue;
            }

            items.Add(new ListItem(
                tmdbId.Value,
                movie.ImdbId ?? movie.Ids?.Imdb,
                movie.Title,
                movie.ReleaseYear,
                sourceListId,
                MediaKind.Movie));
        }

        foreach (var show in response.Shows)
        {
            var tmdbId = show.Ids?.Tmdb;
            if (tmdbId is null || tmdbId <= 0)
            {
                continue;
            }

            items.Add(new ListItem(
                tmdbId.Value,
                show.ImdbId ?? show.Ids?.Imdb,
                show.Title,
                show.ReleaseYear,
                sourceListId,
                MediaKind.Series));
        }

        return items;
    }
}
