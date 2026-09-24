using System.Linq;
using Jellyfin.Plugin.SeasonalContent.RequestProfiles;
using Xunit;

namespace Jellyfin.Plugin.SeasonalContent.Tests.RequestProfiles;

public class RequestProfileLabelerTests
{
    [Fact]
    public void NoProfilesProducesNoLabels()
    {
        // Vacuum check: an inactive kind (no extras configured) must not silently produce a label
        // out of nothing.
        var result = RequestProfileLabeler.BuildLabels([]);

        Assert.Empty(result);
    }

    [Fact]
    public void DistinctNamesAreSanitizedButNotDisambiguated()
    {
        var profiles = new[]
        {
            new ProfileNameInfo(1, 10, "FHD", "Radarr Main"),
            new ProfileNameInfo(2, 20, "4K Remux", "Radarr 4K")
        };

        var result = RequestProfileLabeler.BuildLabels(profiles);

        Assert.Equal(2, result.Count);
        Assert.Equal("FHD", result[0].Label);
        Assert.Equal(1, result[0].ServerId);
        Assert.Equal(10, result[0].ProfileId);
        Assert.Equal("4K Remux", result[1].Label);
        Assert.Equal(2, result[1].ServerId);
        Assert.Equal(20, result[1].ProfileId);
    }

    [Fact]
    public void SameProfileNameOnDifferentServersIsDisambiguatedByServerName()
    {
        var profiles = new[]
        {
            new ProfileNameInfo(1, 10, "1080p", "Radarr Main"),
            new ProfileNameInfo(2, 20, "1080p", "Radarr Remote")
        };

        var result = RequestProfileLabeler.BuildLabels(profiles);

        Assert.Equal("1080p (Radarr Main)", result[0].Label);
        Assert.Equal("1080p (Radarr Remote)", result[1].Label);
    }

    [Fact]
    public void ThreeWayNameCollisionDisambiguatesEveryOne()
    {
        // Not just the first pair - a naive implementation might only fix up the second occurrence
        // and leave a later duplicate unresolved.
        var profiles = new[]
        {
            new ProfileNameInfo(1, 10, "HD", "A"),
            new ProfileNameInfo(2, 20, "HD", "B"),
            new ProfileNameInfo(3, 30, "HD", "C")
        };

        var result = RequestProfileLabeler.BuildLabels(profiles);

        Assert.Equal("HD (A)", result[0].Label);
        Assert.Equal("HD (B)", result[1].Label);
        Assert.Equal("HD (C)", result[2].Label);
    }

    [Fact]
    public void SameNameAndSameServerNameStillGetsAUniqueLabelViaNumericSuffix()
    {
        // Two entries that collide even after server-name disambiguation (identical profile name,
        // identical server name - e.g. two profile ids on one server that happen to share a
        // display name) must still end up with distinct labels, since two stub files under the same
        // folder can never share a name.
        var profiles = new[]
        {
            new ProfileNameInfo(1, 10, "HD", "Radarr Main"),
            new ProfileNameInfo(1, 11, "HD", "Radarr Main")
        };

        var result = RequestProfileLabeler.BuildLabels(profiles);

        Assert.Equal("HD (Radarr Main)", result[0].Label);
        Assert.Equal("HD (Radarr Main) #2", result[1].Label);
        Assert.NotEqual(result[0].Label, result[1].Label);
    }

    [Fact]
    public void ProfileNameNeedingFilesystemSanitisationIsCleanedUp()
    {
        var profiles = new[] { new ProfileNameInfo(1, 10, "4K/HDR<Test>", "Radarr Main") };

        var result = RequestProfileLabeler.BuildLabels(profiles);

        Assert.Equal("4KHDRTest", result[0].Label);
    }

    [Fact]
    public void OrderOfInputIsPreservedInOutput()
    {
        var profiles = new[]
        {
            new ProfileNameInfo(3, 30, "C", "S"),
            new ProfileNameInfo(1, 10, "A", "S"),
            new ProfileNameInfo(2, 20, "B", "S")
        };

        var result = RequestProfileLabeler.BuildLabels(profiles);

        Assert.Equal(["C", "A", "B"], [.. result.Select(r => r.Label)]);
    }
}
