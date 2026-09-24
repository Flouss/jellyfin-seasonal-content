using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <summary>
/// Builds the deterministic stub folder/file names for a TV series stub, per
/// docs/rename-tv-globalkey-plan.md §3: <c>{sanitised Title} ({Year}) [tmdbid-{id}]/Season 01/
/// {sanitised Title} S01E01 [tmdbid-{id}].strm</c> - one dummy first episode is enough for Jellyfin
/// to identify and scan the series, exactly as one file is enough for a movie stub. The tmdb tag is
/// embedded in <b>both</b> the series folder name and the episode file name, so
/// <see cref="StubFileNaming.TryParseTmdbId"/> (already generic - it just regex-matches the
/// <c>[tmdbid-X]</c> marker) works unmodified as the playback interceptor's filename-fallback
/// resolution for TV stubs too, with no TV-specific parsing needed.
/// </summary>
public static class TvStubFileNaming
{
    private const string SeasonFolderName = "Season 01";

    /// <summary>
    /// Builds the stub series folder name for one show.
    /// </summary>
    /// <param name="title">Display title.</param>
    /// <param name="year">Release year, when known.</param>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <returns>The folder name (no path separators, no extension).</returns>
    public static string BuildSeriesFolderName(string title, int? year, int tmdbId) =>
        BuildNamePrefix(title, year, tmdbId);

    /// <summary>
    /// Builds the dummy first episode's path, relative to the series folder.
    /// </summary>
    /// <param name="title">Display title.</param>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <returns>A relative path of the form <c>Season 01/{title} S01E01 [tmdbid-X].strm</c>.</returns>
    public static string BuildEpisodeRelativePath(string title, int tmdbId)
    {
        var fileName = string.Format(CultureInfo.InvariantCulture, "{0} [tmdbid-{1}].strm", BuildEpisodeNamePrefix(title), tmdbId);
        return Path.Combine(SeasonFolderName, fileName);
    }

    /// <summary>
    /// Builds one version's dummy first episode path, for the multi-version TV stub layout (one
    /// series folder, one dummy episode file per quality version inside it - the TV counterpart of
    /// <see cref="StubFileNaming.BuildVersionFileName"/>, docs/decisions.md "M5a spike finding").
    /// </summary>
    /// <param name="title">Display title.</param>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <param name="label">The version's already-sanitised, already-disambiguated label.</param>
    /// <returns>A relative path of the form <c>Season 01/{title} S01E01 [tmdbid-X] - {label}.strm</c>.</returns>
    public static string BuildVersionedEpisodeRelativePath(string title, int tmdbId, string label)
    {
        var fileName = string.Format(CultureInfo.InvariantCulture, "{0} [tmdbid-{1}] - {2}.strm", BuildEpisodeNamePrefix(title), tmdbId, label);
        return Path.Combine(SeasonFolderName, fileName);
    }

    private static string BuildEpisodeNamePrefix(string title)
    {
        var sanitisedTitle = StubFileNaming.SanitiseForFileName(title).Trim();
        return string.IsNullOrEmpty(sanitisedTitle) ? "S01E01" : sanitisedTitle + " S01E01";
    }

    /// <summary>
    /// Whether <paramref name="folderName"/> is safe to combine directly under the TV stub root -
    /// no path separator, and not <c>..</c> or another parent-directory escape (rule: this must
    /// hold before a <b>recursive</b> delete ever touches the resulting path -
    /// <see cref="TvStubFileIoExecutor"/> is the one caller, guarding both delete and write).
    /// Deliberately does not depend on where the folder name came from - it's re-checked
    /// structurally every time, not trusted because <see cref="BuildSeriesFolderName"/> already
    /// sanitises its input.
    /// </summary>
    /// <param name="folderName">A candidate series folder name.</param>
    /// <returns><see langword="true"/> if the name is a plain, single path segment.</returns>
    public static bool IsSafeFolderName(string folderName) =>
        !string.IsNullOrEmpty(folderName)
        && folderName != "."
        && folderName != ".."
        && !folderName.Contains('/')
        && !folderName.Contains('\\')
        && folderName == Path.GetFileName(folderName);

    private static string BuildNamePrefix(string title, int? year, int tmdbId)
    {
        var sanitisedTitle = StubFileNaming.SanitiseForFileName(title).Trim();

        var pieces = new List<string>();
        if (!string.IsNullOrEmpty(sanitisedTitle))
        {
            pieces.Add(sanitisedTitle);
        }

        if (year.HasValue)
        {
            pieces.Add(string.Format(CultureInfo.InvariantCulture, "({0})", year.Value));
        }

        var namePrefix = string.Join(' ', pieces);
        return string.Format(CultureInfo.InvariantCulture, "{0} [tmdbid-{1}]", namePrefix, tmdbId).TrimStart();
    }
}
