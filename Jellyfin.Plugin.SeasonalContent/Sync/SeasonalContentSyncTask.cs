using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Collections;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Jellyfin.Plugin.SeasonalContent.Stubs;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.Sync;

/// <summary>
/// "Sync seasonal lists" - the full M4 pipeline: fetch every enabled list -&gt; partition -&gt;
/// reconcile stubs -&gt; scan the stub library -&gt; reconcile each list's BoxSet, per
/// docs/implementation-plan.md §3.2/§3.3/§3.7 and the M4 milestone (§7).
/// </summary>
public sealed class SeasonalContentSyncTask : IScheduledTask
{
    private readonly IListSource _listSource;
    private readonly IMovieCatalog _movieCatalog;
    private readonly IStubFileIoExecutor _stubFileIoExecutor;
    private readonly IStubLibraryScanner _stubLibraryScanner;
    private readonly ICollectionReconciler _collectionReconciler;
    private readonly ILogger<SeasonalContentSyncTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonalContentSyncTask"/> class.
    /// </summary>
    /// <param name="listSource">Instance of the <see cref="IListSource"/> interface.</param>
    /// <param name="movieCatalog">Instance of the <see cref="IMovieCatalog"/> interface.</param>
    /// <param name="stubFileIoExecutor">Instance of the <see cref="IStubFileIoExecutor"/> interface.</param>
    /// <param name="stubLibraryScanner">Instance of the <see cref="IStubLibraryScanner"/> interface.</param>
    /// <param name="collectionReconciler">Instance of the <see cref="ICollectionReconciler"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public SeasonalContentSyncTask(
        IListSource listSource,
        IMovieCatalog movieCatalog,
        IStubFileIoExecutor stubFileIoExecutor,
        IStubLibraryScanner stubLibraryScanner,
        ICollectionReconciler collectionReconciler,
        ILogger<SeasonalContentSyncTask> logger)
    {
        _listSource = listSource;
        _movieCatalog = movieCatalog;
        _stubFileIoExecutor = stubFileIoExecutor;
        _stubLibraryScanner = stubLibraryScanner;
        _collectionReconciler = collectionReconciler;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Sync seasonal lists";

    /// <inheritdoc />
    public string Key => "SeasonalContentSync";

    /// <inheritdoc />
    public string Description => "Fetches every enabled seasonal list, reconciles stub files, scans the stub library, and reconciles each list's collection.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger },
        new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(6).Ticks }
    ];

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;

        if (string.IsNullOrWhiteSpace(config.StubBaseUrl))
        {
            _logger.LogError("StubBaseUrl is not configured. Sync aborted before touching any stub or collection.");
            return;
        }

        Uri dummyUrl;
        try
        {
            dummyUrl = new Uri(new Uri(config.StubBaseUrl, UriKind.Absolute), "SeasonalContent/Dummy");
        }
        catch (UriFormatException)
        {
            _logger.LogError("StubBaseUrl '{StubBaseUrl}' is not a valid absolute URL. Sync aborted.", config.StubBaseUrl);
            return;
        }

        var expectedContent = dummyUrl.ToString();
        var stubRootPath = Path.Combine(Plugin.Instance!.DataFolderPath, "movies_seasonal");
        var enabledLists = config.Lists.Where(l => l.Enabled).ToList();

        // 1. Fetch + partition every enabled list. A failure here is isolated per list: it's
        // logged and skipped, and that list contributes nothing to this sync's desired stub set or
        // collection reconcile - its previously-written stubs and collection are left untouched
        // (docs/m4-plan.md "per-list failure isolation").
        var ownedIndex = OwnedMovieIndex.Build(_movieCatalog.GetAllMoviesWithTmdbId(), stubRootPath);
        var succeeded = new List<(SeasonalListConfig Config, PartitionResult Partition)>();

        foreach (var listConfig in enabledLists)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var request = new ListSourceRequest(listConfig.Id.ToString(), listConfig.Username, listConfig.Slug, listConfig.ApiKey, listConfig.Limit);
                var items = await _listSource.GetItemsAsync(request, cancellationToken).ConfigureAwait(false);
                succeeded.Add((listConfig, ListPartitioner.Partition(items, ownedIndex)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Failed to sync list '{DisplayName}' ({ListId}). Its existing stubs and collection are left untouched this sync.",
                    listConfig.DisplayName,
                    listConfig.Id);
            }
        }

        progress.Report(25);

        // 2. Reconcile stub files, over the union of every list that fetched successfully.
        var desiredItems = DesiredStubSet.Build(succeeded.SelectMany(s => s.Partition.NotOwned));
        var existingFiles = _stubFileIoExecutor.ListExistingFiles(stubRootPath);
        var plan = StubReconciler.BuildPlan(desiredItems, existingFiles, expectedContent);
        _stubFileIoExecutor.Apply(plan, stubRootPath);

        _logger.LogInformation("Stub reconcile: {Written} written, {Deleted} deleted.", plan.FilesToWrite.Count, plan.FileNamesToDelete.Count);
        progress.Report(50);

        // 3. Scan the stub library so newly-written stubs become real BaseItems.
        var scanProgress = new Progress<double>(p => progress.Report(50.0 + (p * 0.3)));
        var scanned = await _stubLibraryScanner.ScanAsync(stubRootPath, scanProgress, cancellationToken).ConfigureAwait(false);
        if (!scanned)
        {
            _logger.LogWarning("Stub library scan did not run; newly-created stubs will not resolve to real items until the library exists and a scan runs.");
        }

        progress.Report(80);

        // 4. One combined (non-excluding) TmdbId -> ItemId index, covering real items and the
        // stubs the scan above just created, to resolve every list's collection members.
        var allMoviesIndex = new Dictionary<int, Guid>();
        foreach (var movie in _movieCatalog.GetAllMoviesWithTmdbId())
        {
            allMoviesIndex[movie.TmdbId] = movie.ItemId;
        }

        // 5. Reconcile each successfully-fetched list's collection.
        foreach (var (listConfig, partition) in succeeded)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var desiredMemberIds = new HashSet<Guid>(partition.Owned.Select(o => o.OwnedItemId));

            var unresolved = 0;
            foreach (var item in partition.NotOwned)
            {
                if (allMoviesIndex.TryGetValue(item.TmdbId, out var itemId))
                {
                    desiredMemberIds.Add(itemId);
                }
                else
                {
                    unresolved++;
                }
            }

            if (unresolved > 0)
            {
                _logger.LogInformation(
                    "List '{DisplayName}': {Unresolved} stub item(s) not yet resolvable in the library (metadata not populated yet); will retry next sync.",
                    listConfig.DisplayName,
                    unresolved);
            }

            // Always assign, never only-when-HasValue: null is itself meaningful here (no
            // currently-valid collection), not "leave whatever was there before" - otherwise a
            // stale id left over from a BoxSet deleted outside the plugin would never clear while
            // this list has zero resolvable members, and every future sync would keep re-logging
            // CollectionReconciler's "no longer resolves" warning for it.
            listConfig.CollectionId = await _collectionReconciler.ReconcileAsync(listConfig, desiredMemberIds, cancellationToken).ConfigureAwait(false);
        }

        // 6. Disabled lists (still present in config) get their collection removed. Deleted lists
        // (no longer in config at all) are handled immediately at save time instead - see
        // Api/ListsController.
        if (config.RemoveCollectionWhenListDisabled)
        {
            foreach (var listConfig in config.Lists.Where(l => !l.Enabled && l.CollectionId.HasValue))
            {
                await _collectionReconciler.RemoveCollectionAsync(listConfig.CollectionId!.Value, cancellationToken).ConfigureAwait(false);
                listConfig.CollectionId = null;
            }
        }

        Plugin.Instance!.SaveConfiguration();
        progress.Report(100);
    }
}
