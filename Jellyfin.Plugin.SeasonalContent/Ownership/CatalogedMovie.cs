using System;

namespace Jellyfin.Plugin.SeasonalContent.Ownership;

/// <summary>
/// One real movie found in the library, as reported by <see cref="IMovieCatalog"/>.
/// </summary>
/// <param name="TmdbId">The movie's TMDb id.</param>
/// <param name="Path">The item's full path - used only to exclude stub-root items.</param>
/// <param name="ItemId">The Jellyfin item id.</param>
public sealed record CatalogedMovie(int TmdbId, string Path, Guid ItemId);
