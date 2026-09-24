using System.Linq;

namespace Jellyfin.Plugin.SeasonalContent.Configuration;

/// <summary>
/// The outcome of <see cref="ConfigMigration.MigrateMdbListApiKey"/>.
/// </summary>
/// <param name="Changed">Whether the config was actually mutated (a global key was copied in, or a
/// legacy per-list key was cleared) - the caller should only call <c>SaveConfiguration()</c> when
/// this is <see langword="true"/>.</param>
/// <param name="ListsDisagreed">Whether more than one list had a different non-empty legacy key -
/// the caller should log a warning (never including the keys themselves) when this is
/// <see langword="true"/>.</param>
public readonly record struct MdbListApiKeyMigrationResult(bool Changed, bool ListsDisagreed);

/// <summary>
/// One-time migration from the old per-list <see cref="SeasonalListConfig.ApiKey"/> to the new
/// global <see cref="PluginConfiguration.MdbListApiKey"/> (docs/rename-tv-globalkey-plan.md §2).
/// Pure and idempotent: safe to run on every startup, a no-op once already migrated.
/// </summary>
public static class ConfigMigration
{
    /// <summary>
    /// Migrates a legacy per-list MDBList API key into the new global field, in place.
    /// </summary>
    /// <param name="config">The configuration to migrate, mutated in place.</param>
    /// <returns>What happened, for the caller to log/persist.</returns>
    public static MdbListApiKeyMigrationResult MigrateMdbListApiKey(PluginConfiguration config)
    {
        var legacyKeys = config.Lists
            .Select(l => l.ApiKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToList();

        var changed = false;

        if (string.IsNullOrWhiteSpace(config.MdbListApiKey) && legacyKeys.Count > 0)
        {
            // First non-empty key wins, arbitrarily - same "last/first one wins" convention as
            // OwnedItemIndex/DesiredStubSet use for their own dedup collisions.
            config.MdbListApiKey = legacyKeys[0];
            changed = true;
        }

        foreach (var list in config.Lists)
        {
            if (!string.IsNullOrEmpty(list.ApiKey))
            {
                list.ApiKey = string.Empty;
                changed = true;
            }
        }

        var disagreed = legacyKeys.Distinct().Count() > 1;

        return new MdbListApiKeyMigrationResult(changed, disagreed);
    }
}
