using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.SeasonalContent.Lists;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <summary>
/// One series folder already on disk in the TV stub root, as found by a directory listing.
/// </summary>
/// <param name="FolderName">The series folder's name (no path part).</param>
/// <param name="EpisodeContent">The current content of the folder's known dummy episode file, or
/// <see langword="null"/> if that file doesn't exist (e.g. a previous write was interrupted).</param>
public sealed record ExistingTvSeriesFolder(string FolderName, string? EpisodeContent);

/// <summary>
/// One series the reconcile plan wants a stub for (created or repaired - the executor doesn't need
/// to know which).
/// </summary>
/// <param name="FolderName">The series folder's name (no path part).</param>
/// <param name="EpisodeRelativePath">The dummy episode's path, relative to the series folder.</param>
/// <param name="Content">The content the dummy episode file should hold.</param>
public sealed record TvSeriesWrite(string FolderName, string EpisodeRelativePath, string Content);

/// <summary>
/// The result of reconciling desired TV stubs against what's on disk: which series folders to
/// delete (recursively - a whole show, not a single file), which to (re)write.
/// </summary>
/// <param name="FolderNamesToDelete">Existing series folders no longer desired.</param>
/// <param name="SeriesToWrite">Series whose stub needs creating or repairing.</param>
public sealed record TvStubReconcilePlan(IReadOnlyList<string> FolderNamesToDelete, IReadOnlyList<TvSeriesWrite> SeriesToWrite);

/// <summary>
/// Pure diff between the desired TV stub set and what's actually on disk - the TV counterpart of
/// <see cref="StubReconciler"/>, kept as a separate type rather than a shared abstraction because
/// the physical shape genuinely differs (one flat file per movie vs. one folder-with-an-episode per
/// series); see docs/rename-tv-globalkey-plan.md §3.
/// </summary>
public static class TvStubReconciler
{
    /// <summary>
    /// Builds the reconcile plan.
    /// </summary>
    /// <param name="desiredItems">The deduplicated desired stub set (TV items only).</param>
    /// <param name="existingFolders">Every series folder currently in the TV stub root.</param>
    /// <param name="expectedContent">The content every desired series' dummy episode should hold.</param>
    /// <returns>The plan.</returns>
    public static TvStubReconcilePlan BuildPlan(
        IReadOnlyList<ListItem> desiredItems,
        IReadOnlyList<ExistingTvSeriesFolder> existingFolders,
        string expectedContent)
    {
        var existingByName = existingFolders.ToDictionary(f => f.FolderName, f => f.EpisodeContent);

        var desiredFolderNames = new HashSet<string>();
        var seriesToWrite = new List<TvSeriesWrite>();

        foreach (var item in desiredItems)
        {
            var folderName = TvStubFileNaming.BuildSeriesFolderName(item.Title, item.Year, item.TmdbId);
            desiredFolderNames.Add(folderName);

            if (!existingByName.TryGetValue(folderName, out var currentContent) || currentContent != expectedContent)
            {
                var episodeRelativePath = TvStubFileNaming.BuildEpisodeRelativePath(item.Title, item.TmdbId);
                seriesToWrite.Add(new TvSeriesWrite(folderName, episodeRelativePath, expectedContent));
            }
        }

        var toDelete = existingFolders
            .Select(f => f.FolderName)
            .Where(name => !desiredFolderNames.Contains(name))
            .ToList();

        return new TvStubReconcilePlan(toDelete, seriesToWrite);
    }
}
