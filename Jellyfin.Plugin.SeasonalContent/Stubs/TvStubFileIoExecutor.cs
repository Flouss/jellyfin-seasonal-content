using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <inheritdoc />
public sealed class TvStubFileIoExecutor : ITvStubFileIoExecutor
{
    private const string SeasonFolderName = "Season 01";
    private const string StubFilePattern = "*.strm";

    private readonly ILogger<TvStubFileIoExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvStubFileIoExecutor"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public TvStubFileIoExecutor(ILogger<TvStubFileIoExecutor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<ExistingTvSeriesFolder> ListExistingSeriesFolders(string stubRootPath)
    {
        if (!Directory.Exists(stubRootPath))
        {
            return [];
        }

        var result = new List<ExistingTvSeriesFolder>();
        foreach (var seriesDir in Directory.EnumerateDirectories(stubRootPath, "*", SearchOption.TopDirectoryOnly))
        {
            if (!StubPath.IsUnderRoot(seriesDir, stubRootPath))
            {
                continue;
            }

            var seasonDir = Path.Combine(seriesDir, SeasonFolderName);
            string? episodeContent = null;
            if (Directory.Exists(seasonDir))
            {
                var episodeFile = Directory.EnumerateFiles(seasonDir, StubFilePattern, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (episodeFile is not null)
                {
                    episodeContent = File.ReadAllText(episodeFile);
                }
            }

            result.Add(new ExistingTvSeriesFolder(Path.GetFileName(seriesDir), episodeContent));
        }

        return result;
    }

    /// <inheritdoc />
    public void Apply(TvStubReconcilePlan plan, string stubRootPath)
    {
        Directory.CreateDirectory(stubRootPath);

        foreach (var folderName in plan.FolderNamesToDelete)
        {
            if (!TryResolveDirectChild(folderName, stubRootPath, out var folderPath))
            {
                continue;
            }

            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, recursive: true);
            }
        }

        foreach (var write in plan.SeriesToWrite)
        {
            if (!TryResolveDirectChild(write.FolderName, stubRootPath, out var folderPath))
            {
                continue;
            }

            var episodePath = Path.Combine(folderPath, write.EpisodeRelativePath);
            if (!StubPath.IsUnderRoot(episodePath, stubRootPath))
            {
                _logger.LogError("Refusing to write a TV stub path outside the stub root: {Path}", episodePath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(episodePath)!);
            File.WriteAllText(episodePath, write.Content);
        }
    }

    /// <summary>
    /// Resolves a series folder name to a path that is both directly under the stub root (no
    /// nested path via an embedded separator) and passes the general <see cref="StubPath.IsUnderRoot"/>
    /// check - the guard a <b>recursive</b> delete needs beyond the single-file checks the movie
    /// executor uses (docs/rename-tv-globalkey-plan.md).
    /// </summary>
    private bool TryResolveDirectChild(string folderName, string stubRootPath, out string folderPath)
    {
        folderPath = string.Empty;

        if (!TvStubFileNaming.IsSafeFolderName(folderName))
        {
            _logger.LogError("Refusing to touch a TV stub folder name that is not a direct child of the stub root: {FolderName}", folderName);
            return false;
        }

        var candidate = Path.Combine(stubRootPath, folderName);
        if (!StubPath.IsUnderRoot(candidate, stubRootPath))
        {
            _logger.LogError("Refusing to touch a TV stub path outside the stub root: {Path}", candidate);
            return false;
        }

        folderPath = candidate;
        return true;
    }
}
