using System;

namespace Jellyfin.Plugin.SeasonalContent.Playback;

/// <summary>
/// The current time, as an injectable seam so <see cref="PlaybackRateLimiter"/> can be tested
/// without real delays.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}

/// <inheritdoc />
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
