using System;

namespace Jellyfin.Plugin.SeasonalContent.Lists.MdbList;

/// <summary>
/// Thrown when fetching a page from the MDBList API fails. Distinguishing this from "the list is
/// empty" is load-bearing: docs/implementation-plan.md §3.1 requires that a failed fetch never be
/// treated as an empty list, since that combined with stub cleanup would wipe every stub after a
/// single network blip (a confirmed bug in the POC this plugin does not copy).
/// </summary>
public sealed class MdbListFetchException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MdbListFetchException"/> class.
    /// </summary>
    /// <param name="message">A message that must never include the API key.</param>
    /// <param name="innerException">The underlying failure.</param>
    public MdbListFetchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
