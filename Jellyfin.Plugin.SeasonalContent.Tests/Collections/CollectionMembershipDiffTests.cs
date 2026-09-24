using System;
using Jellyfin.Plugin.SeasonalContent.Collections;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Collections;

public class CollectionMembershipDiffTests
{
    [Fact]
    public void EmptyDesiredAndEmptyCurrentProducesAnEmptyPlan()
    {
        var plan = CollectionMembershipDiff.Build([], []);

        Assert.Empty(plan.ToAdd);
        Assert.Empty(plan.ToRemove);
    }

    [Fact]
    public void ANewMemberNotInCurrentIsAdded()
    {
        var newId = Guid.NewGuid();

        var plan = CollectionMembershipDiff.Build([newId], []);

        Assert.Equal([newId], plan.ToAdd);
        Assert.Empty(plan.ToRemove);
    }

    [Fact]
    public void ACurrentMemberNoLongerDesiredIsRemoved()
    {
        var staleId = Guid.NewGuid();

        var plan = CollectionMembershipDiff.Build([], [staleId]);

        Assert.Empty(plan.ToAdd);
        Assert.Equal([staleId], plan.ToRemove);
    }

    [Fact]
    public void AMemberInBothDesiredAndCurrentIsNeitherAddedNorRemoved()
    {
        var keptId = Guid.NewGuid();

        var plan = CollectionMembershipDiff.Build([keptId], [keptId]);

        Assert.Empty(plan.ToAdd);
        Assert.Empty(plan.ToRemove);
    }

    [Fact]
    public void OneAddAndOneRemoveInTheSameSyncAreBothReported()
    {
        var keptId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var staleId = Guid.NewGuid();

        var plan = CollectionMembershipDiff.Build([keptId, newId], [keptId, staleId]);

        Assert.Equal([newId], plan.ToAdd);
        Assert.Equal([staleId], plan.ToRemove);
    }
}
