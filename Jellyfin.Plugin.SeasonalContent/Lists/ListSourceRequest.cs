namespace Jellyfin.Plugin.SeasonalContent.Lists;

/// <summary>
/// Everything a list source needs to fetch one configured list's items.
/// </summary>
/// <param name="ListId">The plugin's own stable id for this configured list (becomes <see cref="ListItem.SourceListId"/>).</param>
/// <param name="Username">Source-specific account/owner identifier (for MDBList: the list owner's username).</param>
/// <param name="Slug">Source-specific list identifier (for MDBList: the list slug).</param>
/// <param name="ApiKey">Source-specific credential. Never logged.</param>
/// <param name="Limit">Requested page size; a source may clamp this to its own valid range.</param>
public sealed record ListSourceRequest(string ListId, string Username, string Slug, string ApiKey, int Limit);
