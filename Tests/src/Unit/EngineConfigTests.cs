namespace BlueHeighliner.Comlink.Tests.Unit;

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

    /// <summary>Empty Users produces an empty user data map.</summary>
    [Fact]
    public void GetUserData_EmptyUsers_ReturnsEmptyMap()
    {
        EngineConfig config = new();
        Assert.Empty(config.GetUserData());
    }

    /// <summary>User entries carry their data through.</summary>
    [Fact]
    public void GetUserData_WithUsers_MapsCorrectly()
    {
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserConfig>
            {
                ["ALPHA"] = new UserConfig { Data = new Dictionary<string, string> { ["role"] = "clerk", ["desk"] = "4" } },
                ["BETA"] = new UserConfig()
            }
        };

        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> data = config.GetUserData();

        Assert.Equal(2, data.Count);
        Assert.Equal("clerk", data["ALPHA"]["role"]);
        Assert.Equal("4", data["ALPHA"]["desk"]);
        Assert.Empty(data["BETA"]);
    }

    /// <summary>User lookup is case-insensitive, and two keys differing only by case do not throw.</summary>
    [Fact]
    public void GetUserData_LookupIsCaseInsensitive_AndDuplicateKeysDoNotThrow()
    {
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserConfig>
            {
                ["Alpha"] = new UserConfig { Data = new Dictionary<string, string> { ["k"] = "v" } },
                ["ALPHA"] = new UserConfig { Data = new Dictionary<string, string> { ["k"] = "w" } }
            }
        };

        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> data = config.GetUserData();

        Assert.Single(data);
        Assert.True(data.ContainsKey("alpha"));
    }

    /// <summary>Outgoing points convert to connection points, IP or serial.</summary>
    [Fact]
    public void GetOutgoingPoints_ConvertsEachEntry()
    {
        EngineConfig config = new()
        {
            OutgoingPoints =
            [
                new ConnectionPointConfig { IpAddress = "10.0.0.1", Port = 7890 },
                new ConnectionPointConfig { SerialPort = "SL0", SerialAddress = 4 }
            ]
        };

        IReadOnlyList<ConnectionPoint> points = config.GetOutgoingPoints();

        Assert.Equal(2, points.Count);
        Assert.Equal(new ConnectionPoint { IpAddress = "10.0.0.1", Port = 7890 }, points[0]);
        Assert.Equal(new ConnectionPoint { SerialPort = "SL0", SerialAddress = 4 }, points[1]);
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
                  "Users": { "ALPHA": { "Data": { "role": "clerk" } } },
                  "UserGroups": { "OPS": ["ALPHA"] }
                }
                """);

            EngineConfig config = EngineConfig.Load(["--config", tempFile]);

            Assert.True(config.HeadlessMode);
            Assert.Equal("TEST", config.UserName);
            Assert.Equal(9001, config.PeerPort);
            Assert.Equal(9002, config.InterfacePort);
            Assert.Single(config.Users);
            Assert.Equal("clerk", config.Users["ALPHA"].Data["role"]);
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

    /// <summary>A relative certificate file path is resolved against the directory containing the loaded config file.</summary>
    [Fact]
    public void GetPeerCertificateFilePath_RelativePath_ResolvesAgainstConfigDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"comlink-config-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string tempFile = Path.Combine(tempDir, "config.json");
        try
        {
            File.WriteAllText(tempFile, """{ "PeerCertificateFile": "identity.pfx", "TrustedAuthorityCertificateFile": "../Root.cer" }""");

            EngineConfig config = EngineConfig.Load(["--config", tempFile]);

            Assert.Equal(Path.Combine(tempDir, "identity.pfx"), config.GetPeerCertificateFilePath());
            Assert.Equal(Path.Combine(Path.GetDirectoryName(tempDir)!, "Root.cer"), config.GetTrustedAuthorityCertificateFilePath());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>An absolute certificate file path is used verbatim, not combined with the config directory.</summary>
    [Fact]
    public void GetPeerCertificateFilePath_AbsolutePath_UsedDirectly()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            string absolutePath = Path.Combine(Path.GetTempPath(), "identity.pfx");
            File.WriteAllText(tempFile, $$"""{ "PeerCertificateFile": {{JsonSerializer.Serialize(absolutePath)}} }""");

            EngineConfig config = EngineConfig.Load(["--config", tempFile]);

            Assert.Equal(absolutePath, config.GetPeerCertificateFilePath());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>Null certificate file config fields resolve to null, with no config directory to combine against.</summary>
    [Fact]
    public void GetPeerCertificateFilePath_NoConfigFile_ReturnsNull()
    {
        EngineConfig config = EngineConfig.Load([]);
        Assert.Null(config.GetPeerCertificateFilePath());
        Assert.Null(config.GetTrustedAuthorityCertificateFilePath());
    }

    /// <summary>GetNodeRole parses a recognized role name case-insensitively.</summary>
    [Fact]
    public void GetNodeRole_RecognizedName_ParsesCaseInsensitively()
    {
        EngineConfig config = new() { NodeRole = "server" };
        Assert.Equal(BlueHeighliner.Comlink.Control.NodeRole.Server, config.GetNodeRole());
    }

    /// <summary>GetNodeRole defaults to Peer when NodeRole is null or unrecognized.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("NotARole")]
    public void GetNodeRole_UnsetOrUnrecognized_DefaultsToPeer(string? nodeRole)
    {
        EngineConfig config = new() { NodeRole = nodeRole };
        Assert.Equal(BlueHeighliner.Comlink.Control.NodeRole.Peer, config.GetNodeRole());
    }
}
