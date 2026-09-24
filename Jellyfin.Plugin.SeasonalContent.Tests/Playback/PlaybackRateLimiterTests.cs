using System;
using Jellyfin.Plugin.SeasonalContent.Lists;
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

        Assert.True(limiter.ShouldAllow(Guid.NewGuid(), MediaKind.Movie, 4977));
    }

    [Fact]
    public void ASecondCallForTheSameUserAndTitleWithinTheWindowIsSuppressed()
    {
        var clock = new FakeClock();
        var limiter = new PlaybackRateLimiter(clock, TimeSpan.FromSeconds(30));
        var userId = Guid.NewGuid();

        limiter.ShouldAllow(userId, MediaKind.Movie, 4977);
        clock.UtcNow = clock.UtcNow.AddSeconds(5);

        Assert.False(limiter.ShouldAllow(userId, MediaKind.Movie, 4977));
    }

    [Fact]
    public void ADifferentUserForTheSameTitleIsNotSuppressed()
    {
        var limiter = new PlaybackRateLimiter(new FakeClock(), TimeSpan.FromSeconds(30));

        limiter.ShouldAllow(Guid.NewGuid(), MediaKind.Movie, 4977);

        Assert.True(limiter.ShouldAllow(Guid.NewGuid(), MediaKind.Movie, 4977));
    }

    [Fact]
    public void TheSameUserForADifferentTitleIsNotSuppressed()
    {
        var limiter = new PlaybackRateLimiter(new FakeClock(), TimeSpan.FromSeconds(30));
        var userId = Guid.NewGuid();

        limiter.ShouldAllow(userId, MediaKind.Movie, 4977);

        Assert.True(limiter.ShouldAllow(userId, MediaKind.Movie, 603));
    }

    [Fact]
    public void ACallAfterTheWindowHasElapsedIsAllowedAgain()
    {
        var clock = new FakeClock();
        var limiter = new PlaybackRateLimiter(clock, TimeSpan.FromSeconds(30));
        var userId = Guid.NewGuid();

        limiter.ShouldAllow(userId, MediaKind.Movie, 4977);
        clock.UtcNow = clock.UtcNow.AddSeconds(31);

        Assert.True(limiter.ShouldAllow(userId, MediaKind.Movie, 4977));
    }

    [Fact]
    public void AMovieAndAShowSharingTheSameTmdbIdAreNotSuppressedByEachOther()
    {
        // Pins the TMDb-collision constraint (docs/rename-tv-globalkey-plan.md): a movie and a
        // show can share a numeric TMDb id, so the same user requesting both back-to-back must not
        // have the second one suppressed as if it were a repeat of the first.
        var limiter = new PlaybackRateLimiter(new FakeClock(), TimeSpan.FromSeconds(30));
        var userId = Guid.NewGuid();

        limiter.ShouldAllow(userId, MediaKind.Movie, 4977);

        Assert.True(limiter.ShouldAllow(userId, MediaKind.Series, 4977));
    }
}
