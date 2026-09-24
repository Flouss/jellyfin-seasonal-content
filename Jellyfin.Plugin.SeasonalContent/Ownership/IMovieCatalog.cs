using System.Collections.Generic;

namespace Jellyfin.Plugin.SeasonalContent.Ownership;

/// <summary>
/// Thin boundary around <c>ILibraryManager</c> - one recursive query for every movie with a
/// TMDb id (docs/implementation-plan.md §3.3: "don't do one query per title"). Kept separate
/// from <see cref="OwnedMovieIndex"/> so the exclusion logic can be unit tested against a fake.
/// </summary>
public interface IMovieCatalog
{
    /// <summary>
    /// Gets every movie in the library that has a TMDb id, stubs included.
    /// </summary>
    /// <returns>Every such movie, exactly once per query (no per-title lookups).</returns>
    IReadOnlyList<CatalogedMovie> GetAllMoviesWithTmdbId();
}
