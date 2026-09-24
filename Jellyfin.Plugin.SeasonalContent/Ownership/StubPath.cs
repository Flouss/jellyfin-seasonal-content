using System;
using System.IO;

namespace Jellyfin.Plugin.SeasonalContent.Ownership;

/// <summary>
/// The one canonical "is this path under the stub root" check, used everywhere that needs it
/// (ownership lookup, playback interceptor, collection builder) per
/// docs/implementation-plan.md §3.3 - a single implementation instead of each call site
/// re-deriving its own, error-prone version (the POC used <c>Path.Contains("jellynext-virtual")</c>,
/// which also matches an unrelated sibling folder with a similar name).
/// </summary>
public static class StubPath
{
    private const string RootFolderName = "movies_seasonal";

    /// <summary>
    /// Computes the configured stub root's full path: <c>Plugin.DataFolderPath/movies_seasonal</c>.
    /// Single-sourced here rather than recomputed at each call site (the sync task, the playback
    /// interceptor, and the config page's path hint all need the same value).
    /// </summary>
    /// <returns>The stub root's full path.</returns>
    public static string GetRootPath() => Path.Combine(Plugin.Instance!.DataFolderPath, RootFolderName);

    /// <summary>
    /// Determines whether <paramref name="itemPath"/> is the stub root itself or lies inside it,
    /// using a normalized full-segment prefix comparison (not a substring match).
    /// </summary>
    /// <param name="itemPath">The item's full path.</param>
    /// <param name="stubRootPath">The configured stub root's full path.</param>
    /// <returns><see langword="true"/> if <paramref name="itemPath"/> is under <paramref name="stubRootPath"/>.</returns>
    public static bool IsUnderRoot(string itemPath, string stubRootPath)
    {
        var normalizedItem = Normalize(itemPath);
        var normalizedRoot = Normalize(stubRootPath);

        if (normalizedItem.Equals(normalizedRoot, StringComparison.Ordinal))
        {
            return false;
        }

        var rootWithSeparator = normalizedRoot.EndsWith('/') ? normalizedRoot : normalizedRoot + "/";
        return normalizedItem.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }

    private static string Normalize(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/');
}
