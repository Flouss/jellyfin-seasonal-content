using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Stubs;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Stubs;

public class TvStubReconcilerTests
{
    private const string ExpectedContent = "http://192.0.2.10:8097/SeasonalContent/Dummy";

    private static ListItem Show(int tmdbId, string title, int? year = 2018, string listId = "list-1") =>
        new(tmdbId, null, title, year, listId, MediaKind.Series);

    [Fact]
    public void NoDesiredAndNoExistingProducesAnEmptyPlan()
    {
        var plan = TvStubReconciler.BuildPlan([], [], ExpectedContent);

        Assert.Empty(plan.FolderNamesToDelete);
        Assert.Empty(plan.SeriesToWrite);
    }

    [Fact]
    public void ANewDesiredShowWithNoExistingFolderIsCreated()
    {
        var desired = new[] { Show(79242, "Chilling Adventures of Sabrina") };

        var plan = TvStubReconciler.BuildPlan(desired, [], ExpectedContent);

        var write = Assert.Single(plan.SeriesToWrite);
        Assert.Equal("Chilling Adventures of Sabrina (2018) [tmdbid-79242]", write.FolderName);
        Assert.Equal(ExpectedContent, write.Content);
        Assert.Empty(plan.FolderNamesToDelete);
    }

    [Fact]
    public void AnExistingFolderNoLongerDesiredIsDeleted()
    {
        var existing = new[] { new ExistingTvSeriesFolder("Chilling Adventures of Sabrina (2018) [tmdbid-79242]", ExpectedContent) };

        var plan = TvStubReconciler.BuildPlan([], existing, ExpectedContent);

        var deleted = Assert.Single(plan.FolderNamesToDelete);
        Assert.Equal("Chilling Adventures of Sabrina (2018) [tmdbid-79242]", deleted);
        Assert.Empty(plan.SeriesToWrite);
    }

    [Fact]
    public void AMatchingDesiredAndExistingFolderWithCorrectContentIsANoOp()
    {
        var desired = new[] { Show(79242, "Chilling Adventures of Sabrina") };
        var existing = new[] { new ExistingTvSeriesFolder("Chilling Adventures of Sabrina (2018) [tmdbid-79242]", ExpectedContent) };

        var plan = TvStubReconciler.BuildPlan(desired, existing, ExpectedContent);

        Assert.Empty(plan.FolderNamesToDelete);
        Assert.Empty(plan.SeriesToWrite);
    }

    [Fact]
    public void AFolderWithAMissingEpisodeFileIsRepaired()
    {
        // EpisodeContent = null means the dummy episode file is missing (e.g. an interrupted
        // previous write) - must be treated the same as stale content, not "already correct".
        var desired = new[] { Show(79242, "Chilling Adventures of Sabrina") };
        var existing = new[] { new ExistingTvSeriesFolder("Chilling Adventures of Sabrina (2018) [tmdbid-79242]", null) };

        var plan = TvStubReconciler.BuildPlan(desired, existing, ExpectedContent);

        var write = Assert.Single(plan.SeriesToWrite);
        Assert.Equal(ExpectedContent, write.Content);
        Assert.Empty(plan.FolderNamesToDelete);
    }

    [Fact]
    public void EveryFolderWithStaleContentIsRewrittenNotJustTheFirst()
    {
        var desired = new[]
        {
            Show(1, "One"),
            Show(2, "Two"),
            Show(3, "Three")
        };
        var existing = new[]
        {
            new ExistingTvSeriesFolder("One (2018) [tmdbid-1]", "http://old-address/SeasonalContent/Dummy"),
            new ExistingTvSeriesFolder("Two (2018) [tmdbid-2]", ExpectedContent),
            new ExistingTvSeriesFolder("Three (2018) [tmdbid-3]", "http://old-address/SeasonalContent/Dummy")
        };

        var plan = TvStubReconciler.BuildPlan(desired, existing, ExpectedContent);

        Assert.Equal(2, plan.SeriesToWrite.Count);
        Assert.Contains(plan.SeriesToWrite, w => w.FolderName == "One (2018) [tmdbid-1]" && w.Content == ExpectedContent);
        Assert.Contains(plan.SeriesToWrite, w => w.FolderName == "Three (2018) [tmdbid-3]" && w.Content == ExpectedContent);
        Assert.DoesNotContain(plan.SeriesToWrite, w => w.FolderName == "Two (2018) [tmdbid-2]");
        Assert.Empty(plan.FolderNamesToDelete);
    }
}
