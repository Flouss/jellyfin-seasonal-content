using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.Collections;

/// <inheritdoc />
public sealed class CollectionReconciler : ICollectionReconciler
{
    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;
    private readonly ILogger<CollectionReconciler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionReconciler"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="collectionManager">Instance of the <see cref="ICollectionManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public CollectionReconciler(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<CollectionReconciler> logger)
    {
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid?> ReconcileAsync(SeasonalListConfig listConfig, IReadOnlyCollection<Guid> desiredMemberIds, CancellationToken cancellationToken)
    {
        var existingBoxSet = listConfig.CollectionId is Guid existingId
            ? _libraryManager.GetItemById<BoxSet>(existingId)
            : null;

        if (existingBoxSet is not null)
        {
            var currentMemberIds = existingBoxSet.GetLinkedChildren().Select(i => i.Id).ToList();
            var diff = CollectionMembershipDiff.Build(desiredMemberIds, currentMemberIds);

            if (diff.ToAdd.Count > 0)
            {
                await _collectionManager.AddToCollectionAsync(existingBoxSet.Id, diff.ToAdd).ConfigureAwait(false);
            }

            if (diff.ToRemove.Count > 0)
            {
                await _collectionManager.RemoveFromCollectionAsync(existingBoxSet.Id, diff.ToRemove).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "List '{DisplayName}': collection reconciled, +{Added}/-{Removed} member(s).",
                listConfig.DisplayName,
                diff.ToAdd.Count,
                diff.ToRemove.Count);

            return existingBoxSet.Id;
        }

        if (listConfig.CollectionId.HasValue)
        {
            _logger.LogWarning(
                "List '{DisplayName}': stored collection id {CollectionId} no longer resolves to a BoxSet (deleted outside the plugin?). A new one will be created.",
                listConfig.DisplayName,
                listConfig.CollectionId);
        }

        if (desiredMemberIds.Count == 0)
        {
            // Nothing resolvable yet (e.g. metadata not populated this sync) - don't create an
            // empty collection just to fill it in next sync.
            return null;
        }

        var options = new CollectionCreationOptions
        {
            Name = listConfig.DisplayName,
            IsLocked = false,
            ProviderIds = new Dictionary<string, string>(),
            ItemIdList = desiredMemberIds.Select(id => id.ToString("N")).ToList(),
            UserIds = []
        };

        var created = await _collectionManager.CreateCollectionAsync(options).ConfigureAwait(false);

        _logger.LogInformation("List '{DisplayName}': created collection {CollectionId} with {Count} member(s).", listConfig.DisplayName, created.Id, desiredMemberIds.Count);

        return created.Id;
    }

    /// <inheritdoc />
    public Task RemoveCollectionAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById<BoxSet>(collectionId);
        if (item is null)
        {
            return Task.CompletedTask;
        }

        _libraryManager.DeleteItem(item, new DeleteOptions { DeleteFileLocation = false });
        return Task.CompletedTask;
    }
}
