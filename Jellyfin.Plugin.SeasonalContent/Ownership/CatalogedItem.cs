using System;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.Ownership;

/// <summary>
/// One real movie or TV series found in the library, as reported by <see cref="IMediaCatalog"/>.
/// </summary>
/// <param name="TmdbId">The item's TMDb id - only unique within <see cref="Kind"/>, see
/// <see cref="MediaKind"/>'s own doc comment.</param>
/// <param name="Kind">Whether this is a movie or a TV series.</param>
/// <param name="Path">The item's full path - used only to exclude stub-root items.</param>
/// <param name="ItemId">The Jellyfin item id.</param>
public sealed record CatalogedItem(int TmdbId, MediaKind Kind, string Path, Guid ItemId);
