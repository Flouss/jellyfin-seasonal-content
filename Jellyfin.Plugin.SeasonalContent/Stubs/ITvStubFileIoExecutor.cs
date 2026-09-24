using System.Collections.Generic;

namespace Jellyfin.Plugin.SeasonalContent.Stubs;

/// <summary>
/// Reads and writes the TV stub root on disk - the TV counterpart of
/// <see cref="IStubFileIoExecutor"/>. A thin IO shell around <see cref="TvStubReconciler"/>'s pure
/// decisions - verified live, not unit tested, same convention as the movie executor.
/// </summary>
public interface ITvStubFileIoExecutor
{
    /// <summary>
    /// Lists every series folder currently in the TV stub root, with its dummy episode's current
    /// content (or <see langword="null"/> if that file is missing).
    /// </summary>
    /// <param name="stubRootPath">The TV stub root.</param>
    /// <returns>Every existing series folder.</returns>
    IReadOnlyList<ExistingTvSeriesFolder> ListExistingSeriesFolders(string stubRootPath);

    /// <summary>
    /// Applies a reconcile plan: recursively deletes series folders no longer desired, and
    /// creates/repairs the dummy episode for every series that needs it. Every touched path is
    /// re-checked with <see cref="Ownership.StubPath.IsUnderRoot"/>, and every folder name is
    /// re-checked to contain no path separators, before it is touched - a folder name can only ever
    /// come from <see cref="TvStubFileNaming.BuildSeriesFolderName"/> (which already strips path
    /// separators from the source title) or from a directory listing of the root itself, but a
    /// <b>recursive</b> delete is high enough blast radius to re-verify anyway rather than trust
    /// that upstream invariant alone (docs/rename-tv-globalkey-plan.md's "never touch outside the
    /// stub root" invariant, same as the movie executor's).
    /// </summary>
    /// <param name="plan">The plan to apply.</param>
    /// <param name="stubRootPath">The TV stub root. Created if it doesn't exist yet.</param>
    void Apply(TvStubReconcilePlan plan, string stubRootPath);
}
