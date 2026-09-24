using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SeasonalContent.Sync;

/// <summary>
/// Finds the Jellyfin library backing the stub root and scans it so newly-written stubs become
/// real <c>BaseItem</c>s, per docs/implementation-plan.md §3.2. A thin IO shell, verified live
/// (§8), not unit tested.
/// </summary>
public interface IStubLibraryScanner
{
    /// <summary>
    /// Scans the library at <paramref name="stubRootPath"/>, if one is configured.
    /// </summary>
    /// <param name="stubRootPath">The stub root.</param>
    /// <param name="progress">Progress reporter, forwarded to the underlying scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if a matching library was found and scanned; <see langword="false"/>
    /// if no library is configured at that path yet (logged as an actionable error - the admin needs
    /// to add it).</returns>
    Task<bool> ScanAsync(string stubRootPath, IProgress<double> progress, CancellationToken cancellationToken);
}
