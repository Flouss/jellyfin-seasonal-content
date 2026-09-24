using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Configuration;

namespace Jellyfin.Plugin.SeasonalContent.Collections;

/// <summary>
/// Creates and reconciles one list's BoxSet via <c>ICollectionManager</c>, per
/// docs/implementation-plan.md §3.7. A thin IO shell, verified live (§8), not unit tested - the
/// membership diff itself is <see cref="CollectionMembershipDiff"/>, which is unit tested.
/// </summary>
public interface ICollectionReconciler
{
    /// <summary>
    /// Creates the list's BoxSet if it doesn't exist yet, or reconciles its membership if it does.
    /// Never adopts or modifies a collection the plugin didn't create - only ever acts on the id
    /// stored in <see cref="SeasonalListConfig.CollectionId"/>.
    /// </summary>
    /// <param name="listConfig">The list. Its <see cref="SeasonalListConfig.CollectionId"/> is read, not written - the caller persists the returned id.</param>
    /// <param name="desiredMemberIds">Every item id that should be a member: owned items plus resolved stub items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection's id (existing or newly created), or <see langword="null"/> if there
    /// was nothing to create yet (no resolvable members this sync).</returns>
    Task<Guid?> ReconcileAsync(SeasonalListConfig listConfig, IReadOnlyCollection<Guid> desiredMemberIds, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a BoxSet the plugin previously created (docs/implementation-plan.md §3.7: disabled
    /// or deleted lists remove their collection).
    /// </summary>
    /// <param name="collectionId">The collection to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the removal attempt is done.</returns>
    Task RemoveCollectionAsync(Guid collectionId, CancellationToken cancellationToken);
}
