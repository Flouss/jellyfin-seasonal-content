using System;

namespace Jellyfin.Plugin.SeasonalContent.Configuration;

/// <summary>
/// One configured MDBList list, persisted in <see cref="PluginConfiguration"/>. Plain mutable
/// class, not a record: <see cref="PluginConfiguration"/> round-trips through Jellyfin's
/// <c>IXmlSerializer</c>, which needs a public parameterless constructor and settable properties.
/// </summary>
public class SeasonalListConfig
{
    /// <summary>
    /// Gets or sets the plugin's own stable id for this list. Never reused - it is how a saved
    /// config update recognizes "this is the same list" across renames, and how the BoxSet id in
    /// <see cref="CollectionId"/> survives a rename (docs/implementation-plan.md §3.7, §4).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this list is included in sync. Disabling a list
    /// (with <see cref="PluginConfiguration.RemoveCollectionWhenListDisabled"/> on, the default)
    /// removes its BoxSet on the next sync.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the display name. Becomes the BoxSet's name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MDBList list owner's username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MDBList list slug.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MDBList API key. <b>Legacy only</b>: superseded by
    /// <see cref="PluginConfiguration.MdbListApiKey"/> (one key for every list, instead of one per
    /// list). Kept here - not deleted - only so <c>IXmlSerializer</c> still deserializes an old
    /// saved config's key before <see cref="ConfigMigration.MigrateMdbListApiKey"/> runs and clears
    /// it; nothing in the sync pipeline reads this field anymore.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested page size. Clamped to 1-500 on save; <see cref="Lists.MdbList.MdbListSource"/>
    /// clamps again at fetch time regardless.
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Gets or sets the BoxSet id the plugin created for this list, once it exists. Written by the
    /// sync task, never shown for editing - not user input.
    /// </summary>
    public Guid? CollectionId { get; set; }
}
