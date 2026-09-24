using System.Collections.Generic;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.RequestProfiles;
using Jellyfin.Plugin.SeasonalContent.Stubs;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Stubs;

public class MultiVersionStubReconcilerTests
{
    private const string ExpectedContent = "http://192.0.2.10:8097/SeasonalContent/Dummy";

    private static readonly ResolvedRequestProfile[] TwoProfiles =
    [
        new ResolvedRequestProfile("FHD", 1, 10),
        new ResolvedRequestProfile("4K", 2, 20)
    ];

    private static string BuildFolderName(ListItem item) => StubFileNaming.BuildTitleFolderName(item.Title, item.Year, item.TmdbId);

    private static string BuildRelativePath(ListItem item, ResolvedRequestProfile profile) =>
        StubFileNaming.BuildVersionFileName(item.Title, item.Year, item.TmdbId, profile.Label);

    private static MultiVersionStubReconcilePlan BuildPlan(
        IReadOnlyList<ListItem> desired,
        IReadOnlyList<ResolvedRequestProfile> profiles,
        IReadOnlyList<ExistingVersionedFolder> existing) =>
        MultiVersionStubReconciler.BuildPlan(desired, profiles, existing, ExpectedContent, BuildFolderName, BuildRelativePath);

    [Fact]
    public void NoDesiredAndNoExistingProducesAnEmptyPlan()
    {
        // Vacuum check: an empty sync must not be confused with "everything needs deleting".
        var plan = BuildPlan([], TwoProfiles, []);

        Assert.Empty(plan.FolderNamesToDelete);
        Assert.Empty(plan.FoldersToReconcile);
    }

