namespace BlueHeighliner.Comlink.Tests.Core;

/// <summary>Unit tests for <see cref="EngineConfig"/> methods.</summary>
public sealed class EngineConfigTests
{
    // ── Defaults ──────────────────────────────────────────────────────────────

    /// <summary>Default config has HeadlessMode = false.</summary>
    [Fact]
    public void DefaultConfig_HeadlessModeIsFalse()
    {
        EngineConfig config = new();
        Assert.False(config.HeadlessMode);
    }

    /// <summary>Default config has empty Sites and SiteGroups.</summary>
    [Fact]
    public void DefaultConfig_EmptySitesAndGroups()
    {
        EngineConfig config = new();
        Assert.Empty(config.Sites);
        Assert.Empty(config.SiteGroups);
    }

    // ── GetSiteEndpoints ──────────────────────────────────────────────────────

    /// <summary>Empty Sites produces an empty endpoint map.</summary>
    [Fact]
    public void GetSiteEndpoints_EmptySites_ReturnsEmptyMap()
    {
        EngineConfig config = new();
        IReadOnlyDictionary<string, SiteEndpoint> endpoints = config.GetSiteEndpoints();
        Assert.Empty(endpoints);
    }

    /// <summary>Site entries are converted to SiteEndpoint with correct fields.</summary>
    [Fact]
    public void GetSiteEndpoints_WithSites_MapsCorrectly()
    {
        EngineConfig config = new()
        {
            Sites = new Dictionary<string, SiteEndpointConfig>
            {
                ["ALPHA"] = new SiteEndpointConfig { IpAddress = "10.0.0.1", Port = 7890 },
                ["BETA"]  = new SiteEndpointConfig { IpAddress = "10.0.0.2", Port = 7891 }
            }
        };

        IReadOnlyDictionary<string, SiteEndpoint> endpoints = config.GetSiteEndpoints();

        Assert.Equal(2, endpoints.Count);
        Assert.Equal("10.0.0.1", endpoints["ALPHA"].IpAddress);
        Assert.Equal(7890, endpoints["ALPHA"].Port);
        Assert.Equal("10.0.0.2", endpoints["BETA"].IpAddress);
    }

    /// <summary>Site lookup is case-insensitive.</summary>
    [Fact]
    public void GetSiteEndpoints_LookupIsCaseInsensitive()
    {
        EngineConfig config = new()
        {
            Sites = new Dictionary<string, SiteEndpointConfig>
            {
                ["Alpha"] = new SiteEndpointConfig { IpAddress = "1.2.3.4", Port = 100 }
            }
        };

        IReadOnlyDictionary<string, SiteEndpoint> endpoints = config.GetSiteEndpoints();

        Assert.True(endpoints.ContainsKey("ALPHA"));
        Assert.True(endpoints.ContainsKey("alpha"));
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>Load with no --config argument returns a default config.</summary>
    [Fact]
    public void Load_NoArgs_ReturnsDefaultConfig()
    {
        EngineConfig config = EngineConfig.Load([]);
        Assert.False(config.HeadlessMode);
        Assert.Null(config.SiteName);
        Assert.Null(config.PeerPort);
        Assert.Null(config.InterfacePort);
    }

    /// <summary>Load with a real JSON file deserializes all fields.</summary>
    [Fact]
    public void Load_WithConfigFile_DeserializesCorrectly()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                {
                  "HeadlessMode": true,
                  "SiteName": "TEST",
                  "PeerPort": 9001,
                  "InterfacePort": 9002,
                  "Sites": { "ALPHA": { "IpAddress": "1.2.3.4", "Port": 5000 } },
                  "SiteGroups": { "OPS": ["ALPHA"] }
                }
                """);

            EngineConfig config = EngineConfig.Load(["--config", tempFile]);

            Assert.True(config.HeadlessMode);
            Assert.Equal("TEST", config.SiteName);
            Assert.Equal(9001, config.PeerPort);
            Assert.Equal(9002, config.InterfacePort);
            Assert.Single(config.Sites);
            Assert.Equal("1.2.3.4", config.Sites["ALPHA"].IpAddress);
            Assert.Single(config.SiteGroups);
            Assert.Contains("ALPHA", config.SiteGroups["OPS"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>An empty JSON object produces all-default values.</summary>
    [Fact]
    public void Load_EmptyJson_ReturnsDefaultValues()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{}");
            EngineConfig config = EngineConfig.Load(["--config", tempFile]);
            Assert.False(config.HeadlessMode);
            Assert.Null(config.SiteName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
