using Jellyfin.Plugin.SeasonalContent.Jellyseerr;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Jellyseerr;

public class RequestDecisionTests
{
    [Fact]
    public void NoMediaInfoAtAllMeansCreateARequest()
    {
        // The common case: Jellyseerr has never seen this title, so there's no MediaInfo object.
        var action = RequestDecision.Decide(null);

        Assert.Equal(RequestAction.CreateRequest, action);
    }

    [Theory]
    [InlineData(JellyseerrMediaStatus.Unknown)]
    [InlineData(JellyseerrMediaStatus.Deleted)]
    public void UnknownOrDeletedMeansCreateARequest(JellyseerrMediaStatus status)
    {
        // Vacuum check companion to the null case: a present-but-inert MediaInfo must not be
        // confused with "no MediaInfo" - both should reach the same CreateRequest action, but via
        // genuinely distinct inputs, not because the function ignores its argument.
        var action = RequestDecision.Decide(status);

        Assert.Equal(RequestAction.CreateRequest, action);
    }

    [Theory]
    [InlineData(JellyseerrMediaStatus.Pending)]
    [InlineData(JellyseerrMediaStatus.Processing)]
    public void PendingOrProcessingMeansAlreadyPending(JellyseerrMediaStatus status)
    {
        var action = RequestDecision.Decide(status);

        Assert.Equal(RequestAction.AlreadyPending, action);
    }

    [Theory]
    [InlineData(JellyseerrMediaStatus.PartiallyAvailable)]
    [InlineData(JellyseerrMediaStatus.Available)]
    public void PartiallyAvailableOrAvailableMeansAlreadyAvailable(JellyseerrMediaStatus status)
    {
        var action = RequestDecision.Decide(status);

        Assert.Equal(RequestAction.AlreadyAvailable, action);
    }
}
