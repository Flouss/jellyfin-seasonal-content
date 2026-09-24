using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SeasonalContent.Setup;

/// <summary>
/// One outcome of a <see cref="ILibrarySetupService.SetupAsync"/> call.
/// </summary>
/// <param name="Created"><see langword="true"/> if this step created a library; <see langword="false"/> if one already existed.</param>
/// <param name="Message">A human-readable outcome description, shown on the config page.</param>
public sealed record LibrarySetupStepResult(bool Created, string Message);

/// <summary>
/// The outcome of a full <see cref="ILibrarySetupService.SetupAsync"/> call.
/// </summary>
/// <param name="StubLibrary">The stub (Movies-type) library step's outcome.</param>
/// <param name="CollectionsLibrary">The Collections (boxsets-type) library step's outcome.</param>
public sealed record LibrarySetupResult(LibrarySetupStepResult StubLibrary, LibrarySetupStepResult CollectionsLibrary);

/// <summary>
/// Creates the libraries this plugin needs to work correctly (docs/implementation-plan.md's
/// stub-library setup note, and the Collections library needed for BoxSets to be browsable -
/// see docs/progress-log.md's 2026-09-24 entry), if they don't already exist. Idempotent: safe
/// to call repeatedly.
/// </summary>
public interface ILibrarySetupService
{
    /// <summary>
    /// Creates whichever of the stub library and the Collections library don't already exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What was created vs. already present.</returns>
    Task<LibrarySetupResult> SetupAsync(CancellationToken cancellationToken);
}
