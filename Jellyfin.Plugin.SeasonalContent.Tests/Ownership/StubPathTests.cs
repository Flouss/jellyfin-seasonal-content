using Jellyfin.Plugin.SeasonalContent.Ownership;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Ownership;

public class StubPathTests
{
    private const string Root = "/config/data/plugins/Jellyfin.Plugin.SeasonalContent/movies_seasonal";

    [Fact]
    public void TrueForAFileDirectlyInsideTheRoot()
    {
        Assert.True(StubPath.IsUnderRoot($"{Root}/Paprika (2006) [tmdbid-4977].strm", Root));
    }

    [Fact]
    public void TrueForAFileInsideANestedSubfolder()
    {
        Assert.True(StubPath.IsUnderRoot($"{Root}/sub/folder/movie.strm", Root));
    }

    [Fact]
    public void FalseForASiblingFolderThatMerelyStartsWithTheRootName()
    {
        // The exact POC bug this exists to fix: Path.Contains("movies_seasonal") would also match
        // "movies_seasonal_archive/real_movie.mkv", which is NOT under the stub root.
        Assert.False(StubPath.IsUnderRoot($"{Root}_archive/real_movie.mkv", Root));
    }

    [Fact]
    public void FalseForAnUnrelatedPath()
    {
        Assert.False(StubPath.IsUnderRoot("/mnt/Movies/12 Angry Men (1957)/12 Angry Men (1957).mp4", Root));
    }

    [Fact]
    public void FalseForTheRootsOwnParentDirectory()
    {
        Assert.False(StubPath.IsUnderRoot("/config/data/plugins/Jellyfin.Plugin.SeasonalContent", Root));
    }

    [Fact]
    public void FalseForTheRootFolderItselfNotAFileInsideIt()
    {
        // The root is a folder, never a media item's own path - callers that walk "everything
        // under root" should never treat the root entry itself as a match.
        Assert.False(StubPath.IsUnderRoot(Root, Root));
    }
}
