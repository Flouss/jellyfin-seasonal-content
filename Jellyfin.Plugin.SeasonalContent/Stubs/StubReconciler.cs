using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <summary>
/// One file already on disk in the stub root, as found by a directory listing.
/// </summary>
/// <param name="FileName">The file's name (no directory part).</param>
/// <param name="Content">The file's current text content.</param>
public sealed record ExistingStubFile(string FileName, string Content);

/// <summary>
/// One file the reconcile plan wants written (created or rewritten - the executor doesn't need to
/// know which).
/// </summary>
/// <param name="FileName">The file's name (no directory part).</param>
/// <param name="Content">The content to write.</param>
public sealed record StubFileWrite(string FileName, string Content);

/// <summary>
/// The result of reconciling desired stubs against what's on disk: what to delete, what to
/// (over)write. Deliberately has no "rewrite" category separate from "write" - the executor
/// doesn't care whether a file is new or being corrected, only that its content must match.
/// </summary>
/// <param name="FileNamesToDelete">Existing files no longer desired.</param>
/// <param name="FilesToWrite">Files to create or overwrite so their content is correct.</param>
public sealed record StubReconcilePlan(IReadOnlyList<string> FileNamesToDelete, IReadOnlyList<StubFileWrite> FilesToWrite);

/// <summary>
/// Pure diff between the desired stub set and what's actually on disk, per
/// docs/implementation-plan.md §3.2. No "previous base URL" is tracked: every existing file's
/// actual content is compared against freshly-computed expected content on every sync, which
/// self-heals a base-URL change, a partially-failed previous write, or a manual edit uniformly.
/// </summary>
public static class StubReconciler
{
    /// <summary>
    /// Builds the reconcile plan.
    /// </summary>
    /// <param name="desiredItems">The deduplicated desired stub set (see <see cref="DesiredStubSet"/>).</param>
    /// <param name="existingFiles">Every file currently in the stub root.</param>
    /// <param name="expectedContent">The content every desired stub's file should currently hold.</param>
    /// <returns>The plan.</returns>
    public static StubReconcilePlan BuildPlan(
        IReadOnlyList<ListItem> desiredItems,
        IReadOnlyList<ExistingStubFile> existingFiles,
        string expectedContent)
    {
        var existingByName = existingFiles.ToDictionary(f => f.FileName, f => f.Content);

        var desiredFileNames = new HashSet<string>();
        var filesToWrite = new List<StubFileWrite>();

        foreach (var item in desiredItems)
        {
            var fileName = StubFileNaming.BuildFileName(item.Title, item.Year, item.TmdbId);
            desiredFileNames.Add(fileName);

            if (!existingByName.TryGetValue(fileName, out var currentContent) || currentContent != expectedContent)
            {
                filesToWrite.Add(new StubFileWrite(fileName, expectedContent));
            }
        }

        var toDelete = existingFiles
            .Select(f => f.FileName)
            .Where(name => !desiredFileNames.Contains(name))
            .ToList();

        return new StubReconcilePlan(toDelete, filesToWrite);
    }
}
