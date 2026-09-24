using System;
using Jellyfin.Plugin.SeasonalContent.Playback;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Playback;

public class PlaybackRateLimiterTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void TheFirstCallForAUserAndTitleIsAllowed()
    {
        var limiter = new PlaybackRateLimiter(new FakeClock(), TimeSpan.FromSeconds(30));

        Assert.True(limiter.ShouldAllow(Guid.NewGuid(), 4977));
    }

    [Fact]
    public void ASecondCallForTheSameUserAndTitleWithinTheWindowIsSuppressed()
    {
        var clock = new FakeClock();
        var limiter = new PlaybackRateLimiter(clock, TimeSpan.FromSeconds(30));
        var userId = Guid.NewGuid();

        limiter.ShouldAllow(userId, 4977);
        clock.UtcNow = clock.UtcNow.AddSeconds(5);

        Assert.False(limiter.ShouldAllow(userId, 4977));
    }

    [Fact]
    public void ADifferentUserForTheSameTitleIsNotSuppressed()
    {
        var limiter = new PlaybackRateLimiter(new FakeClock(), TimeSpan.FromSeconds(30));

        limiter.ShouldAllow(Guid.NewGuid(), 4977);

        Assert.True(limiter.ShouldAllow(Guid.NewGuid(), 4977));
    }

    [Fact]
    public void TheSameUserForADifferentTitleIsNotSuppressed()
    {
        var limiter = new PlaybackRateLimiter(new FakeClock(), TimeSpan.FromSeconds(30));
        var userId = Guid.NewGuid();

        limiter.ShouldAllow(userId, 4977);

        Assert.True(limiter.ShouldAllow(userId, 603));
    }

    [Fact]
    public void ACallAfterTheWindowHasElapsedIsAllowedAgain()
    {
        var clock = new FakeClock();
        var limiter = new PlaybackRateLimiter(clock, TimeSpan.FromSeconds(30));
        var userId = Guid.NewGuid();

        limiter.ShouldAllow(userId, 4977);
        clock.UtcNow = clock.UtcNow.AddSeconds(31);

        Assert.True(limiter.ShouldAllow(userId, 4977));
    }
}
