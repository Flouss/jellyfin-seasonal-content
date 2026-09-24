using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.SeasonalContent.Stubs;

namespace Jellyfin.Plugin.SeasonalContent.RequestProfiles;

/// <summary>
/// Turns a list of live-fetched Jellyseerr profile names into unique, filesystem-safe version
/// labels, preserving input order. Pure - no I/O, no Jellyseerr calls (those live in
/// <see cref="RequestProfileResolver"/>). Two profiles with the same name are disambiguated by
/// appending their server's name, and if that still collides (e.g. an empty/duplicate server name)
/// a numeric suffix guarantees uniqueness deterministically - two profiles must never resolve to
/// the same file name, or one would silently overwrite the other's stub on disk.
/// </summary>
public static class RequestProfileLabeler
{
    /// <summary>
    /// Builds one disambiguated label per input profile.
    /// </summary>
    /// <param name="profiles">The profiles to label, in the order they should be listed.</param>
    /// <returns>One <see cref="ResolvedRequestProfile"/> per input, same order.</returns>
    public static IReadOnlyList<ResolvedRequestProfile> BuildLabels(IReadOnlyList<ProfileNameInfo> profiles)
    {
        var baseLabels = profiles
            .Select(p => StubFileNaming.SanitiseForFileName(p.ProfileName).Trim())
            .ToList();

        var baseLabelCounts = baseLabels
            .GroupBy(l => l, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var candidateLabels = new List<string>(profiles.Count);
        for (var i = 0; i < profiles.Count; i++)
        {
            var baseLabel = baseLabels[i];
            if (baseLabelCounts[baseLabel] > 1)
            {
                var serverPart = StubFileNaming.SanitiseForFileName(profiles[i].ServerName).Trim();
                candidateLabels.Add(string.IsNullOrEmpty(serverPart) ? baseLabel : $"{baseLabel} ({serverPart})");
            }
            else
            {
                candidateLabels.Add(baseLabel);
            }
        }

        // Final uniqueness pass: anything still colliding after the server-name disambiguation
        // (same profile name AND same server name, or both sanitized empty) gets a deterministic
        // "#2", "#3", ... suffix. The first occurrence of a label is never suffixed.
        var seenCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<ResolvedRequestProfile>(profiles.Count);
        for (var i = 0; i < profiles.Count; i++)
        {
            var label = candidateLabels[i];
            var occurrence = seenCounts.TryGetValue(label, out var count) ? count + 1 : 1;
            seenCounts[label] = occurrence;

            var finalLabel = occurrence == 1 ? label : string.Format(CultureInfo.InvariantCulture, "{0} #{1}", label, occurrence);
            result.Add(new ResolvedRequestProfile(finalLabel, profiles[i].ServerId, profiles[i].ProfileId));
        }

        return result;
    }
}
