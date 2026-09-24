using System;
using System.Collections.Generic;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.Ownership;

/// <summary>
/// Builds the "is this (kind, TMDb id) already owned" lookup once per sync, excluding stub-root
/// items. Fixes the P0 defect confirmed in the POC (docs/implementation-plan.md §3.3): the POC's
/// ownership check was never wired into its seasonal path at all, so an owned title still got a
/// duplicate stub. Here, the exclusion is structural - a stub can never appear in this index -
/// rather than depending on every caller remembering to check first.
/// </summary>
/// <remarks>
/// Keyed on <c>(MediaKind, TmdbId)</c>, not bare <c>TmdbId</c>: TMDb ids are only unique within one
/// kind, so a movie and a show that happen to share a numeric id would otherwise collide (a movie's
/// ownership would satisfy a same-numbered show's, or vice versa) - see
/// docs/rename-tv-globalkey-plan.md's "correctness constraint".
/// </remarks>
public static class OwnedItemIndex
{
    /// <summary>
    /// Builds a (kind, TMDb id) to item id lookup from every real movie/series the library reports.
    /// </summary>
    /// <param name="items">Every movie/series the library reports, including stubs.</param>
    /// <param name="stubRootPaths">Every stub root; an item under any of them is not "owned".</param>
    /// <returns>A lookup of (kind, TMDb id) to item id, for real (non-stub) items only.</returns>
    public static IReadOnlyDictionary<(MediaKind Kind, int TmdbId), Guid> Build(IEnumerable<CatalogedItem> items, IReadOnlyList<string> stubRootPaths)
    {
        var index = new Dictionary<(MediaKind, int), Guid>();

        foreach (var item in items)
        {
            var isStub = false;
            foreach (var stubRootPath in stubRootPaths)
            {
                if (StubPath.IsUnderRoot(item.Path, stubRootPath))
                {
                    isStub = true;
                    break;
                }
            }

            if (isStub)
            {
                continue;
            }

            // Same (kind, TMDb id) in more than one real library: any one of them is fine to
            // reference (docs/implementation-plan.md §3.3 edge case) - last one wins, arbitrarily.
            index[(item.Kind, item.TmdbId)] = item.ItemId;
        }

        return index;
    }
}
