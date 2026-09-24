using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.SeasonalContent.Collections;

/// <summary>
/// The result of diffing a BoxSet's desired membership against its current membership.
/// </summary>
/// <param name="ToAdd">Item ids to add via <c>ICollectionManager.AddToCollectionAsync</c>.</param>
/// <param name="ToRemove">Item ids to remove via <c>ICollectionManager.RemoveFromCollectionAsync</c>.</param>
public sealed record CollectionMembershipPlan(IReadOnlyList<Guid> ToAdd, IReadOnlyList<Guid> ToRemove);

/// <summary>
/// Pure diff between a BoxSet's desired members and its current members
/// (<c>BoxSet.GetLinkedChildren()</c> - see docs/decisions.md, "M4 API surface"), per
/// docs/implementation-plan.md §3.7.
/// </summary>
public static class CollectionMembershipDiff
{
    /// <summary>
    /// Builds the add/remove plan.
    /// </summary>
    /// <param name="desiredMemberIds">The item ids that should be members (owned + resolved stub items).</param>
    /// <param name="currentMemberIds">The BoxSet's current linked-children item ids.</param>
    /// <returns>The plan.</returns>
    public static CollectionMembershipPlan Build(IReadOnlyCollection<Guid> desiredMemberIds, IReadOnlyCollection<Guid> currentMemberIds)
    {
        var desired = new HashSet<Guid>(desiredMemberIds);
        var current = new HashSet<Guid>(currentMemberIds);

        var toAdd = desired.Where(id => !current.Contains(id)).ToList();
        var toRemove = current.Where(id => !desired.Contains(id)).ToList();

        return new CollectionMembershipPlan(toAdd, toRemove);
    }
}
