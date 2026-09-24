using System.Collections.Generic;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.SeasonalContent.Lists;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SeasonalContent.Ownership;

/// <summary>
/// Real <see cref="IMediaCatalog"/> backed by <see cref="ILibraryManager"/>. This is a thin IO
/// shell with no branching logic worth unit testing on its own (the actual ownership-exclusion
/// logic lives in <see cref="OwnedItemIndex"/>, which is unit tested); this class is verified by a
/// live sync run instead, same convention as the original movies-only M3 P0 live test.
/// </summary>
public sealed class LibraryMediaCatalog : IMediaCatalog
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryMediaCatalog"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public LibraryMediaCatalog(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public IReadOnlyList<CatalogedItem> GetAllItemsWithTmdbId()
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            Recursive = true,
            HasTmdbId = true
        };

        var mediaItems = _libraryManager.GetItemList(query);
        var result = new List<CatalogedItem>(mediaItems.Count);

        foreach (var item in mediaItems)
        {
            if (!item.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbIdString)
                || !int.TryParse(tmdbIdString, out var tmdbId)
                || string.IsNullOrEmpty(item.Path))
            {
                continue;
            }

            var kind = item switch
            {
                Movie => MediaKind.Movie,
                Series => MediaKind.Series,
                _ => (MediaKind?)null
            };

            if (kind is null)
            {
                continue;
            }

            result.Add(new CatalogedItem(tmdbId, kind.Value, item.Path, item.Id));
        }

        return result;
    }
}
