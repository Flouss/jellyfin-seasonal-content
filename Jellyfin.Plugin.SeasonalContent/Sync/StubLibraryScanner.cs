using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.Sync;

/// <inheritdoc />
public sealed class StubLibraryScanner : IStubLibraryScanner
{
    private readonly ILibraryManager _libraryManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<StubLibraryScanner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubLibraryScanner"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public StubLibraryScanner(ILibraryManager libraryManager, IFileSystem fileSystem, ILogger<StubLibraryScanner> logger)
    {
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ScanAsync(string stubRootPath, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var normalizedRoot = Normalize(stubRootPath);

        var matchingFolder = _libraryManager
            .GetVirtualFolders()
            .FirstOrDefault(vf => vf.Locations.Any(location => Normalize(location) == normalizedRoot));

        if (matchingFolder is null)
        {
            _logger.LogError(
                "No Jellyfin library is configured at the stub root '{StubRoot}'. Add it as a Movies-type library in Dashboard -> Libraries, then run sync again.",
                stubRootPath);
            return false;
        }

        // VirtualFolderInfo.ItemId is the library's own CollectionFolder (: Folder) item - the
        // node to call ValidateChildren on. FindByPath(location, isFolder: true) looks like the
        // obvious alternative (and is what docs/implementation-plan.md §3.2 originally described),
        // but verified live (2026-09-24, docs/decisions.md) NOT to work here: it only resolves an
        // already-indexed child item at that exact path, which doesn't exist yet for a library
        // that was empty at its first scan - exactly the state a brand-new stub library starts in.
        if (!Guid.TryParse(matchingFolder.ItemId, out var folderId) || _libraryManager.GetItemById<Folder>(folderId) is not Folder folder)
        {
            _logger.LogError("The stub library at '{StubRoot}' could not be resolved to a folder item (ItemId '{ItemId}').", stubRootPath, matchingFolder.ItemId);
            return false;
        }

        // The simple 2-arg ValidateChildren(progress, cancellationToken) overload does NOT pick up
        // new files on disk - confirmed live (2026-09-24, docs/decisions.md): 100 freshly-written
        // stubs stayed invisible to it, while the built-in "Scan Media Library" task found them
        // immediately. That task builds its own DirectoryService (a per-scan directory-listing
        // cache) and passes it through MetadataRefreshOptions; the simple overload apparently uses
        // a stale/empty one instead. Building our own the same way is the confirmed fix.
        var directoryService = new DirectoryService(_fileSystem);
        var refreshOptions = new MetadataRefreshOptions(directoryService) { MetadataRefreshMode = MetadataRefreshMode.Default };

        await folder.ValidateChildren(progress, refreshOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string Normalize(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/');
}
