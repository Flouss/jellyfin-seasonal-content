using Jellyfin.Plugin.SeasonalContent.Stubs;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Stubs;

public class TvStubFileNamingTests
{
    [Fact]
    public void BuildsTheStandardSeriesFolderName()
    {
        var name = TvStubFileNaming.BuildSeriesFolderName("Chilling Adventures of Sabrina", 2018, 79242);

        Assert.Equal("Chilling Adventures of Sabrina (2018) [tmdbid-79242]", name);
    }

    [Fact]
    public void OmitsTheYearParenthesesWhenYearIsMissing()
    {
        var name = TvStubFileNaming.BuildSeriesFolderName("Untitled Show", null, 12345);

        Assert.Equal("Untitled Show [tmdbid-12345]", name);
    }

    [Fact]
    public void BuildsTheEpisodeRelativePathUnderSeasonOne()
    {
        var path = TvStubFileNaming.BuildEpisodeRelativePath("Chilling Adventures of Sabrina", 79242);

        Assert.Equal(System.IO.Path.Combine("Season 01", "Chilling Adventures of Sabrina S01E01 [tmdbid-79242].strm"), path);
    }

    [Fact]
    public void EmbeddedTmdbTagInTheEpisodeFileNameIsParsedBackOutByTheSharedParser()
    {
        // The whole reason the tag is embedded in the episode file name too, not just the series
        // folder: the playback interceptor's filename fallback (StubFileNaming.TryParseTmdbId,
        // deliberately reused unmodified) must be able to resolve it from the episode's own path.
        var path = TvStubFileNaming.BuildEpisodeRelativePath("Chilling Adventures of Sabrina", 79242);
        var fileName = System.IO.Path.GetFileName(path);

        Assert.Equal(79242, StubFileNaming.TryParseTmdbId(fileName));
    }

    [Theory]
    [InlineData("Show/Title")]
    [InlineData("Show\\Title")]
    [InlineData("../../etc/passwd")]
    public void StripsPathSeparatorsFromTheSeriesFolderName(string maliciousTitle)
    {
        var name = TvStubFileNaming.BuildSeriesFolderName(maliciousTitle, 2024, 1);

        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain('\\', name);
    }

    [Theory]
    [InlineData("Normal Show (2018) [tmdbid-79242]", true)]
    [InlineData("..", false)]
    [InlineData("../escape", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("", false)]
    public void IsSafeFolderNameRejectsAnythingThatIsNotAPlainSingleSegment(string candidate, bool expectedSafe)
    {
        // Mutation-proofing target for TvStubFileIoExecutor's recursive-delete guard
        // (docs/rename-tv-globalkey-plan.md): this must reject any candidate that could make
        // Path.Combine(root, candidate) resolve outside the stub root.
        Assert.Equal(expectedSafe, TvStubFileNaming.IsSafeFolderName(candidate));
    }
}
