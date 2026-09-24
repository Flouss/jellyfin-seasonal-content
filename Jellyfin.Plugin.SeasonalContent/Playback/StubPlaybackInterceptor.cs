using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Jellyseerr;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Jellyfin.Plugin.SeasonalContent.Stubs;
using MediaBrowser.Controller.Entities;
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
/// </summary>
public sealed class StubPlaybackInterceptor : IHostedService
{
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(30);

    private readonly ISessionManager _sessionManager;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IJellyseerrClient _jellyseerrClient;
    private readonly PlaybackRateLimiter _rateLimiter;
    private readonly ILogger<StubPlaybackInterceptor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubPlaybackInterceptor"/> class.
    /// </summary>
    /// <param name="sessionManager">Instance of the <see cref="ISessionManager"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="jellyseerrClient">Instance of the <see cref="IJellyseerrClient"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public StubPlaybackInterceptor(
        ISessionManager sessionManager,
        IUserManager userManager,
        IUserDataManager userDataManager,
        IJellyseerrClient jellyseerrClient,
        ILogger<StubPlaybackInterceptor> logger)
    {
        _sessionManager = sessionManager;
        _userManager = userManager;
        _userDataManager = userDataManager;
        _jellyseerrClient = jellyseerrClient;
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

        var stubRootPath = Path.Combine(Plugin.Instance!.DataFolderPath, "movies_seasonal");
        if (!StubPath.IsUnderRoot(item.Path, stubRootPath))
        {
            return;
        }

        var tmdbId = ResolveTmdbId(item);
        if (tmdbId is null)
        {
            _logger.LogWarning("Stub '{Path}' has no resolvable TMDb id - cannot request it.", item.Path);
            return;
        }

        var userId = session.UserId;
        if (!_rateLimiter.ShouldAllow(userId, tmdbId.Value))
        {
            _logger.LogDebug("TMDb {TmdbId}: rate-limited for user {UserId}, skipping.", tmdbId, userId);
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

        var statusResult = await _jellyseerrClient.GetMovieStatusAsync(tmdbId.Value, CancellationToken.None).ConfigureAwait(false);
        if (!statusResult.Success)
        {
            await SendMessageAsync(session.Id, "Request failed", $"Could not check Jellyseerr: {statusResult.ErrorMessage}").ConfigureAwait(false);
            return;
        }

        var action = RequestDecision.Decide(statusResult.Status);
        if (action == RequestAction.AlreadyPending)
        {
            _logger.LogInformation("TMDb {TmdbId}: already pending/processing in Jellyseerr, no new request created.", tmdbId);
            await SendMessageAsync(session.Id, "Already requested", "This title has already been requested.").ConfigureAwait(false);
            return;
        }

        if (action == RequestAction.AlreadyAvailable)
        {
            _logger.LogInformation("TMDb {TmdbId}: already available in Jellyseerr, no new request created.", tmdbId);
            await SendMessageAsync(session.Id, "Already available", "This title is already available - the library should update shortly.").ConfigureAwait(false);
            return;
        }

        var config = Plugin.Instance!.Configuration;
        var createResult = await _jellyseerrClient
            .CreateRequestAsync(tmdbId.Value, userLookup.JellyseerrUserId!.Value, config.JellyseerrRadarrServerId, config.JellyseerrRadarrProfileId, CancellationToken.None)
            .ConfigureAwait(false);

        if (!createResult.Success)
        {
            _logger.LogWarning("TMDb {TmdbId}: Jellyseerr request creation failed: {Error}", tmdbId, createResult.ErrorMessage);
            await SendMessageAsync(session.Id, "Request failed", $"Could not create the request: {createResult.ErrorMessage}").ConfigureAwait(false);
            return;
        }

        _logger.LogInformation("TMDb {TmdbId}: Jellyseerr request created for user {UserId}, status {Status}.", tmdbId, userId, createResult.Status);
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

        var stubRootPath = Path.Combine(Plugin.Instance!.DataFolderPath, "movies_seasonal");
        if (!StubPath.IsUnderRoot(item.Path, stubRootPath))
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

    private static int? ResolveTmdbId(BaseItem item)
    {
        if (item.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbIdString) && int.TryParse(tmdbIdString, out var id))
        {
            return id;
        }

        return StubFileNaming.TryParseTmdbId(Path.GetFileName(item.Path ?? string.Empty));
    }

    private Task SendMessageAsync(string sessionId, string header, string text) =>
        _sessionManager.SendMessageCommand(
            sessionId,
            sessionId,
            new MessageCommand { Header = header, Text = text, TimeoutMs = 5000 },
            CancellationToken.None);
}
