using System;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Configuration;

public class SeasonalListConfigMergerTests
{
    private static SeasonalListInput ValidInput(Guid? id = null, string displayName = "Halloween") =>
        new(id, true, displayName, "hdlists", "the-top-100-halloween-movies-of-all-time", 100);

    [Fact]
    public void EmptyExistingAndEmptyIncomingProducesAnEmptyResultWithNoErrors()
    {
        // Vacuum check: an empty save must not be mistaken for "everything survived" - both look
        // like a clean, error-free result if the merge logic is wrong.
        var result = SeasonalListConfigMerger.Merge([], []);

        Assert.Empty(result.Lists);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ANewEntryWithNoIdGetsAFreshIdAndNoCollectionId()
    {
        var result = SeasonalListConfigMerger.Merge([], [ValidInput()]);

        var saved = Assert.Single(result.Lists);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Null(saved.CollectionId);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ReSavingWithAMatchingIdKeepsItsCollectionId()
    {
        // The whole reason Id is stable (docs/implementation-plan.md §3.7, §4): a saved BoxSet id
        // must survive an edit-and-resave (e.g. the admin fixing a typo in DisplayName).
        var existingId = Guid.NewGuid();
        var existingCollectionId = Guid.NewGuid();
        var existing = new[]
        {
            new SeasonalListConfig { Id = existingId, CollectionId = existingCollectionId, DisplayName = "Old Name" }
        };

        var result = SeasonalListConfigMerger.Merge(existing, [ValidInput(existingId, "New Name")]);

        var saved = Assert.Single(result.Lists);
        Assert.Equal(existingId, saved.Id);
        Assert.Equal(existingCollectionId, saved.CollectionId);
        Assert.Equal("New Name", saved.DisplayName);
    }

    [Fact]
    public void AnIdThatDoesNotMatchAnyExistingEntryIsTreatedAsNew()
    {
        var existing = new[] { new SeasonalListConfig { Id = Guid.NewGuid(), CollectionId = Guid.NewGuid() } };
        var unknownId = Guid.NewGuid();

        var result = SeasonalListConfigMerger.Merge(existing, [ValidInput(unknownId)]);

        var saved = Assert.Single(result.Lists);
        Assert.Equal(unknownId, saved.Id);
        Assert.Null(saved.CollectionId);
    }

    [Theory]
    [InlineData("", "hdlists", "slug")]
    [InlineData("Name", "", "slug")]
    [InlineData("Name", "hdlists", "")]
    public void MissingARequiredFieldProducesAnErrorAndSavesNothing(string displayName, string username, string slug)
    {
        var input = new SeasonalListInput(null, true, displayName, username, slug, 100);

        var result = SeasonalListConfigMerger.Merge([], [input]);

        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Lists);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(501, 500)]
    [InlineData(100, 100)]
    public void LimitIsClampedTo1Through500(int requested, int expected)
    {
        var input = ValidInput() with { Limit = requested };

        var result = SeasonalListConfigMerger.Merge([], [input]);

        var saved = Assert.Single(result.Lists);
        Assert.Equal(expected, saved.Limit);
    }

    [Fact]
    public void TwoIncomingEntriesSharingTheSameIdProduceAnErrorAndSaveNothing()
    {
        // A prior version let this through, which corrupted the saved config: the next save's
        // existing.ToDictionary(l => l.Id) would throw on the duplicate key, permanently bricking
        // the endpoint until the XML was hand-edited.
        var sharedId = Guid.NewGuid();
        var inputs = new[] { ValidInput(sharedId, "First"), ValidInput(sharedId, "Second") };

        var result = SeasonalListConfigMerger.Merge([], inputs);

        // Errors.Count > 0 means the caller (Api/ListsController) must not persist result.Lists at
        // all (see ListsSaveResult's own doc comment) - that atomicity is the controller's
        // contract to honor, not Merge's to redundantly re-enforce by emptying Lists itself.
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void DuplicateIdsInExistingSavedConfigDoNotCrashTheNextMerge()
    {
        // Defensive: even if corrupted duplicate-Id data already exists on disk (e.g. from manual
        // XML editing, or a save made before the above fix shipped), a further save must not throw.
        var dupeId = Guid.NewGuid();
        var existing = new[]
        {
            new SeasonalListConfig { Id = dupeId, DisplayName = "A" },
            new SeasonalListConfig { Id = dupeId, DisplayName = "B" }
        };

        var result = SeasonalListConfigMerger.Merge(existing, [ValidInput()]);

        Assert.Empty(result.Errors);
        Assert.Single(result.Lists);
    }
}
