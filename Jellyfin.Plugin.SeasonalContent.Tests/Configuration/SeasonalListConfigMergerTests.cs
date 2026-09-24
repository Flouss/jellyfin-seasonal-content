using System;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Configuration;

public class SeasonalListConfigMergerTests
{
    private static SeasonalListInput ValidInput(Guid? id = null, string displayName = "Halloween") =>
        new(id, true, displayName, "hdlists", "the-top-100-halloween-movies-of-all-time", "real-api-key", 100);

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
    [InlineData("", "hdlists", "slug", "key")]
    [InlineData("Name", "", "slug", "key")]
    [InlineData("Name", "hdlists", "", "key")]
    [InlineData("Name", "hdlists", "slug", "")]
    public void MissingARequiredFieldProducesAnErrorAndSavesNothing(string displayName, string username, string slug, string apiKey)
    {
        var input = new SeasonalListInput(null, true, displayName, username, slug, apiKey, 100);

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
}
