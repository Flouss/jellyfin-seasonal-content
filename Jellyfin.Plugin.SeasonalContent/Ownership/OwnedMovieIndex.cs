using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.SeasonalContent.Ownership;

/// <summary>
/// Builds the "is this TMDb id already owned" lookup once per sync, excluding stub-root items.
/// Fixes the P0 defect confirmed in the POC (docs/implementation-plan.md §3.3): the POC's
/// ownership check was never wired into its seasonal path at all, so an owned title still got a
/// duplicate stub. Here, the exclusion is structural - a stub can never appear in this index -
/// rather than depending on every caller remembering to check first.
/// </summary>
public static class OwnedMovieIndex
{
    /// <summary>
    /// Builds a TMDb id to item id lookup from every real movie found in the library.
    /// </summary>
    /// <param name="movies">Every movie the library reports, including stubs.</param>
    /// <param name="stubRootPath">The stub root; any movie under it is not "owned".</param>
    /// <returns>A lookup of TMDb id to item id, for real (non-stub) movies only.</returns>
    public static IReadOnlyDictionary<int, Guid> Build(IEnumerable<CatalogedMovie> movies, string stubRootPath)
    {
        var index = new Dictionary<int, Guid>();

        foreach (var movie in movies)
        {
            if (StubPath.IsUnderRoot(movie.Path, stubRootPath))
            {
                continue;
            }

            // Same TMDb id in more than one real library: any one of them is fine to reference
            // (docs/implementation-plan.md §3.3 edge case) - last one wins, arbitrarily.
            index[movie.TmdbId] = movie.ItemId;
        }

        return index;
    }
}
