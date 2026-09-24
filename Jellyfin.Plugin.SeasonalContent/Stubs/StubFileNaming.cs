using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

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
    /// Builds the stub file name for one item.
    /// </summary>
    /// <param name="title">Display title.</param>
    /// <param name="year">Release year, when known.</param>
    /// <param name="tmdbId">The TMDb id.</param>
    /// <returns>The file name, including the <c>.strm</c> extension.</returns>
    public static string BuildFileName(string title, int? year, int tmdbId)
    {
        var sanitisedTitle = Sanitise(title).Trim();

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
        return string.Format(CultureInfo.InvariantCulture, "{0} [tmdbid-{1}].strm", namePrefix, tmdbId).TrimStart();
    }

    private static string Sanitise(string title)
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
