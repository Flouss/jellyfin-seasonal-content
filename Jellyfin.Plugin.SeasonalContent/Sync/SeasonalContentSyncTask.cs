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
/// "Sync Smarter Collections lists" - the full pipeline: fetch every enabled list -&gt; partition
/// (movies and TV shows together) -&gt; reconcile both stub roots -&gt; scan both stub libraries
/// -&gt; reconcile each list's BoxSet, per docs/implementation-plan.md §3.2/§3.3/§3.7 and
/// docs/rename-tv-globalkey-plan.md §3.
/// </summary>
public sealed class SeasonalContentSyncTask : IScheduledTask
{
    private readonly IListSource _listSource;
    private readonly IMediaCatalog _mediaCatalog;
    private readonly IStubFileIoExecutor _stubFileIoExecutor;
    private readonly ITvStubFileIoExecutor _tvStubFileIoExecutor;
    private readonly IStubLibraryScanner _stubLibraryScanner;
    private readonly ICollectionReconciler _collectionReconciler;
    private readonly ILogger<SeasonalContentSyncTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonalContentSyncTask"/> class.
    /// </summary>
    /// <param name="listSource">Instance of the <see cref="IListSource"/> interface.</param>
    /// <param name="mediaCatalog">Instance of the <see cref="IMediaCatalog"/> interface.</param>
    /// <param name="stubFileIoExecutor">Instance of the <see cref="IStubFileIoExecutor"/> interface.</param>
    /// <param name="tvStubFileIoExecutor">Instance of the <see cref="ITvStubFileIoExecutor"/> interface.</param>
    /// <param name="stubLibraryScanner">Instance of the <see cref="IStubLibraryScanner"/> interface.</param>
    /// <param name="collectionReconciler">Instance of the <see cref="ICollectionReconciler"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public SeasonalContentSyncTask(
        IListSource listSource,
        IMediaCatalog mediaCatalog,
        IStubFileIoExecutor stubFileIoExecutor,
        ITvStubFileIoExecutor tvStubFileIoExecutor,
        IStubLibraryScanner stubLibraryScanner,
        ICollectionReconciler collectionReconciler,
        ILogger<SeasonalContentSyncTask> logger)
    {
        _listSource = listSource;
        _mediaCatalog = mediaCatalog;
        _stubFileIoExecutor = stubFileIoExecutor;
        _tvStubFileIoExecutor = tvStubFileIoExecutor;
        _stubLibraryScanner = stubLibraryScanner;
        _collectionReconciler = collectionReconciler;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Sync Smarter Collections lists";

    /// <inheritdoc />
    public string Key => "SeasonalContentSync";

    /// <inheritdoc />
    public string Description => "Fetches every enabled Smarter Collections list, reconciles stub files, scans the stub libraries, and reconciles each list's collection.";

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

        if (string.IsNullOrWhiteSpace(config.MdbListApiKey))
        {
            _logger.LogError("MdbListApiKey is not configured. Sync aborted before touching any stub or collection.");
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
        var stubRootPath = StubPath.GetRootPath();
        var tvStubRootPath = StubPath.GetTvRootPath();
        var enabledLists = config.Lists.Where(l => l.Enabled).ToList();

        // 1. Fetch + partition every enabled list (movies and TV shows together - MDBList lists
        // commonly carry both). A failure here is isolated per list: it's logged and skipped, and
        // that list contributes nothing to this sync's desired stub sets or collection reconcile -
        // its previously-written stubs and collection are left untouched (docs/m4-plan.md "per-list
        // failure isolation").
        var ownedIndex = OwnedItemIndex.Build(_mediaCatalog.GetAllItemsWithTmdbId(), [stubRootPath, tvStubRootPath]);
        var succeeded = new List<(SeasonalListConfig Config, PartitionResult Partition)>();

        foreach (var listConfig in enabledLists)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var request = new ListSourceRequest(listConfig.Id.ToString(), listConfig.Username, listConfig.Slug, config.MdbListApiKey, listConfig.Limit);
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

        progress.Report(20);

        var notOwnedItems = succeeded.SelectMany(s => s.Partition.NotOwned).ToList();

        // 2. Reconcile movie stub files, over the union of every list that fetched successfully.
        var desiredMovieItems = DesiredStubSet.Build(notOwnedItems.Where(i => i.Kind == MediaKind.Movie));
        var existingMovieFiles = _stubFileIoExecutor.ListExistingFiles(stubRootPath);
        var moviePlan = StubReconciler.BuildPlan(desiredMovieItems, existingMovieFiles, expectedContent);
        _stubFileIoExecutor.Apply(moviePlan, stubRootPath);
        _logger.LogInformation("Movie stub reconcile: {Written} written, {Deleted} deleted.", moviePlan.FilesToWrite.Count, moviePlan.FileNamesToDelete.Count);

        // 3. Reconcile TV stub folders the same way.
        var desiredShowItems = DesiredStubSet.Build(notOwnedItems.Where(i => i.Kind == MediaKind.Series));
        var existingShowFolders = _tvStubFileIoExecutor.ListExistingSeriesFolders(tvStubRootPath);
        var tvPlan = TvStubReconciler.BuildPlan(desiredShowItems, existingShowFolders, expectedContent);
        _tvStubFileIoExecutor.Apply(tvPlan, tvStubRootPath);
        _logger.LogInformation("TV stub reconcile: {Written} written, {Deleted} deleted.", tvPlan.SeriesToWrite.Count, tvPlan.FolderNamesToDelete.Count);

        progress.Report(45);

        // 4. Scan both stub libraries so newly-written stubs become real BaseItems. Each is
        // independently guarded - a failed TV scan must never prevent the movie scan (already run
        // above it) from having happened, or vice versa.
        var scannedMovies = await TryScanAsync(stubRootPath, new Progress<double>(p => progress.Report(45.0 + (p * 0.2))), cancellationToken).ConfigureAwait(false);
        if (!scannedMovies)
        {
            _logger.LogWarning("Movie stub library scan did not run; newly-created stubs will not resolve to real items until the library exists and a scan runs.");
        }

        var scannedShows = await TryScanAsync(tvStubRootPath, new Progress<double>(p => progress.Report(65.0 + (p * 0.2))), cancellationToken).ConfigureAwait(false);
        if (!scannedShows)
        {
            _logger.LogWarning("TV stub library scan did not run; newly-created stubs will not resolve to real items until the library exists and a scan runs.");
        }

        progress.Report(85);

        // 5. One combined (kind, TmdbId) -> ItemId index, covering real items and the stubs the
        // scans above just created, to resolve every list's collection members.
        var allItemsIndex = new Dictionary<(MediaKind Kind, int TmdbId), Guid>();
        foreach (var item in _mediaCatalog.GetAllItemsWithTmdbId())
        {
            allItemsIndex[(item.Kind, item.TmdbId)] = item.ItemId;
        }

        // 6. Reconcile each successfully-fetched list's collection. A single list's BoxSet can
        // contain both movie and show members - ICollectionManager operates on bare item ids with
        // no kind-specific logic.
        foreach (var (listConfig, partition) in succeeded)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var desiredMemberIds = new HashSet<Guid>(partition.Owned.Select(o => o.OwnedItemId));

            var unresolved = 0;
            foreach (var item in partition.NotOwned)
            {
                if (allItemsIndex.TryGetValue((item.Kind, item.TmdbId), out var itemId))
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

        // 7. Disabled lists (still present in config) get their collection removed. Deleted lists
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

    private async Task<bool> TryScanAsync(string stubRootPath, IProgress<double> scanProgress, CancellationToken cancellationToken)
    {
        try
        {
            return await _stubLibraryScanner.ScanAsync(stubRootPath, scanProgress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Stub library scan at '{StubRoot}' failed unexpectedly.", stubRootPath);
            return false;
        }
    }
}
