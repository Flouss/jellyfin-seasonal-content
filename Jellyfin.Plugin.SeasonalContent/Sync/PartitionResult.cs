using System;
using System.Collections.Generic;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.Sync;

/// <summary>
/// One entry of <see cref="PartitionResult.Owned"/>: a list item paired with the real item id
/// that already represents it in the library.
/// </summary>
/// <param name="Item">The list item.</param>
/// <param name="OwnedItemId">The real, owned item's id.</param>
public sealed record OwnedListItem(ListItem Item, Guid OwnedItemId);

/// <summary>
/// The result of splitting a list's items by whether the server already owns them.
/// </summary>
/// <param name="Owned">Items the server already owns, paired with the real item id.</param>
/// <param name="NotOwned">Items that need a stub.</param>
public sealed record PartitionResult(IReadOnlyList<OwnedListItem> Owned, IReadOnlyList<ListItem> NotOwned);
