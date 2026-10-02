namespace BlueHeighliner.Comlink.Tests.Unit.Internal;

/// <summary>Unit tests for <see cref="NetworkConfig"/>: loading, and what it says about users.</summary>
public sealed class NetworkConfigTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"comlink-network-{Guid.NewGuid():N}");

    /// <summary>Creates the temporary directory tests write their files into.</summary>
    public NetworkConfigTests() => Directory.CreateDirectory(directory);

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    private string Write(string name, string json)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>With no argument and no Config.json in the working directory, the network is empty.</summary>
    [Fact]
    public void Load_NoArgumentAndNoFile_IsEmpty()
    {
        NetworkConfig config = NetworkConfig.Load([], directory);

        Assert.Empty(config.Users);
        Assert.Empty(config.UserGroups);
        Assert.Null(config.User);
        Assert.Null(config.AuthorityCertificate);
        Assert.Null(config.CertificateStore);
    }

    /// <summary>Without --config, Config.json in the working directory is used.</summary>
    [Fact]
    public void Load_NoArgument_UsesConfigJsonInTheWorkingDirectory()
    {
        Write("Config.json", """{ "Users": { "ALICE": { "PeerPort": 1234 } } }""");

        NetworkConfig config = NetworkConfig.Load([], directory);

        Assert.Equal(1234, config.Find("ALICE")!.PeerPort);
    }

    /// <summary>--config names the file to use, even when Config.json exists, and --user names the user the process runs as.</summary>
    [Fact]
    public void Load_ConfigArgument_OverridesTheDefaultFile_AndUserArgumentIsRead()
    {
        Write("Config.json", """{ "Users": { "DEFAULT": {} } }""");
        string custom = Write("Custom.json", """{ "Users": { "CUSTOM": {} } }""");

        NetworkConfig config = NetworkConfig.Load(["--config", custom, "--user", "CUSTOM"], directory);

        Assert.Equal(["CUSTOM"], config.Users.Keys);
        Assert.Equal("CUSTOM", config.User);
    }

    /// <summary>Without --user, User.json in the working directory names the user, as an object or a bare string; --user wins, and a file naming nobody is ignored.</summary>
    [Fact]
    public void Load_NoUserArgument_ReadsUserJsonFromTheWorkingDirectory()
    {
        Assert.Null(NetworkConfig.Load([], directory).User);

        Write("User.json", """{ "user": "ALICE" }""");
        Assert.Equal("ALICE", NetworkConfig.Load([], directory).User);

        Write("User.json", "\"BOB\"");
        Assert.Equal("BOB", NetworkConfig.Load([], directory).User);
        Assert.Equal("CAROL", NetworkConfig.Load(["--user", "CAROL"], directory).User);

        Write("User.json", "{ \"Other\": 1 }");
        Assert.Null(NetworkConfig.Load([], directory).User);
    }

    /// <summary>Reload reads the file again and replaces the users, groups and certificate settings, keeps the user the process runs as, and leaves everything as it was when the file cannot be read.</summary>
    [Fact]
    public void Reload_ReplacesWhatTheFileSays_KeepsTheRunningUser_AndSurvivesABrokenFile()
    {
        Write("Config.json", """{ "CertificateStore": "a", "UserGroups": { "G": [ "A" ] }, "Users": { "A": { "PeerPort": 1 } } }""");
        NetworkConfig config = NetworkConfig.Load(["--user", "A"], directory);

        Write("Config.json", """{ "CertificateStore": "b", "AuthorityCertificate": "root.cer", "UserGroups": { "H": [ "B" ] }, "Users": { "B": { "PeerPort": 2 } } }""");
        config.Reload();

        Assert.Equal("A", config.User);
        Assert.Equal(("b", "root.cer"), (config.CertificateStore, config.AuthorityCertificate));
        Assert.Equal(["H"], config.UserGroups.Keys);
        Assert.Equal(["B"], config.Users.Keys);

        Write("Config.json", "{ broken");
        Assert.ThrowsAny<Exception>(() => config.Reload());
        Assert.Equal(["B"], config.Users.Keys);
    }

    /// <summary>A --config path that does not exist is an error rather than an empty network.</summary>
    [Fact]
    public void Load_MissingConfigArgumentFile_Throws() => Assert.Throws<FileNotFoundException>(() => NetworkConfig.Load(["--config", Path.Combine(directory, "Nope.json")], directory));

    /// <summary>Every user setting is read, with names and property names matched case-insensitively.</summary>
    [Fact]
    public void Load_ReadsEveryUserSetting()
    {
        string path = Write("Network.json", """
            {
              "trustedAuthorityCertificateName": "ROOT",
              "UserGroups": { "OPS": [ "alice", "BOB" ] },
              "Users": {
                "ALICE": {
                  "Role": "server", "PeerPort": 1, "InterfacePort": 2,
                  "OutgoingPoints": [ { "IpAddress": "10.0.0.1", "Port": 3 }, { "SerialPort": "SL0", "SerialAddress": 5 } ],
                  "ChildClients": [ "BOB" ], "StoresMessages": true, "SecurityLevel": "HIGH", "CertificateName": "CN-ALICE",
                  "Data": { "desk": "4" },
                  "Headless": true, "AlertText": "HEY", "AlarmSoundSeconds": 5.5,
                  "QuickConfirmationEnabled": false, "ComposeAlertsEnabled": false, "MessageTagsEnabled": false, "MessageTagLabel": "Kind", "PrintReceivedEnabled": true
                }
              }
            }
            """);

        NetworkConfig config = NetworkConfig.Load(["--config", path]);
        UserInfo info = config.GetUserInfo("alice")!;
        NetworkUserConfig node = config.Find("Alice")!;

        Assert.Equal("ROOT", config.TrustedAuthorityCertificateName);
        Assert.Equal("ALICE", info.Name);
        Assert.Equal((UserRole.Server, 1, 2), (info.Role, info.PeerPort, info.InterfacePort));
        Assert.Equal([new ConnectionPoint { IpAddress = "10.0.0.1", Port = 3 }, new ConnectionPoint { SerialPort = "SL0", SerialAddress = 5 }], info.OutgoingPoints);
        Assert.Equal(["BOB"], info.ChildClients);
        Assert.True(info.StoresMessages);
        Assert.Equal(("HIGH", "CN-ALICE"), (info.SecurityLevel, info.CertificateName));
        Assert.Equal("4", info.Data["desk"]);
        Assert.Equal(["OPS"], info.Groups);
        Assert.Equal((true, "HEY", 5.5), (node.Headless, node.AlertText, node.AlarmSoundSeconds));
        Assert.Equal("SERVER", ((ConnectionPointConfig)new ConnectionPointConfig { SerialPort = "SL0", User = "SERVER" }).ToPoint().User);
        Assert.Equal((false, false, false, "Kind", true), (node.QuickConfirmationEnabled, node.ComposeAlertsEnabled, node.MessageTagsEnabled, node.MessageTagLabel, node.PrintReceivedEnabled));
    }

    /// <summary>A user the file does not list has no entry and no info.</summary>
    [Fact]
    public void UnlistedUser_HasNoEntryAndNoInfo()
    {
        NetworkConfig config = new();

        Assert.Null(config.Find("NOBODY"));
        Assert.Null(config.Find(null));
        Assert.Null(config.GetUserInfo("NOBODY"));
    }

    /// <summary>Relative certificate paths resolve against the configuration file's directory, absolute ones are used as they are, each user's identity is {USERNAME}.pfx in the store, and without a store there is no path.</summary>
    [Fact]
    public void CertificatePaths_ResolveAgainstTheFilesDirectory()
    {
        string absolute = Path.Combine(directory, "absolute-store");
        string path = Write("Network.json", """{ "AuthorityCertificate": "../Root.cer", "CertificateStore": "certs" }""");
        string absolutePath = Write("Absolute.json", $$"""{ "CertificateStore": {{System.Text.Json.JsonSerializer.Serialize(absolute)}} }""");

        NetworkConfig config = NetworkConfig.Load(["--config", path]);

        Assert.Equal(Path.GetFullPath(Path.Combine(directory, "..", "Root.cer")), config.GetAuthorityCertificatePath());
        Assert.Equal(Path.Combine(directory, "certs", "ALICE.pfx"), config.GetCertificatePath("ALICE"));
        Assert.Equal(Path.Combine(absolute, "BOB.pfx"), NetworkConfig.Load(["--config", absolutePath]).GetCertificatePath("BOB"));
        Assert.Null(new NetworkConfig().GetCertificatePath("ALICE"));
        Assert.Null(new NetworkConfig().GetAuthorityCertificatePath());
    }

    /// <summary>A recognized role parses regardless of case; an unset or unrecognized one is not a role.</summary>
    [Theory]
    [InlineData("server", UserRole.Server)]
    [InlineData("CLIENT", UserRole.Client)]
    [InlineData("router", UserRole.Router)]
    [InlineData("Peer", UserRole.Peer)]
    [InlineData(null, null)]
    [InlineData("Bogus", null)]
    public void GetRole_ParsesRecognizedNamesOnly(string? role, UserRole? expected) => Assert.Equal(expected, new NetworkUserConfig { Role = role }.GetRole());

    /// <summary>Serial and IP points convert to the engine's connection point model.</summary>
    [Fact]
    public void ConnectionPointConfig_ConvertsToAPoint()
    {
        Assert.Equal(new ConnectionPoint { SerialPort = "SL0", SerialAddress = 5 }, new ConnectionPointConfig { SerialPort = "SL0", SerialAddress = 5 }.ToPoint());
        Assert.Equal(new ConnectionPoint { IpAddress = "10.0.0.9", Port = 7 }, new ConnectionPointConfig { IpAddress = "10.0.0.9", Port = 7 }.ToPoint());
    }
}
