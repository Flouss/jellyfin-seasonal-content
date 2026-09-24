namespace Jellyfin.Plugin.SeasonalContent.Lists;

/// <summary>
/// Which kind of title a <see cref="ListItem"/> is. TMDb ids are <b>not</b> unique across kinds -
/// movie id 1399 and show id 1399 are different titles - so every lookup keyed on a TMDb id must
/// also carry this (docs/rename-tv-globalkey-plan.md "correctness constraint").
/// </summary>
public enum MediaKind
{
    /// <summary>A movie.</summary>
    Movie,

    /// <summary>A TV series.</summary>
    Series
}
