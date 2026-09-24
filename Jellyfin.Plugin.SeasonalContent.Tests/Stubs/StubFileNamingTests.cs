using Jellyfin.Plugin.SeasonalContent.Stubs;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Stubs;

public class StubFileNamingTests
{
    [Fact]
    public void BuildsTheStandardPattern()
    {
        var name = StubFileNaming.BuildFileName("Paprika", 2006, 4977);

        Assert.Equal("Paprika (2006) [tmdbid-4977].strm", name);
    }

    [Fact]
    public void OmitsTheYearParenthesesWhenYearIsMissing()
    {
        // Presence companion: proves the year block is conditionally built, not just present with
        // a blank value - a title-only item must not produce "Title () [tmdbid-X].strm".
        var name = StubFileNaming.BuildFileName("Untitled Project", null, 12345);

        Assert.Equal("Untitled Project [tmdbid-12345].strm", name);
    }

    [Fact]
    public void StripsAColonFromTheTitle()
    {
        var name = StubFileNaming.BuildFileName("Spider-Man: Into the Spider-Verse", 2018, 324857);

        Assert.Equal("Spider-Man Into the Spider-Verse (2018) [tmdbid-324857].strm", name);
    }

    [Theory]
    [InlineData("Movie/Title")]
    [InlineData("Movie\\Title")]
    [InlineData("../../etc/passwd")]
    public void StripsPathSeparatorsSoTheFileNameCannotEscapeTheStubRoot(string maliciousTitle)
    {
        // Security-relevant: Title comes from the MDBList API, an external source. If a crafted
        // title survives with a "/" or "\" in it, Path.Combine(stubRoot, fileName) could produce
        // a path outside the stub root, defeating the "only ever touch files inside the root"
        // invariant (docs/implementation-plan.md §3.2) before StubPath.IsUnderRoot even runs.
        var name = StubFileNaming.BuildFileName(maliciousTitle, 2024, 1);

        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain('\\', name);
    }

    [Fact]
    public void TrimsWhitespaceLeftBehindAfterStrippingInvalidCharacters()
    {
        var name = StubFileNaming.BuildFileName("  Weird Title?  ", 2020, 42);

        Assert.Equal("Weird Title (2020) [tmdbid-42].strm", name);
    }

    [Fact]
    public void EmptyTitleAfterSanitisationStillProducesAValidFileName()
    {
        // Vacuum check: an all-invalid-characters title must not produce an empty or malformed
        // file name that could collide with another empty-titled item or break on the filesystem.
        var name = StubFileNaming.BuildFileName("///", 2020, 99);

        Assert.Equal("(2020) [tmdbid-99].strm", name);
    }

    [Fact]
    public void ParsesTheTmdbIdBackOutOfAWellFormedStubFileName()
    {
        var id = StubFileNaming.TryParseTmdbId("Paprika (2006) [tmdbid-4977].strm");

        Assert.Equal(4977, id);
    }

    [Fact]
    public void ParsesTheTmdbIdWhenThereIsNoYearBlock()
    {
        var id = StubFileNaming.TryParseTmdbId("Untitled Project [tmdbid-12345].strm");

        Assert.Equal(12345, id);
    }

    [Fact]
    public void ReturnsNullForARealFileNameWithNoTmdbIdMarker()
    {
        // Presence companion: a real library file's name must never be mistaken for a stub's.
        var id = StubFileNaming.TryParseTmdbId("12 Angry Men (1957).mp4");

        Assert.Null(id);
    }

    [Fact]
    public void ReturnsNullWhenTheMarkerTextAppearsOutsideBrackets()
    {
        // A title that happens to contain the literal word "tmdbid" must not be mistaken for the
        // real marker - only the bracketed form counts.
        var id = StubFileNaming.TryParseTmdbId("My tmdbid-99 Story (2020).strm");

        Assert.Null(id);
    }
}
