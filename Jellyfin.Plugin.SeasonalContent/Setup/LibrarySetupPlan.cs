using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SeasonalContent.Setup;

/// <summary>
/// Pure "does the right library already exist" checks backing the config page's "Set up
/// libraries" button (docs: the stub library and the Collections library are both things an
/// admin currently has to add by hand). Kept separate from <see cref="LibrarySetupService"/> so
/// the decision logic is unit-testable without a real <c>ILibraryManager</c>.
/// </summary>
public static class LibrarySetupPlan
{
    /// <summary>
    /// Whether any existing library already has a location at exactly <paramref name="stubRootPath"/>.
    /// </summary>
    /// <param name="folders">The server's current virtual folders.</param>
    /// <param name="stubRootPath">The stub root's full path (<see cref="StubPath.GetRootPath"/>).</param>
    /// <returns><see langword="true"/> if a matching library already exists.</returns>
    public static bool StubLibraryExists(IEnumerable<VirtualFolderInfo> folders, string stubRootPath) =>
        folders.Any(f => (f.Locations ?? []).Any(location => StubPath.PathsEqual(location, stubRootPath)));

    /// <summary>
    /// Whether a library of type <see cref="CollectionTypeOptions.boxsets"/> already exists -
    /// the special library that makes BoxSets browsable (lost if it's ever removed, even though
    /// the BoxSets themselves survive - see docs/progress-log.md's 2026-09-24 entry).
    /// </summary>
    /// <param name="folders">The server's current virtual folders.</param>
    /// <returns><see langword="true"/> if a Collections-type library already exists.</returns>
    public static bool CollectionsLibraryExists(IEnumerable<VirtualFolderInfo> folders) =>
        folders.Any(f => f.CollectionType == CollectionTypeOptions.boxsets);
}
