namespace Jellyfin.Plugin.SeasonalContent.Jellyseerr;

/// <summary>
/// Mirrors Jellyseerr's <c>MediaRequest.status</c> numeric values exactly (verified against the
/// real published OpenAPI spec - see docs/decisions.md, "M5 API surface").
/// </summary>
public enum JellyseerrRequestStatus
{
    /// <summary>Created, awaiting approval - what Option A+ (docs/implementation-plan.md §5) expects.</summary>
    PendingApproval = 1,

    /// <summary>Approved (auto-approved, or by the requesting user's own elevated permissions).</summary>
    Approved = 2,

    /// <summary>Declined.</summary>
    Declined = 3
}
