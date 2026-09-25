using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.Setup;

/// <inheritdoc />
public sealed class LibrarySetupService : ILibrarySetupService
{
    private const string StubLibraryName = "Smarter Collections (Movies)";
    private const string TvStubLibraryName = "Smarter Collections (TV Shows)";
    private const string CollectionsLibraryName = "Collections";

    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<LibrarySetupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibrarySetupService"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public LibrarySetupService(ILibraryManager libraryManager, ILogger<LibrarySetupService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LibrarySetupResult> SetupAsync(CancellationToken cancellationToken)
    {
        var existing = _libraryManager.GetVirtualFolders();

        var stubResult = await EnsureStubLibraryAsync(
                existing,
                StubPath.GetRootPath(),
                StubLibraryName,
                CollectionTypeOptions.movies,
                "Movie")
            .ConfigureAwait(false);

        // Re-read after the movie step: AddVirtualFolder mutates server state that
        // GetVirtualFolders() reflects, and the TV check below must not miss a library the movie
        // step (or an earlier partial run) just created.
        existing = _libraryManager.GetVirtualFolders();
        var tvStubResult = await EnsureStubLibraryAsync(
                existing,
                StubPath.GetTvRootPath(),
                TvStubLibraryName,
                CollectionTypeOptions.tvshows,
                "Series")
            .ConfigureAwait(false);

        existing = _libraryManager.GetVirtualFolders();
        LibrarySetupStepResult collectionsResult;
        if (LibrarySetupPlan.CollectionsLibraryExists(existing))
        {
            collectionsResult = new LibrarySetupStepResult(false, "Collections library already exists.");
        }
        else
        {
            await _libraryManager.AddVirtualFolder(CollectionsLibraryName, CollectionTypeOptions.boxsets, new LibraryOptions(), refreshLibrary: true).ConfigureAwait(false);
            _logger.LogInformation("Created Collections library {Name}.", CollectionsLibraryName);
            collectionsResult = new LibrarySetupStepResult(true, "Collections library created.");
        }

        return new LibrarySetupResult(stubResult, tvStubResult, collectionsResult);
    }

    private async Task<LibrarySetupStepResult> EnsureStubLibraryAsync(
        IEnumerable<VirtualFolderInfo> existing,
        string stubRootPath,
        string libraryName,
        CollectionTypeOptions collectionType,
        string itemTypeOptionsType)
    {
        if (LibrarySetupPlan.StubLibraryExists(existing, stubRootPath))
        {
            return new LibrarySetupStepResult(false, $"{libraryName} already exists.");
        }

        // AddVirtualFolder is not confirmed to create the target directory itself - create it
        // first so the call never has to guess whether a not-yet-existing path is tolerated.
        Directory.CreateDirectory(stubRootPath);

        var options = new LibraryOptions
        {
#pragma warning disable CS0618 // Obsolete: "disable remote providers in TypeOptions instead" -
            // but this flag still defaults to false and still gates remote metadata fetching
            // in 12.1.0 (confirmed: the real, working "Seasonal Content" library's own saved
            // options.xml has it explicitly true). TypeOptions.MetadataFetchers alone isn't
            // sufficient without this.
            EnableInternetProviders = true,
#pragma warning restore CS0618
            PathInfos = [new MediaPathInfo(stubRootPath)],
            // ImageFetchers must be set explicitly too - leaving it unset persists as an empty
            // list (confirmed live 2026-09-25: the TV stub library's saved options.xml had
            // <ImageFetchers /> with nothing in it), which means "no image provider enabled for
            // this type", not "use the defaults". Without this, TheMovieDb still supplies
            // metadata (title/overview) but Jellyfin never downloads any poster/backdrop art.
            TypeOptions =
            [
                new TypeOptions
                {
                    Type = itemTypeOptionsType,
                    MetadataFetchers = ["TheMovieDb"],
                    ImageFetchers = ["TheMovieDb"]
                }
            ]
        };
        await _libraryManager.AddVirtualFolder(libraryName, collectionType, options, refreshLibrary: true).ConfigureAwait(false);
        _logger.LogInformation("Created stub library {Name} at {Path}.", libraryName, stubRootPath);
        return new LibrarySetupStepResult(true, $"{libraryName} created.");
    }
}
