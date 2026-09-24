using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <summary>
/// Builds the deterministic stub file name for a list item, per docs/implementation-plan.md §3.2:
/// <c>{sanitised Title} ({Year}) [tmdbid-{id}].strm</c>. Deterministic on purpose - the reconcile
/// algorithm (<see cref="StubReconciler"/>) diffs desired-vs-existing file names directly,
/// with no separate id-to-filename index to keep in sync.
/// </summary>
public static class StubFileNaming
{
    // Windows-reserved filename characters plus control characters. Titles come from the MDBList
    // API - an external source - so this also has to stop a crafted title (containing "/", "\" or
    // "..") from letting the resulting file name escape the stub root once combined into a path.
    private static readonly char[] InvalidChars =
    [
        '<', '>', ':', '"', '/', '\\', '|', '?', '*',
        .. Enumerable.Range(0, 32).Select(c => (char)c)
    ];

    /// <summary>
    /// Builds the sanitised <c>{Title} ({Year})</c> prefix shared by <see cref="BuildFileName"/> and
    /// <see cref="BuildTitleFolderName"/> - extracted so both the flat single-file layout and the
    /// multi-version folder layout (docs/decisions.md "M5a spike finding") derive the exact same
    /// title text instead of two independently-maintained copies.
    /// </summary>
    /// <param name="title">Display title.</param>
    /// <param name="year">Release year, when known.</param>
    /// <returns>The prefix, without the trailing <c>[tmdbid-X]</c> marker.</returns>
    public static string BuildNamePrefix(string title, int? year)
    {
        var sanitisedTitle = SanitiseForFileName(title).Trim();

        var pieces = new List<string>();
        if (!string.IsNullOrEmpty(sanitisedTitle))
        {
            pieces.Add(sanitisedTitle);
        }

        if (year.HasValue)
        {
            pieces.Add(string.Format(CultureInfo.InvariantCulture, "({0})", year.Value));
        }

        return string.Join(' ', pieces);
    }

    /// <summary>
    /// Builds the stub file name for one item.
    /// </summary>
    /// <param name="title">Display title.</param>
    /// <param name="year">Release year, when known.</param>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <returns>The file name, including the <c>.strm</c> extension.</returns>
    public static string BuildFileName(string title, int? year, int tmdbId) =>
        string.Format(CultureInfo.InvariantCulture, "{0} [tmdbid-{1}].strm", BuildNamePrefix(title, year), tmdbId).TrimStart();

    /// <summary>
    /// Builds the per-title folder name used by the multi-version stub layout (one folder per
    /// title, one file per quality version inside it) - the same <c>{Title} ({Year}) [tmdbid-X]</c>
    /// text <see cref="BuildFileName"/> uses, minus the <c>.strm</c> extension.
    /// </summary>
    /// <param name="title">Display title.</param>
    /// <param name="year">Release year, when known.</param>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <returns>The folder name (no path separators, no extension).</returns>
    public static string BuildTitleFolderName(string title, int? year, int tmdbId) =>
        string.Format(CultureInfo.InvariantCulture, "{0} [tmdbid-{1}]", BuildNamePrefix(title, year), tmdbId).TrimStart();

    /// <summary>
    /// Builds one version's file name inside a multi-version title folder built by
    /// <see cref="BuildTitleFolderName"/>: <c>{folder name} - {label}.strm</c>. Jellyfin groups every
    /// file in the same folder as alternate versions of one item, with each file's suffix (here,
    /// <paramref name="label"/>) becoming that version's display name in the native version picker
    /// (docs/decisions.md "M5a spike finding") - so <paramref name="label"/> must already be
    /// filesystem-safe and unique within the folder (see <see cref="RequestProfiles.RequestProfileLabeler"/>).
    /// </summary>
    /// <param name="title">Display title.</param>
    /// <param name="year">Release year, when known.</param>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <param name="label">The version's already-sanitised, already-disambiguated label.</param>
    /// <returns>The file name, including the <c>.strm</c> extension.</returns>
    public static string BuildVersionFileName(string title, int? year, int tmdbId, string label) =>
        string.Format(CultureInfo.InvariantCulture, "{0} - {1}.strm", BuildTitleFolderName(title, year, tmdbId), label);

    private static readonly Regex TmdbIdMarker = new(@"\[tmdbid-(\d+)\]", RegexOptions.Compiled);

    /// <summary>
    /// Parses the TMDb id back out of a stub file name built by <see cref="BuildFileName"/>, for
    /// the case where an item's TMDb provider id isn't populated yet (docs/implementation-plan.md
    /// §3.5 step 2's fallback).
    /// </summary>
    /// <param name="fileName">The file name (not a full path).</param>
    /// <returns>The TMDb id, or <see langword="null"/> if the name has no <c>[tmdbid-X]</c> marker.</returns>
    public static int? TryParseTmdbId(string fileName)
    {
        var match = TmdbIdMarker.Match(fileName);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }

    /// <summary>
    /// Strips Windows-reserved/control characters from a title so it's safe to use as (part of) a
    /// file or folder name - the single source for this, shared with <see cref="TvStubFileNaming"/>
    /// (rule: don't re-derive a security-relevant sanitizer per call site).
    /// </summary>
    /// <param name="title">The raw title, from an external source (the MDBList API).</param>
    /// <returns>The sanitised title. May be empty if every character was invalid.</returns>
    public static string SanitiseForFileName(string title)
    {
        var builder = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            if (!InvalidChars.Contains(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
