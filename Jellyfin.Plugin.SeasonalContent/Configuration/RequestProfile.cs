namespace Jellyfin.Plugin.SeasonalContent.Configuration;

/// <summary>
/// One admin-selected extra Radarr/Sonarr server+profile pair, offered to users as a quality
/// version alongside the existing default fields (docs/decisions.md "M5a spike finding"). Plain
/// mutable class, not a record - persisted inside <see cref="PluginConfiguration"/>, which
/// round-trips through Jellyfin's <c>IXmlSerializer</c> (same reason as <see cref="SeasonalListConfig"/>).
/// </summary>
public class RequestProfile
{
    /// <summary>
    /// Gets or sets the Radarr/Sonarr server's Jellyseerr-assigned id.
    /// </summary>
    public int ServerId { get; set; }

    /// <summary>
    /// Gets or sets the quality profile's id on that server.
    /// </summary>
    public int ProfileId { get; set; }
}
