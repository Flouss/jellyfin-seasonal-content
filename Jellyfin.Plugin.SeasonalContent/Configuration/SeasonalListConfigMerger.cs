using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.SeasonalContent.Configuration;

/// <summary>
/// The outcome of a <see cref="SeasonalListConfigMerger.Merge"/> call.
/// </summary>
/// <param name="Lists">The merged list entries. Only meaningful when <see cref="Errors"/> is empty
/// - the caller must not persist a partial save (see <c>Api/ListsController</c>).</param>
/// <param name="Errors">Validation errors, one per invalid input entry. Empty means the save is valid.</param>
public sealed record ListsSaveResult(IReadOnlyList<SeasonalListConfig> Lists, IReadOnlyList<string> Errors);

/// <summary>
/// Merges a <c>POST SeasonalContent/Lists</c> request against the currently saved lists. The whole
/// endpoint replaces the saved <c>Lists</c> array (docs/m4-plan.md - a bare-bones stand-in for the
/// full M6 config UI), but an entry whose <see cref="SeasonalListInput.Id"/> matches an existing
/// saved entry must keep that entry's plugin-assigned <c>Id</c> and <c>CollectionId</c>, so a
/// saved BoxSet survives an edit-and-resave.
/// </summary>
public static class SeasonalListConfigMerger
{
    /// <summary>
    /// Merges incoming list entries against the currently saved ones.
    /// </summary>
    /// <param name="existing">The currently saved lists.</param>
    /// <param name="incoming">The submitted replacement lists.</param>
    /// <returns>The merge result. The caller must check <see cref="ListsSaveResult.Errors"/> before persisting.</returns>
    public static ListsSaveResult Merge(IReadOnlyList<SeasonalListConfig> existing, IReadOnlyList<SeasonalListInput> incoming)
    {
        var existingById = existing.ToDictionary(l => l.Id);
        var errors = new List<string>();
        var merged = new List<SeasonalListConfig>();

        for (var index = 0; index < incoming.Count; index++)
        {
            var input = incoming[index];

            if (string.IsNullOrWhiteSpace(input.DisplayName)
                || string.IsNullOrWhiteSpace(input.Username)
                || string.IsNullOrWhiteSpace(input.Slug)
                || string.IsNullOrWhiteSpace(input.ApiKey))
            {
                errors.Add($"List at index {index}: DisplayName, Username, Slug and ApiKey are all required.");
                continue;
            }

            var matched = input.Id.HasValue && existingById.TryGetValue(input.Id.Value, out var found) ? found : null;

            merged.Add(new SeasonalListConfig
            {
                Id = input.Id ?? Guid.NewGuid(),
                Enabled = input.Enabled,
                DisplayName = input.DisplayName,
                Username = input.Username,
                Slug = input.Slug,
                ApiKey = input.ApiKey,
                Limit = Math.Clamp(input.Limit, 1, 500),
                CollectionId = matched?.CollectionId
            });
        }

        return new ListsSaveResult(merged, errors);
    }
}
