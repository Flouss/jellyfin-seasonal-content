using System.Collections.Generic;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <summary>
/// Reads and writes the stub root on disk. A thin IO shell around <see cref="StubReconciler"/>'s
/// pure decisions - verified live (docs/implementation-plan.md §8), not unit tested, same
/// convention as <c>Ownership.LibraryMovieCatalog</c>.
/// </summary>
public interface IStubFileIoExecutor
{
    /// <summary>
    /// Lists every stub file currently in the stub root, with its content.
    /// </summary>
    /// <param name="stubRootPath">The stub root.</param>
    /// <returns>Every existing stub file.</returns>
    IReadOnlyList<ExistingStubFile> ListExistingFiles(string stubRootPath);

    /// <summary>
    /// Applies a reconcile plan: deletes and (re)writes the files it names. Every touched path is
    /// re-checked with <see cref="Ownership.StubPath.IsUnderRoot"/> before it is touched - the
    /// filename itself already can't contain a path separator (<see cref="StubFileNaming"/>), but
    /// this is the last line of defense per docs/implementation-plan.md §3.2's "never delete
    /// anything outside [the stub root]" invariant.
    /// </summary>
    /// <param name="plan">The plan to apply.</param>
    /// <param name="stubRootPath">The stub root. Created if it doesn't exist yet.</param>
    void Apply(StubReconcilePlan plan, string stubRootPath);
}
