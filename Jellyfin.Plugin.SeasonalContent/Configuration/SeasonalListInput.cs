using System;

namespace Jellyfin.Plugin.SeasonalContent.Configuration;

/// <summary>
/// One list entry as submitted to <c>POST SeasonalContent/Lists</c>. Separate from
/// <see cref="SeasonalListConfig"/> because the wire shape (an optional <see cref="Id"/> meaning
/// "this is an edit of an existing list") differs from the persisted shape (an always-present,
/// plugin-assigned <c>Id</c> plus a plugin-written <c>CollectionId</c> the client never sends).
/// </summary>
/// <param name="Id">The list's id. Null means "assign a new id". A non-null id is always honored
/// as submitted, whether or not it matches a currently saved entry - only when it matches does the
/// existing entry's <see cref="SeasonalListConfig.CollectionId"/> carry over.</param>
/// <param name="Enabled">Whether the list participates in sync.</param>
/// <param name="DisplayName">Required. Becomes the BoxSet's name.</param>
/// <param name="Username">Required. The MDBList list owner's username.</param>
/// <param name="Slug">Required. The MDBList list slug.</param>
/// <param name="ApiKey">The MDBList API key. Never logged. Null or empty means "keep the current
/// key" when editing an existing entry (matched by <paramref name="Id"/>) - required otherwise, so
/// that <c>GET SeasonalContent/Lists</c>'s masked preview can never be fed back in as a real key by
/// a naive fetch-edit-resave round trip (it isn't returned under this field name at all - see
/// <c>Api/ListsController.GetLists</c>).</param>
/// <param name="Limit">Requested page size; clamped to 1-500 on save.</param>
public sealed record SeasonalListInput(Guid? Id, bool Enabled, string DisplayName, string Username, string Slug, string? ApiKey, int Limit);
