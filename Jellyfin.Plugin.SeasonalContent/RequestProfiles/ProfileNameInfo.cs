namespace Jellyfin.Plugin.SeasonalContent.RequestProfiles;

/// <summary>
/// One request profile with its live-fetched display names, ready to be turned into a stub version
/// label by <see cref="RequestProfileLabeler"/>.
/// </summary>
/// <param name="ServerId">The Radarr/Sonarr server's Jellyseerr-assigned id.</param>
/// <param name="ProfileId">The quality profile's id on that server.</param>
/// <param name="ProfileName">The profile's current display name in Jellyseerr/Radarr/Sonarr.</param>
/// <param name="ServerName">The server's current display name, used only to disambiguate two
/// profiles that share a name across different servers.</param>
public sealed record ProfileNameInfo(int ServerId, int ProfileId, string ProfileName, string ServerName);

/// <summary>
/// One profile after labeling: a filesystem-safe, unique-within-this-call version label, plus the
/// server/profile ids to request against once a user picks that version.
/// </summary>
/// <param name="Label">The sanitized, disambiguated label - becomes both the stub file name suffix
/// and (via Jellyfin's alternate-version grouping) the name shown in the client's version picker.</param>
/// <param name="ServerId">The Radarr/Sonarr server's Jellyseerr-assigned id.</param>
/// <param name="ProfileId">The quality profile's id on that server.</param>
public sealed record ResolvedRequestProfile(string Label, int ServerId, int ProfileId);
