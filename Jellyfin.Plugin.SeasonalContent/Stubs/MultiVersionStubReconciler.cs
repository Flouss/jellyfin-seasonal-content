using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.RequestProfiles;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <summary>
/// One title folder already on disk in a multi-version stub root, as found by a directory listing.
/// </summary>
/// <param name="FolderName">The title folder's name (no path part).</param>
/// <param name="ExistingFilesByRelativePath">Every stub file found under the folder, keyed by its
/// path relative to the folder (e.g. <c>"Title - 4K.strm"</c> for movies, <c>"Season 01/Title S01E01
/// [tmdbid-X] - 4K.strm"</c> for TV), with its current content.</param>
public sealed record ExistingVersionedFolder(string FolderName, IReadOnlyDictionary<string, string> ExistingFilesByRelativePath);

/// <summary>
/// One version file the reconcile plan wants written (created or rewritten) inside a title folder.
/// </summary>
/// <param name="RelativePath">The file's path relative to the title folder.</param>
/// <param name="Content">The content to write.</param>
public sealed record VersionFileWrite(string RelativePath, string Content);

/// <summary>
/// The reconcile result for one title folder that either already exists, should exist, or both -
/// never emitted for a folder that is neither desired nor on disk.
/// </summary>
/// <param name="FolderName">The title folder's name (no path part).</param>
/// <param name="RelativePathsToDelete">Version files in this folder no longer desired (e.g. a
/// profile the admin removed) - the folder itself is kept, only these files go.</param>
/// <param name="FilesToWrite">Version files to create or overwrite so their content is correct.</param>
public sealed record VersionedFolderPlan(string FolderName, IReadOnlyList<string> RelativePathsToDelete, IReadOnlyList<VersionFileWrite> FilesToWrite);

/// <summary>
/// The result of reconciling the desired multi-version stub set against what's on disk.
/// </summary>
/// <param name="FolderNamesToDelete">Existing title folders no longer desired at all - deleted
/// recursively (docs/rename-tv-globalkey-plan.md's whole-folder delete pattern).</param>
/// <param name="FoldersToReconcile">Title folders needing at least one file added or removed.</param>
public sealed record MultiVersionStubReconcilePlan(IReadOnlyList<string> FolderNamesToDelete, IReadOnlyList<VersionedFolderPlan> FoldersToReconcile);

/// <summary>
/// Pure diff between a desired multi-version stub set and what's on disk - the shared reconciler
/// behind both the movie and TV "quality version picker" layouts (docs/decisions.md "M5a spike
/// finding"). Unlike <see cref="StubReconciler"/>/<see cref="TvStubReconciler"/> (exactly one file
/// per title), a title folder here holds one file per <see cref="ResolvedRequestProfile"/> - the
/// same profile set for every title in one call, since the set of offered versions is a per-kind
/// admin setting, not a per-title one. Folder/file-path shape (movie: flat inside the folder; TV:
/// under a season subfolder) is supplied by the caller via <see cref="BuildPlan"/>'s
/// <c>buildFolderName</c>/<c>buildRelativePath</c> delegates rather than hard-coded here, so this
/// one implementation serves both kinds.
/// </summary>
public static class MultiVersionStubReconciler
{
    /// <summary>
    /// Builds the reconcile plan.
    /// </summary>
    /// <param name="desiredItems">The deduplicated desired stub set (see <see cref="DesiredStubSet"/>).</param>
    /// <param name="profiles">Every version to offer, for every title in this call - resolved once
    /// per sync, not per title (see <see cref="RequestProfiles.IRequestProfileResolver"/>).</param>
    /// <param name="existingFolders">Every title folder currently in the stub root.</param>
    /// <param name="expectedContent">The content every desired version's file should currently hold.</param>
    /// <param name="buildFolderName">Computes an item's title folder name.</param>
    /// <param name="buildRelativePath">Computes one version's file path, relative to its title folder.</param>
    /// <returns>The plan.</returns>
    public static MultiVersionStubReconcilePlan BuildPlan(
        IReadOnlyList<ListItem> desiredItems,
        IReadOnlyList<ResolvedRequestProfile> profiles,
        IReadOnlyList<ExistingVersionedFolder> existingFolders,
        string expectedContent,
        Func<ListItem, string> buildFolderName,
        Func<ListItem, ResolvedRequestProfile, string> buildRelativePath)
    {
        var existingByFolder = existingFolders.ToDictionary(f => f.FolderName, f => f.ExistingFilesByRelativePath);
        var desiredFolderNames = new HashSet<string>();
        var foldersToReconcile = new List<VersionedFolderPlan>();

        foreach (var item in desiredItems)
        {
            var folderName = buildFolderName(item);
            desiredFolderNames.Add(folderName);

            existingByFolder.TryGetValue(folderName, out var existingFiles);
            existingFiles ??= new Dictionary<string, string>();

            var desiredRelativePaths = new HashSet<string>();
            var filesToWrite = new List<VersionFileWrite>();

            foreach (var profile in profiles)
            {
                var relativePath = buildRelativePath(item, profile);
                desiredRelativePaths.Add(relativePath);

                if (!existingFiles.TryGetValue(relativePath, out var currentContent) || currentContent != expectedContent)
                {
                    filesToWrite.Add(new VersionFileWrite(relativePath, expectedContent));
                }
            }

            var relativePathsToDelete = existingFiles.Keys
                .Where(path => !desiredRelativePaths.Contains(path))
                .ToList();

            if (relativePathsToDelete.Count > 0 || filesToWrite.Count > 0)
            {
                foldersToReconcile.Add(new VersionedFolderPlan(folderName, relativePathsToDelete, filesToWrite));
            }
        }

        var folderNamesToDelete = existingFolders
            .Select(f => f.FolderName)
            .Where(name => !desiredFolderNames.Contains(name))
            .ToList();

        return new MultiVersionStubReconcilePlan(folderNamesToDelete, foldersToReconcile);
    }
}
