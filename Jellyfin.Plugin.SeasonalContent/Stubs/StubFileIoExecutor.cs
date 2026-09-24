using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <inheritdoc />
public sealed class StubFileIoExecutor : IStubFileIoExecutor
{
    private const string StubFilePattern = "*.strm";

    private readonly ILogger<StubFileIoExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubFileIoExecutor"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public StubFileIoExecutor(ILogger<StubFileIoExecutor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<ExistingStubFile> ListExistingFiles(string stubRootPath)
    {
        if (!Directory.Exists(stubRootPath))
        {
            return [];
        }

        var result = new List<ExistingStubFile>();
        foreach (var filePath in Directory.EnumerateFiles(stubRootPath, StubFilePattern, SearchOption.TopDirectoryOnly))
        {
            if (!StubPath.IsUnderRoot(filePath, stubRootPath))
            {
                continue;
            }

            var content = File.ReadAllText(filePath);
            result.Add(new ExistingStubFile(Path.GetFileName(filePath), content));
        }

        return result;
    }

    /// <inheritdoc />
    public void Apply(StubReconcilePlan plan, string stubRootPath)
    {
        Directory.CreateDirectory(stubRootPath);

        foreach (var fileName in plan.FileNamesToDelete)
        {
            var path = Path.Combine(stubRootPath, fileName);
            if (!StubPath.IsUnderRoot(path, stubRootPath))
            {
                _logger.LogError("Refusing to delete a stub path outside the stub root: {Path}", path);
                continue;
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        foreach (var write in plan.FilesToWrite)
        {
            var path = Path.Combine(stubRootPath, write.FileName);
            if (!StubPath.IsUnderRoot(path, stubRootPath))
            {
                _logger.LogError("Refusing to write a stub path outside the stub root: {Path}", path);
                continue;
            }

            File.WriteAllText(path, write.Content);
        }
    }
}
