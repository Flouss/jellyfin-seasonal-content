using System.Linq;
using Jellyfin.Plugin.SeasonalContent.Setup;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Setup;

public class LibrarySetupPlanTests
{
    private const string StubRoot = "/config/data/plugins/Jellyfin.Plugin.SeasonalContent/movies_seasonal";

    [Fact]
    public void StubLibraryExists_TrueWhenALibraryLocationMatchesTheStubRootExactly()
    {
        var folders = new[] { new VirtualFolderInfo { Locations = [StubRoot] } };

        Assert.True(LibrarySetupPlan.StubLibraryExists(folders, StubRoot));
    }

    [Fact]
    public void StubLibraryExists_FalseWhenNoLibraryLocationMatches()
    {
        var folders = new[] { new VirtualFolderInfo { Locations = ["/mnt/Movies"] } };

        Assert.False(LibrarySetupPlan.StubLibraryExists(folders, StubRoot));
    }

    [Fact]
    public void StubLibraryExists_FalseWhenNoLibrariesExist()
    {
        Assert.False(LibrarySetupPlan.StubLibraryExists(Enumerable.Empty<VirtualFolderInfo>(), StubRoot));
    }

    [Fact]
    public void StubLibraryExists_TrueDespiteATrailingSlashDifference()
    {
        var folders = new[] { new VirtualFolderInfo { Locations = [StubRoot + "/"] } };

        Assert.True(LibrarySetupPlan.StubLibraryExists(folders, StubRoot));
    }

    [Fact]
    public void StubLibraryExists_FalseForASiblingPathThatMerelyStartsWithTheStubRoot()
    {
        // Same class of bug StubPath.IsUnderRoot guards against: a sibling folder whose name
        // happens to start with the stub root's name is not the stub root itself.
        var folders = new[] { new VirtualFolderInfo { Locations = [StubRoot + "_archive"] } };

        Assert.False(LibrarySetupPlan.StubLibraryExists(folders, StubRoot));
    }

    [Fact]
    public void StubLibraryExists_HandlesALibraryWithNoLocations()
    {
        var folders = new[] { new VirtualFolderInfo { Locations = null! } };

        Assert.False(LibrarySetupPlan.StubLibraryExists(folders, StubRoot));
    }

    [Fact]
    public void CollectionsLibraryExists_TrueWhenABoxsetsLibraryIsPresent()
    {
        var folders = new[] { new VirtualFolderInfo { CollectionType = CollectionTypeOptions.boxsets } };

        Assert.True(LibrarySetupPlan.CollectionsLibraryExists(folders));
    }

    [Fact]
    public void CollectionsLibraryExists_FalseWhenOnlyOtherLibraryTypesArePresent()
    {
        var folders = new[]
        {
            new VirtualFolderInfo { CollectionType = CollectionTypeOptions.movies },
            new VirtualFolderInfo { CollectionType = CollectionTypeOptions.tvshows }
        };

        Assert.False(LibrarySetupPlan.CollectionsLibraryExists(folders));
    }

    [Fact]
    public void CollectionsLibraryExists_FalseWhenNoLibrariesExist()
    {
        Assert.False(LibrarySetupPlan.CollectionsLibraryExists(Enumerable.Empty<VirtualFolderInfo>()));
    }

    [Fact]
    public void CollectionsLibraryExists_FalseWhenCollectionTypeIsNull()
    {
        var folders = new[] { new VirtualFolderInfo { CollectionType = null } };

        Assert.False(LibrarySetupPlan.CollectionsLibraryExists(folders));
    }
}
