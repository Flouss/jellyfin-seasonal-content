using Jellyfin.Plugin.SeasonalContent.Configuration;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.Configuration;

public class ConfigMigrationTests
{
    [Fact]
    public void AnEmptyGlobalKeyIsFilledFromTheFirstNonEmptyPerListKey()
    {
        var config = new PluginConfiguration
        {
            MdbListApiKey = string.Empty,
            Lists =
            [
                new SeasonalListConfig { ApiKey = "the-real-key" },
                new SeasonalListConfig { ApiKey = "the-real-key" }
            ]
        };

        var result = ConfigMigration.MigrateMdbListApiKey(config);

        Assert.Equal("the-real-key", config.MdbListApiKey);
        Assert.True(result.Changed);
    }

    [Fact]
    public void MigrationClearsEveryPerListKeyAfterCopyingIt()
    {
        // Vacuum check companion: without this, an already-empty-list config would also "pass" a
        // test that only checked the global key got filled.
        var config = new PluginConfiguration
        {
            MdbListApiKey = string.Empty,
            Lists = [new SeasonalListConfig { ApiKey = "the-real-key" }]
        };

        ConfigMigration.MigrateMdbListApiKey(config);

        Assert.Equal(string.Empty, config.Lists[0].ApiKey);
    }

    [Fact]
    public void AnAlreadySetGlobalKeyIsNeverOverwrittenByALegacyPerListKey()
    {
        var config = new PluginConfiguration
        {
            MdbListApiKey = "already-migrated-key",
            Lists = [new SeasonalListConfig { ApiKey = "stale-leftover-key" }]
        };

        var result = ConfigMigration.MigrateMdbListApiKey(config);

        Assert.Equal("already-migrated-key", config.MdbListApiKey);
        // The stale per-list key is still cleared, even though it wasn't copied - it's dead data.
        Assert.Equal(string.Empty, config.Lists[0].ApiKey);
        Assert.True(result.Changed);
    }

    [Fact]
    public void DisagreeingPerListKeysAreFlaggedButOneIsStillKept()
    {
        var config = new PluginConfiguration
        {
            MdbListApiKey = string.Empty,
            Lists =
            [
                new SeasonalListConfig { ApiKey = "key-one" },
                new SeasonalListConfig { ApiKey = "key-two" }
            ]
        };

        var result = ConfigMigration.MigrateMdbListApiKey(config);

        Assert.True(result.ListsDisagreed);
        Assert.False(string.IsNullOrEmpty(config.MdbListApiKey));
    }

    [Fact]
    public void MatchingPerListKeysAreNotFlaggedAsDisagreeing()
    {
        var config = new PluginConfiguration
        {
            MdbListApiKey = string.Empty,
            Lists =
            [
                new SeasonalListConfig { ApiKey = "same-key" },
                new SeasonalListConfig { ApiKey = "same-key" }
            ]
        };

        var result = ConfigMigration.MigrateMdbListApiKey(config);

        Assert.False(result.ListsDisagreed);
    }

    [Fact]
    public void AlreadyMigratedConfigWithNoLegacyKeysIsANoOp()
    {
        var config = new PluginConfiguration
        {
            MdbListApiKey = "the-global-key",
            Lists = [new SeasonalListConfig { ApiKey = string.Empty }]
        };

        var result = ConfigMigration.MigrateMdbListApiKey(config);

        Assert.False(result.Changed);
        Assert.False(result.ListsDisagreed);
        Assert.Equal("the-global-key", config.MdbListApiKey);
    }

    [Fact]
    public void NoListsAndNoGlobalKeyIsANoOp()
    {
        var config = new PluginConfiguration { MdbListApiKey = string.Empty, Lists = [] };

        var result = ConfigMigration.MigrateMdbListApiKey(config);

        Assert.False(result.Changed);
        Assert.False(result.ListsDisagreed);
        Assert.Equal(string.Empty, config.MdbListApiKey);
    }
}
