using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <inheritdoc />
public sealed class MultiVersionStubFileIoExecutor : IMultiVersionStubFileIoExecutor
{
    private const string StubFilePattern = "*.strm";

    private readonly ILogger<MultiVersionStubFileIoExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiVersionStubFileIoExecutor"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public MultiVersionStubFileIoExecutor(ILogger<MultiVersionStubFileIoExecutor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<ExistingVersionedFolder> ListExistingFolders(string stubRootPath)
    {
        if (!Directory.Exists(stubRootPath))
        {
            return [];
        }

        var result = new List<ExistingVersionedFolder>();
        foreach (var folderPath in Directory.EnumerateDirectories(stubRootPath, "*", SearchOption.TopDirectoryOnly))
        {
            if (!StubPath.IsUnderRoot(folderPath, stubRootPath))
            {
                continue;
            }

            var filesByRelativePath = new Dictionary<string, string>();
            foreach (var filePath in Directory.EnumerateFiles(folderPath, StubFilePattern, SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(folderPath, filePath);
                filesByRelativePath[relativePath] = File.ReadAllText(filePath);
            }

            result.Add(new ExistingVersionedFolder(Path.GetFileName(folderPath), filesByRelativePath));
        }

        return result;
    }

    /// <inheritdoc />
    public void Apply(MultiVersionStubReconcilePlan plan, string stubRootPath)
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

        foreach (var folderPlan in plan.FoldersToReconcile)
        {
            if (!TryResolveDirectChild(folderPlan.FolderName, stubRootPath, out var folderPath))
            {
                continue;
            }

            foreach (var relativePath in folderPlan.RelativePathsToDelete)
            {
                var filePath = Path.Combine(folderPath, relativePath);
                if (!StubPath.IsUnderRoot(filePath, stubRootPath))
                {
                    _logger.LogError("Refusing to delete a stub path outside the stub root: {Path}", filePath);
                    continue;
                }

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            foreach (var write in folderPlan.FilesToWrite)
            {
                var filePath = Path.Combine(folderPath, write.RelativePath);
                if (!StubPath.IsUnderRoot(filePath, stubRootPath))
                {
                    _logger.LogError("Refusing to write a stub path outside the stub root: {Path}", filePath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.WriteAllText(filePath, write.Content);
            }
        }
    }

    /// <summary>
    /// Resolves a title folder name to a path that is both directly under the stub root and passes
    /// <see cref="StubPath.IsUnderRoot"/> - the same guard <see cref="TvStubFileIoExecutor"/> uses,
    /// reused here via <see cref="TvStubFileNaming.IsSafeFolderName"/> rather than re-derived
    /// (docs/rename-tv-globalkey-plan.md's "single source a security-relevant check" rule; the
    /// method is generic despite its TV-named home).
    /// </summary>
    private bool TryResolveDirectChild(string folderName, string stubRootPath, out string folderPath)
    {
        folderPath = string.Empty;

        if (!TvStubFileNaming.IsSafeFolderName(folderName))
        {
            _logger.LogError("Refusing to touch a stub folder name that is not a direct child of the stub root: {FolderName}", folderName);
            return false;
        }

        var candidate = Path.Combine(stubRootPath, folderName);
        if (!StubPath.IsUnderRoot(candidate, stubRootPath))
        {
            _logger.LogError("Refusing to touch a stub path outside the stub root: {Path}", candidate);
            return false;
        }

        folderPath = candidate;
        return true;
    }
}
