using System.Collections.Generic;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <summary>
/// Reads and applies a <see cref="MultiVersionStubReconcilePlan"/> against disk - the shared IO
/// shell behind both the movie and TV multi-version ("quality picker") stub layouts. A thin IO
/// shell, verified live rather than unit tested (same convention as
/// <see cref="IStubFileIoExecutor"/>/<see cref="ITvStubFileIoExecutor"/>).
/// </summary>
public interface IMultiVersionStubFileIoExecutor
{
    /// <summary>
    /// Lists every title folder currently under the stub root, and every stub file inside each one
    /// (at any depth, so it also finds TV's season-subfolder files).
    /// </summary>
    /// <param name="stubRootPath">The stub root to scan.</param>
    /// <returns>Every existing title folder found.</returns>
    IReadOnlyList<ExistingVersionedFolder> ListExistingFolders(string stubRootPath);

    /// <summary>
    /// Applies a plan: deletes whole folders no longer desired, and adds/removes individual version
    /// files within folders that are kept.
    /// </summary>
    /// <param name="plan">The plan to apply.</param>
    /// <param name="stubRootPath">The stub root the plan's folder names are relative to.</param>
    void Apply(MultiVersionStubReconcilePlan plan, string stubRootPath);
}
