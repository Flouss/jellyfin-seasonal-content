using System.Collections.Generic;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SeasonalContent.Ownership;

/// <summary>
/// Real <see cref="IMovieCatalog"/> backed by <see cref="ILibraryManager"/>. This is a thin IO
/// shell with no branching logic worth unit testing on its own (the actual ownership-exclusion
/// logic lives in <see cref="OwnedMovieIndex"/>, which is unit tested); this class is verified by
/// the M3 P0 live test instead (docs/implementation-plan.md §8, T6).
/// </summary>
public sealed class LibraryMovieCatalog : IMovieCatalog
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryMovieCatalog"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public LibraryMovieCatalog(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public IReadOnlyList<CatalogedMovie> GetAllMoviesWithTmdbId()
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie],
            Recursive = true,
            HasTmdbId = true
        };

        var movies = _libraryManager.GetItemList(query);
        var result = new List<CatalogedMovie>(movies.Count);

        foreach (var item in movies)
        {
            if (!item.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbIdString)
                || !int.TryParse(tmdbIdString, out var tmdbId)
                || string.IsNullOrEmpty(item.Path))
            {
                continue;
            }

            result.Add(new CatalogedMovie(tmdbId, item.Path, item.Id));
        }

        return result;
    }
}
