using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Stubs;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Stubs;

public class StubReconcilerTests
{
    private const string ExpectedContent = "http://192.0.2.10:8097/SeasonalContent/Dummy";

    [Fact]
    public void NoDesiredAndNoExistingProducesAnEmptyPlan()
    {
        // Vacuum check: an empty sync must not be confused with "everything needs deleting" or
        // "everything needs creating" - both would also produce non-throwing, plausible-looking
        // output if the diff logic were simply wrong.
        var plan = StubReconciler.BuildPlan([], [], ExpectedContent);

        Assert.Empty(plan.FileNamesToDelete);
        Assert.Empty(plan.FilesToWrite);
    }

    [Fact]
    public void ANewDesiredItemWithNoExistingFileIsCreated()
    {
        var desired = new[] { new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1") };

        var plan = StubReconciler.BuildPlan(desired, [], ExpectedContent);

        var write = Assert.Single(plan.FilesToWrite);
        Assert.Equal("Paprika (2006) [tmdbid-4977].strm", write.FileName);
        Assert.Equal(ExpectedContent, write.Content);
        Assert.Empty(plan.FileNamesToDelete);
    }

    [Fact]
    public void AnExistingFileNoLongerDesiredIsDeleted()
    {
        var existing = new[] { new ExistingStubFile("Paprika (2006) [tmdbid-4977].strm", ExpectedContent) };

        var plan = StubReconciler.BuildPlan([], existing, ExpectedContent);

        var deleted = Assert.Single(plan.FileNamesToDelete);
        Assert.Equal("Paprika (2006) [tmdbid-4977].strm", deleted);
        Assert.Empty(plan.FilesToWrite);
    }

    [Fact]
    public void AMatchingDesiredAndExistingFileWithCorrectContentIsANoOp()
    {
        var desired = new[] { new ListItem(4977, "tt0851578", "Paprika", 2006, "list-1") };
        var existing = new[] { new ExistingStubFile("Paprika (2006) [tmdbid-4977].strm", ExpectedContent) };

        var plan = StubReconciler.BuildPlan(desired, existing, ExpectedContent);

        Assert.Empty(plan.FileNamesToDelete);
        Assert.Empty(plan.FilesToWrite);
    }

    [Fact]
    public void EveryFileWithStaleContentIsRewrittenNotJustTheFirst()
    {
        // docs/implementation-plan.md §3.2 explicitly calls out the POC's bug: it only checked the
        // first stub's content before deciding whether to flush. This proves all three get caught.
        var desired = new[]
        {
            new ListItem(1, null, "One", 2020, "list-1"),
            new ListItem(2, null, "Two", 2020, "list-1"),
            new ListItem(3, null, "Three", 2020, "list-1")
        };
        var existing = new[]
        {
            new ExistingStubFile("One (2020) [tmdbid-1].strm", "http://old-address/SeasonalContent/Dummy"),
            new ExistingStubFile("Two (2020) [tmdbid-2].strm", ExpectedContent),
            new ExistingStubFile("Three (2020) [tmdbid-3].strm", "http://old-address/SeasonalContent/Dummy")
        };

        var plan = StubReconciler.BuildPlan(desired, existing, ExpectedContent);

        Assert.Equal(2, plan.FilesToWrite.Count);
        Assert.Contains(plan.FilesToWrite, w => w.FileName == "One (2020) [tmdbid-1].strm" && w.Content == ExpectedContent);
        Assert.Contains(plan.FilesToWrite, w => w.FileName == "Three (2020) [tmdbid-3].strm" && w.Content == ExpectedContent);
        Assert.DoesNotContain(plan.FilesToWrite, w => w.FileName == "Two (2020) [tmdbid-2].strm");
        Assert.Empty(plan.FileNamesToDelete);
    }

    [Fact]
    public void OneListLosingATitleAndAnotherGainingOneReconcilesBothInTheSamePlan()
    {
        var desired = new[] { new ListItem(2, null, "Two", 2020, "list-2") };
        var existing = new[] { new ExistingStubFile("One (2020) [tmdbid-1].strm", ExpectedContent) };

        var plan = StubReconciler.BuildPlan(desired, existing, ExpectedContent);

        Assert.Equal(["One (2020) [tmdbid-1].strm"], plan.FileNamesToDelete);
        var write = Assert.Single(plan.FilesToWrite);
        Assert.Equal("Two (2020) [tmdbid-2].strm", write.FileName);
    }
}
