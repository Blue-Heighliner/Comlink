namespace BlueHeighliner.Comlink.Tests.Core;

/// <summary>Unit tests for <see cref="EngineConfig"/> methods.</summary>
public sealed class EngineConfigTests
{

    /// <summary>Default config has HeadlessMode = false.</summary>
    [Fact]
    public void DefaultConfig_HeadlessModeIsFalse()
    {
        EngineConfig config = new();
        Assert.False(config.HeadlessMode);
    }

    /// <summary>Default config has empty Users and UserGroups.</summary>
    [Fact]
    public void DefaultConfig_EmptyUsersAndGroups()
    {
        EngineConfig config = new();
        Assert.Empty(config.Users);
        Assert.Empty(config.UserGroups);
    }

    /// <summary>Empty Users produces an empty endpoint map.</summary>
    [Fact]
    public void GetUserEndpoints_EmptyUsers_ReturnsEmptyMap()
    {
        EngineConfig config = new();
        IReadOnlyDictionary<string, UserEndpoint> endpoints = config.GetUserEndpoints();
        Assert.Empty(endpoints);
    }

    /// <summary>User entries are converted to UserEndpoint with correct fields.</summary>
    [Fact]
    public void GetUserEndpoints_WithUsers_MapsCorrectly()
    {
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserEndpointConfig>
            {
                ["ALPHA"] = new UserEndpointConfig { IpAddress = "10.0.0.1", Port = 7890 },
                ["BETA"]  = new UserEndpointConfig { IpAddress = "10.0.0.2", Port = 7891 }
            }
        };

        IReadOnlyDictionary<string, UserEndpoint> endpoints = config.GetUserEndpoints();

        Assert.Equal(2, endpoints.Count);
        Assert.Equal("10.0.0.1", endpoints["ALPHA"].IpAddress);
        Assert.Equal(7890, endpoints["ALPHA"].Port);
        Assert.Equal("10.0.0.2", endpoints["BETA"].IpAddress);
    }

    /// <summary>User lookup is case-insensitive.</summary>
    [Fact]
    public void GetUserEndpoints_LookupIsCaseInsensitive()
    {
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserEndpointConfig>
            {
                ["Alpha"] = new UserEndpointConfig { IpAddress = "1.2.3.4", Port = 100 }
            }
        };

        IReadOnlyDictionary<string, UserEndpoint> endpoints = config.GetUserEndpoints();

        Assert.True(endpoints.ContainsKey("ALPHA"));
        Assert.True(endpoints.ContainsKey("alpha"));
    }

    /// <summary>Load with no --config argument returns a default config.</summary>
    [Fact]
    public void Load_NoArgs_ReturnsDefaultConfig()
    {
        EngineConfig config = EngineConfig.Load([]);
        Assert.False(config.HeadlessMode);
        Assert.Null(config.UserName);
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
                  "UserName": "TEST",
                  "PeerPort": 9001,
                  "InterfacePort": 9002,
                  "Users": { "ALPHA": { "IpAddress": "1.2.3.4", "Port": 5000 } },
                  "UserGroups": { "OPS": ["ALPHA"] }
                }
                """);

            EngineConfig config = EngineConfig.Load(["--config", tempFile]);

            Assert.True(config.HeadlessMode);
            Assert.Equal("TEST", config.UserName);
            Assert.Equal(9001, config.PeerPort);
            Assert.Equal(9002, config.InterfacePort);
            Assert.Single(config.Users);
            Assert.Equal("1.2.3.4", config.Users["ALPHA"].IpAddress);
            Assert.Single(config.UserGroups);
            Assert.Contains("ALPHA", config.UserGroups["OPS"]);
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
            Assert.Null(config.UserName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
