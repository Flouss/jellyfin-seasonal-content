using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SeasonalContent.Configuration;
using Jellyfin.Plugin.SeasonalContent.Jellyseerr;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeasonalContent.RequestProfiles;

/// <inheritdoc />
public sealed class RequestProfileResolver : IRequestProfileResolver
{
    private readonly IJellyseerrClient _jellyseerrClient;
    private readonly ILogger<RequestProfileResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestProfileResolver"/> class.
    /// </summary>
    /// <param name="jellyseerrClient">Instance of the <see cref="IJellyseerrClient"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public RequestProfileResolver(IJellyseerrClient jellyseerrClient, ILogger<RequestProfileResolver> logger)
    {
        _jellyseerrClient = jellyseerrClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RequestProfileResolveResult> ResolveAsync(MediaKind kind, IReadOnlyList<RequestProfile> profiles, CancellationToken cancellationToken)
    {
        if (profiles.Count == 0)
        {
            return new RequestProfileResolveResult(true, [], null);
        }

        var (success, error, infos) = kind == MediaKind.Series
            ? await ResolveSeriesProfilesAsync(profiles, cancellationToken).ConfigureAwait(false)
            : await ResolveMovieProfilesAsync(profiles, cancellationToken).ConfigureAwait(false);

        return success
            ? new RequestProfileResolveResult(true, RequestProfileLabeler.BuildLabels(infos), null)
            : new RequestProfileResolveResult(false, [], error);
    }

    private async Task<(bool Success, string? Error, List<ProfileNameInfo> Infos)> ResolveMovieProfilesAsync(
        IReadOnlyList<RequestProfile> profiles,
        CancellationToken cancellationToken)
    {
        var serversResult = await _jellyseerrClient.GetRadarrServersAsync(cancellationToken).ConfigureAwait(false);
        if (!serversResult.Success)
        {
            return (false, serversResult.ErrorMessage, []);
        }

        var serverNames = serversResult.Servers!.ToDictionary(s => s.Id, s => s.Name);
        var profileNamesByServer = new Dictionary<int, Dictionary<int, string>>();

        foreach (var serverId in profiles.Select(p => p.ServerId).Distinct())
        {
            var profilesResult = await _jellyseerrClient.GetRadarrProfilesAsync(serverId, cancellationToken).ConfigureAwait(false);
            if (!profilesResult.Success)
            {
                return (false, profilesResult.ErrorMessage, []);
            }

            profileNamesByServer[serverId] = profilesResult.Profiles!.ToDictionary(p => p.Id, p => p.Name);
        }

        return (true, null, BuildInfos(profiles, serverNames, profileNamesByServer, "Movie"));
    }

    private async Task<(bool Success, string? Error, List<ProfileNameInfo> Infos)> ResolveSeriesProfilesAsync(
        IReadOnlyList<RequestProfile> profiles,
        CancellationToken cancellationToken)
    {
        var serversResult = await _jellyseerrClient.GetSonarrServersAsync(cancellationToken).ConfigureAwait(false);
        if (!serversResult.Success)
        {
            return (false, serversResult.ErrorMessage, []);
        }

        var serverNames = serversResult.Servers!.ToDictionary(s => s.Id, s => s.Name);
        var profileNamesByServer = new Dictionary<int, Dictionary<int, string>>();

        foreach (var serverId in profiles.Select(p => p.ServerId).Distinct())
        {
            var profilesResult = await _jellyseerrClient.GetSonarrProfilesAsync(serverId, cancellationToken).ConfigureAwait(false);
            if (!profilesResult.Success)
            {
                return (false, profilesResult.ErrorMessage, []);
            }

            profileNamesByServer[serverId] = profilesResult.Profiles!.ToDictionary(p => p.Id, p => p.Name);
        }

        return (true, null, BuildInfos(profiles, serverNames, profileNamesByServer, "TV"));
    }

    private List<ProfileNameInfo> BuildInfos(
        IReadOnlyList<RequestProfile> profiles,
        Dictionary<int, string> serverNames,
        Dictionary<int, Dictionary<int, string>> profileNamesByServer,
        string kindLabel)
    {
        var infos = new List<ProfileNameInfo>(profiles.Count);

        foreach (var profile in profiles)
        {
            var serverName = serverNames.TryGetValue(profile.ServerId, out var name)
                ? name
                : string.Format(CultureInfo.InvariantCulture, "Server {0}", profile.ServerId);

            if (profileNamesByServer.TryGetValue(profile.ServerId, out var profileMap)
                && profileMap.TryGetValue(profile.ProfileId, out var profileName))
            {
                infos.Add(new ProfileNameInfo(profile.ServerId, profile.ProfileId, profileName, serverName));
            }
            else
            {
                _logger.LogWarning(
                    "{Kind} request profile server {ServerId} profile {ProfileId} no longer exists in Jellyseerr - skipping this version for this sync.",
                    kindLabel,
                    profile.ServerId,
                    profile.ProfileId);
            }
        }

        return infos;
    }
}
