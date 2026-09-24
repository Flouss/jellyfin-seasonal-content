using System.Collections.Generic;

namespace Jellyfin.Plugin.SeasonalContent.Ownership;

/// <summary>
/// Thin boundary around <c>ILibraryManager</c> - one recursive query for every movie/series with a
/// TMDb id (docs/implementation-plan.md §3.3: "don't do one query per title", extended to TV in
/// docs/rename-tv-globalkey-plan.md). Kept separate from <see cref="OwnedItemIndex"/> so the
/// exclusion logic can be unit tested against a fake.
/// </summary>
public interface IMediaCatalog
{
    /// <summary>
    /// Gets every movie and TV series in the library that has a TMDb id, stubs included.
    /// </summary>
    /// <returns>Every such item, exactly once per query (no per-title lookups).</returns>
    IReadOnlyList<CatalogedItem> GetAllItemsWithTmdbId();
}