    [Fact]
    public void ANewTitleWithNoExistingFolderGetsEveryProfileFileWritten()
    {
        var desired = new[] { new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1") };

        var plan = BuildPlan(desired, TwoProfiles, []);

        var folderPlan = Assert.Single(plan.FoldersToReconcile);
        Assert.Equal("Paprika (2006) [tmdbid-4977]", folderPlan.FolderName);
        Assert.Equal(2, folderPlan.FilesToWrite.Count);
        Assert.Contains(folderPlan.FilesToWrite, f => f.RelativePath == "Paprika (2006) [tmdbid-4977] - FHD.strm" && f.Content == ExpectedContent);
        Assert.Contains(folderPlan.FilesToWrite, f => f.RelativePath == "Paprika (2006) [tmdbid-4977] - 4K.strm" && f.Content == ExpectedContent);
        Assert.Empty(folderPlan.RelativePathsToDelete);
        Assert.Empty(plan.FolderNamesToDelete);
    }

    [Fact]
    public void AnExistingFolderNoLongerDesiredIsDeletedWhole()
    {
        var existing = new[]
        {
            new ExistingVersionedFolder(
                "Paprika (2006) [tmdbid-4977]",
                new Dictionary<string, string>
                {
                    ["Paprika (2006) [tmdbid-4977] - FHD.strm"] = ExpectedContent,
                    ["Paprika (2006) [tmdbid-4977] - 4K.strm"] = ExpectedContent
                })
        };

        var plan = BuildPlan([], TwoProfiles, existing);

        var deleted = Assert.Single(plan.FolderNamesToDelete);
        Assert.Equal("Paprika (2006) [tmdbid-4977]", deleted);
        Assert.Empty(plan.FoldersToReconcile);
    }

    [Fact]
    public void AFullyInSyncFolderProducesNoReconcileEntryAtAll()
    {
        // Not just "nothing to write" - the folder itself must be absent from FoldersToReconcile,
        // proving the executor won't be asked to touch it at all. A naive `>= 0` instead of `> 0`
        // guard would still pass a weaker "empty lists" assertion but fail this one.
        var desired = new[] { new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1") };
        var existing = new[]
        {
            new ExistingVersionedFolder(
                "Paprika (2006) [tmdbid-4977]",
                new Dictionary<string, string>
                {
                    ["Paprika (2006) [tmdbid-4977] - FHD.strm"] = ExpectedContent,
                    ["Paprika (2006) [tmdbid-4977] - 4K.strm"] = ExpectedContent
                })
        };

        var plan = BuildPlan(desired, TwoProfiles, existing);

        Assert.Empty(plan.FoldersToReconcile);
        Assert.Empty(plan.FolderNamesToDelete);
    }

    [Fact]
    public void RemovingAProfileDeletesOnlyThatFilesAndKeepsTheFolder()
    {
        var desired = new[] { new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1") };
        var existing = new[]
        {
            new ExistingVersionedFolder(
                "Paprika (2006) [tmdbid-4977]",
                new Dictionary<string, string>
                {
                    ["Paprika (2006) [tmdbid-4977] - FHD.strm"] = ExpectedContent,
                    ["Paprika (2006) [tmdbid-4977] - 4K.strm"] = ExpectedContent,
                    ["Paprika (2006) [tmdbid-4977] - Old.strm"] = ExpectedContent
                })
        };

        var plan = BuildPlan(desired, TwoProfiles, existing);

        var folderPlan = Assert.Single(plan.FoldersToReconcile);
        var deletedPath = Assert.Single(folderPlan.RelativePathsToDelete);
        Assert.Equal("Paprika (2006) [tmdbid-4977] - Old.strm", deletedPath);
        Assert.Empty(folderPlan.FilesToWrite);
        Assert.Empty(plan.FolderNamesToDelete);
    }

    [Fact]
    public void AddingAProfileWritesOnlyTheNewFileAndLeavesTheRestAlone()
    {
        var desired = new[] { new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1") };
        var existing = new[]
        {
            new ExistingVersionedFolder(
                "Paprika (2006) [tmdbid-4977]",
                new Dictionary<string, string> { ["Paprika (2006) [tmdbid-4977] - FHD.strm"] = ExpectedContent })
        };

        var plan = BuildPlan(desired, TwoProfiles, existing);

        var folderPlan = Assert.Single(plan.FoldersToReconcile);
        var write = Assert.Single(folderPlan.FilesToWrite);
        Assert.Equal("Paprika (2006) [tmdbid-4977] - 4K.strm", write.RelativePath);
        Assert.Empty(folderPlan.RelativePathsToDelete);
    }

    [Fact]
    public void StaleContentIsRewritten()
    {
        var desired = new[] { new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1") };
        var existing = new[]
        {
            new ExistingVersionedFolder(
                "Paprika (2006) [tmdbid-4977]",
                new Dictionary<string, string>
                {
                    ["Paprika (2006) [tmdbid-4977] - FHD.strm"] = "http://old-address/SeasonalContent/Dummy",
                    ["Paprika (2006) [tmdbid-4977] - 4K.strm"] = ExpectedContent
                })
        };

        var plan = BuildPlan(desired, TwoProfiles, existing);

        var folderPlan = Assert.Single(plan.FoldersToReconcile);
        var write = Assert.Single(folderPlan.FilesToWrite);
        Assert.Equal("Paprika (2006) [tmdbid-4977] - FHD.strm", write.RelativePath);
        Assert.Equal(ExpectedContent, write.Content);
    }

    [Fact]
    public void OneTitleLosingAFileAndAnotherGainingOneReconcileIndependentlyInTheSamePlan()
    {
        var desired = new[]
        {
            new ListItem(1, null, "One", 2020, "list-1"),
            new ListItem(2, null, "Two", 2020, "list-1")
        };
        var existing = new[]
        {
            new ExistingVersionedFolder(
                "One (2020) [tmdbid-1]",
                new Dictionary<string, string>
                {
                    ["One (2020) [tmdbid-1] - FHD.strm"] = ExpectedContent,
                    ["One (2020) [tmdbid-1] - 4K.strm"] = ExpectedContent,
                    ["One (2020) [tmdbid-1] - Old.strm"] = ExpectedContent
                }),
            new ExistingVersionedFolder(
                "Two (2020) [tmdbid-2]",
                new Dictionary<string, string> { ["Two (2020) [tmdbid-2] - FHD.strm"] = ExpectedContent })
        };

        var plan = BuildPlan(desired, TwoProfiles, existing);

        Assert.Equal(2, plan.FoldersToReconcile.Count);
        var one = Assert.Single(plan.FoldersToReconcile, f => f.FolderName == "One (2020) [tmdbid-1]");
        Assert.Equal(["One (2020) [tmdbid-1] - Old.strm"], one.RelativePathsToDelete);
        Assert.Empty(one.FilesToWrite);
        var two = Assert.Single(plan.FoldersToReconcile, f => f.FolderName == "Two (2020) [tmdbid-2]");
        Assert.Empty(two.RelativePathsToDelete);
        var twoWrite = Assert.Single(two.FilesToWrite);
        Assert.Equal("Two (2020) [tmdbid-2] - 4K.strm", twoWrite.RelativePath);
        Assert.Empty(plan.FolderNamesToDelete);
    }

    [Fact]
    public void ATitleNoLongerDesiredIsDeletedWholeWhileAnotherIsReconciledInTheSamePlan()
    {
        var desired = new[] { new ListItem(2, null, "Two", 2020, "list-2") };
        var existing = new[]
        {
            new ExistingVersionedFolder(
                "One (2020) [tmdbid-1]",
                new Dictionary<string, string> { ["One (2020) [tmdbid-1] - FHD.strm"] = ExpectedContent }),
            new ExistingVersionedFolder(
                "Two (2020) [tmdbid-2]",
                new Dictionary<string, string> { ["Two (2020) [tmdbid-2] - FHD.strm"] = ExpectedContent })
        };

        var plan = BuildPlan(desired, TwoProfiles, existing);

        Assert.Equal(["One (2020) [tmdbid-1]"], plan.FolderNamesToDelete);
        var two = Assert.Single(plan.FoldersToReconcile);
        Assert.Equal("Two (2020) [tmdbid-2]", two.FolderName);
        var write = Assert.Single(two.FilesToWrite);
        Assert.Equal("Two (2020) [tmdbid-2] - 4K.strm", write.RelativePath);
    }
}
