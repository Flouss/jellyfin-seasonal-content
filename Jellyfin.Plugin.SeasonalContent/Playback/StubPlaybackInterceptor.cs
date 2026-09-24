using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using Jellyfin.Plugin.SeasonalContent.Jellyseerr;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Jellyfin.Plugin.SeasonalContent.RequestProfiles;
using Jellyfin.Plugin.SeasonalContent.Stubs;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.Playback;

/// <summary>
/// Intercepts playback of a stub item: stops it, then requests it through Jellyseerr (Option A+,
/// docs/implementation-plan.md §5 - fire-and-forget, created unapproved). Also resets a stub's
/// user data on stop so it never appears in Continue Watching or Next Up (§3.5). Runs on every
/// playback on the server, so the non-stub path must stay cheap and this must never throw into
/// Jellyfin - both event handlers are the one place <c>async void</c> is used, each wrapped in a
/// try/catch around a separately testable-by-construction <c>async Task</c>/synchronous method.
/// Handles both the movie and TV stub roots (docs/rename-tv-globalkey-plan.md §3).
/// </summary>
public sealed class StubPlaybackInterceptor : IHostedService
{
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(30);

    private readonly ISessionManager _sessionManager;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IJellyseerrClient _jellyseerrClient;
    private readonly IRequestProfileResolver _requestProfileResolver;
    private readonly PlaybackRateLimiter _rateLimiter;
    private readonly ILogger<StubPlaybackInterceptor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubPlaybackInterceptor"/> class.
    /// </summary>
    /// <param name="sessionManager">Instance of the <see cref="ISessionManager"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface - used
    /// to resolve a played TV stub episode's parent Series and read its real TMDb provider id.</param>
    /// <param name="jellyseerrClient">Instance of the <see cref="IJellyseerrClient"/> interface.</param>
    /// <param name="requestProfileResolver">Instance of the <see cref="IRequestProfileResolver"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public StubPlaybackInterceptor(
        ISessionManager sessionManager,
        IUserManager userManager,
        IUserDataManager userDataManager,
        ILibraryManager libraryManager,
        IJellyseerrClient jellyseerrClient,
        IRequestProfileResolver requestProfileResolver,
        ILogger<StubPlaybackInterceptor> logger)
    {
        _sessionManager = sessionManager;
        _userManager = userManager;
        _userDataManager = userDataManager;
        _libraryManager = libraryManager;
        _jellyseerrClient = jellyseerrClient;
        _requestProfileResolver = requestProfileResolver;
        _rateLimiter = new PlaybackRateLimiter(new SystemClock(), RateLimitWindow);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        return Task.CompletedTask;
    }

    private async void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        try
        {
            await HandlePlaybackStartAsync(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error handling PlaybackStart in the stub playback interceptor.");
        }
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        try
        {
            HandlePlaybackStopped(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error handling PlaybackStopped in the stub playback interceptor.");
        }
    }

    private async Task HandlePlaybackStartAsync(PlaybackProgressEventArgs e)
    {
        var item = e.Item;
        var session = e.Session;
        if (item is null || session is null || string.IsNullOrEmpty(item.Path))
        {
            return;
        }

        var kind = ResolveStubKind(item.Path);
        if (kind is null)
        {
            return;
        }

        var tmdbId = ResolveTmdbId(item, kind.Value);
        if (tmdbId is null)
        {
            _logger.LogWarning("Stub '{Path}' has no resolvable TMDb id - cannot request it.", item.Path);
            return;
        }

        var userId = session.UserId;
        if (!_rateLimiter.ShouldAllow(userId, kind.Value, tmdbId.Value))
        {
            _logger.LogDebug("{Kind} {TmdbId}: rate-limited for user {UserId}, skipping.", kind, tmdbId, userId);
            return;
        }

        var delaySeconds = Plugin.Instance!.Configuration.PlaybackStopDelaySeconds;
        if (delaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds)).ConfigureAwait(false);
        }

        await _sessionManager
            .SendPlaystateCommand(session.Id, session.Id, new PlaystateRequest { Command = PlaystateCommand.Stop }, CancellationToken.None)
            .ConfigureAwait(false);

        var userLookup = await _jellyseerrClient.FindUserByJellyfinIdAsync(userId, CancellationToken.None).ConfigureAwait(false);
        if (!userLookup.Found)
        {
            await SendMessageAsync(
                    session.Id,
                    "Not linked to Jellyseerr",
                    userLookup.ErrorMessage is null
                        ? "Ask an admin to import your account into Jellyseerr, then try again."
                        : $"Could not reach Jellyseerr: {userLookup.ErrorMessage}")
                .ConfigureAwait(false);
            return;
        }

        var statusResult = kind.Value == MediaKind.Series
            ? await _jellyseerrClient.GetTvStatusAsync(tmdbId.Value, CancellationToken.None).ConfigureAwait(false)
            : await _jellyseerrClient.GetMovieStatusAsync(tmdbId.Value, CancellationToken.None).ConfigureAwait(false);
        if (!statusResult.Success)
        {
            await SendMessageAsync(session.Id, "Request failed", $"Could not check Jellyseerr: {statusResult.ErrorMessage}").ConfigureAwait(false);
            return;
        }

        var action = RequestDecision.Decide(statusResult.Status);
        if (action == RequestAction.AlreadyPending)
        {
            _logger.LogInformation("{Kind} {TmdbId}: already pending/processing in Jellyseerr, no new request created.", kind, tmdbId);
            await SendMessageAsync(session.Id, "Already requested", "This title has already been requested.").ConfigureAwait(false);
            return;
        }

        if (action == RequestAction.AlreadyAvailable)
        {
            _logger.LogInformation("{Kind} {TmdbId}: already available in Jellyseerr, no new request created.", kind, tmdbId);
            await SendMessageAsync(session.Id, "Already available", "This title is already available - the library should update shortly.").ConfigureAwait(false);
            return;
        }

        var config = Plugin.Instance!.Configuration;
        var (serverId, profileId) = await ResolveRequestTargetAsync(kind.Value, item, e.MediaSourceId, config).ConfigureAwait(false);

        var createResult = await _jellyseerrClient
            .CreateRequestAsync(tmdbId.Value, kind.Value, userLookup.JellyseerrUserId!.Value, serverId, profileId, CancellationToken.None)
            .ConfigureAwait(false);

        if (!createResult.Success)
        {
            _logger.LogWarning("{Kind} {TmdbId}: Jellyseerr request creation failed: {Error}", kind, tmdbId, createResult.ErrorMessage);
            await SendMessageAsync(session.Id, "Request failed", $"Could not create the request: {createResult.ErrorMessage}").ConfigureAwait(false);
            return;
        }

        _logger.LogInformation("{Kind} {TmdbId}: Jellyseerr request created for user {UserId}, status {Status}.", kind, tmdbId, userId, createResult.Status);
        await SendMessageAsync(session.Id, "Requested", "Your request has been submitted and is awaiting approval.").ConfigureAwait(false);
    }

    private void HandlePlaybackStopped(PlaybackStopEventArgs e)
    {
        var item = e.Item;
        var session = e.Session;
        if (item is null || session is null || string.IsNullOrEmpty(item.Path))
        {
            return;
        }

        if (ResolveStubKind(item.Path) is null)
        {
            return;
        }

        var user = _userManager.GetUserById(session.UserId);
        if (user is null)
        {
            return;
        }

        var userData = _userDataManager.GetUserData(user, item);
        if (userData is null)
        {
            return;
        }

        userData.PlaybackPositionTicks = 0;
        userData.Played = false;
        userData.LastPlayedDate = null;
        _userDataManager.SaveUserData(user, item, userData, UserDataSaveReason.UpdateUserData, CancellationToken.None);
        _logger.LogDebug("Reset user data for stub '{Path}' (user {UserId}) so it won't appear in Continue Watching/Next Up.", item.Path, session.UserId);
    }

    private static MediaKind? ResolveStubKind(string itemPath)
    {
        if (StubPath.IsUnderRoot(itemPath, StubPath.GetRootPath()))
        {
            return MediaKind.Movie;
        }

        if (StubPath.IsUnderRoot(itemPath, StubPath.GetTvRootPath()))
        {
            return MediaKind.Series;
        }

        return null;
    }

    private int? ResolveTmdbId(BaseItem item, MediaKind kind)
    {
        if (kind == MediaKind.Series)
        {
            // The played item is the dummy episode, whose OWN provider id (if it ever had one)
            // would be a different, per-episode TMDb id - the request must target the series, so
            // this reads the parent Series' provider id instead, never the episode's own.
            if (item is Episode episode
                && _libraryManager.GetItemById<Series>(episode.SeriesId) is Series series
                && series.TryGetProviderId(MetadataProvider.Tmdb, out var seriesTmdbIdString)
                && int.TryParse(seriesTmdbIdString, out var seriesTmdbId))
            {
                return seriesTmdbId;
            }

            return StubFileNaming.TryParseTmdbId(Path.GetFileName(item.Path ?? string.Empty));
        }

        if (item.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbIdString) && int.TryParse(tmdbIdString, out var id))
        {
            return id;
        }

        return StubFileNaming.TryParseTmdbId(Path.GetFileName(item.Path ?? string.Empty));
    }

    /// <summary>
    /// Resolves which Radarr/Sonarr server+profile to request against: the admin's default fields,
    /// unless the played item has multiple alternate versions (the quality picker,
    /// docs/decisions.md "M5a spike finding") and the user's chosen one - read from
    /// <paramref name="mediaSourceId"/>, cross-referenced against the item's own
    /// <see cref="BaseItem.GetMediaSources"/> to recover the version's display name (never
    /// <c>Item.Path</c>, which never varies by version) - matches a currently-configured extra
    /// profile. Falls back to the default fields whenever that match can't be made (no extras
    /// configured, only one version, a stale/renamed profile, or a live Jellyseerr failure) rather
    /// than failing the request outright.
    /// </summary>
    private async Task<(int? ServerId, int? ProfileId)> ResolveRequestTargetAsync(MediaKind kind, BaseItem item, string? mediaSourceId, PluginConfiguration config)
    {
        var (defaultServerId, defaultProfileId, extraProfiles) = kind == MediaKind.Series
            ? (config.JellyseerrSonarrServerId, config.JellyseerrSonarrProfileId, config.ExtraTvProfiles)
            : (config.JellyseerrRadarrServerId, config.JellyseerrRadarrProfileId, config.ExtraMovieProfiles);

        if (extraProfiles.Count == 0 || string.IsNullOrEmpty(mediaSourceId) || defaultServerId is null || defaultProfileId is null)
        {
            return (defaultServerId, defaultProfileId);
        }

        var mediaSources = item.GetMediaSources(enablePathSubstitution: false);
        if (mediaSources.Count < 2)
        {
            return (defaultServerId, defaultProfileId);
        }

        var chosen = mediaSources.FirstOrDefault(m => m.Id == mediaSourceId);
        if (chosen is null || string.IsNullOrEmpty(chosen.Name))
        {
            return (defaultServerId, defaultProfileId);
        }

        var candidates = new List<RequestProfile> { new() { ServerId = defaultServerId.Value, ProfileId = defaultProfileId.Value } };
        candidates.AddRange(extraProfiles);

        var result = await _requestProfileResolver.ResolveAsync(kind, candidates, CancellationToken.None).ConfigureAwait(false);
        if (!result.Success)
        {
            _logger.LogWarning(
                "{Kind}: failed to resolve request profiles from Jellyseerr while handling playback ({Error}) - requesting with the default profile instead.",
                kind,
                result.ErrorMessage);
            return (defaultServerId, defaultProfileId);
        }

        var matched = result.Profiles.FirstOrDefault(p => p.Label == chosen.Name);
        if (matched is null)
        {
            _logger.LogWarning(
                "{Kind}: played version '{Name}' does not match any currently configured request profile - requesting with the default profile instead.",
                kind,
                chosen.Name);
            return (defaultServerId, defaultProfileId);
        }

        return (matched.ServerId, matched.ProfileId);
    }

    private Task SendMessageAsync(string sessionId, string header, string text) =>
        _sessionManager.SendMessageCommand(
            sessionId,
            sessionId,
            new MessageCommand { Header = header, Text = text, TimeoutMs = 5000 },
            CancellationToken.None);
}
