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

    /// <summary>A file with a role that is not Client, Server or Relay (the retired Peer, say) does not load, so networking cannot start.</summary>
    [Theory]
    [InlineData("Peer")]
    [InlineData("Bogus")]
    public void Load_UnrecognizedRole_Fails(string role)
    {
        File.WriteAllText(Path.Combine(directory, "Config.json"), $$"""{ "Users": { "ALICE": { "Role": "{{role}}" }, "BOB": { "Role": "Client" } } }""");

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => NetworkConfig.Load([], directory));

        Assert.Contains($"\"{role}\" of user ALICE", error.Message);
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
        Write("Config.json", """{ "Users": { "ALICE": { "IpHost": "10.0.0.5", "Msmt": { "Port": 1234 } } } }""");

        NetworkConfig config = NetworkConfig.Load([], directory);

        Assert.Equal(("10.0.0.5", 1234), (config.Find("ALICE")!.IpHost, config.Find("ALICE")!.GetMsmtPort()));
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

    /// <summary>The user comes only from --user: a User.json in the working directory is never read.</summary>
    [Fact]
    public void Load_UserComesOnlyFromTheArgument()
    {
        Write("User.json", """{ "user": "ALICE" }""");

        Assert.Null(NetworkConfig.Load([], directory).User);
        Assert.Equal("CAROL", NetworkConfig.Load(["--user", "CAROL"], directory).User);
    }

    /// <summary>Reload reads the file again and replaces the users, groups and certificate settings, keeps the user the process runs as, and leaves everything as it was when the file cannot be read.</summary>
    [Fact]
    public void Reload_ReplacesWhatTheFileSays_KeepsTheRunningUser_AndSurvivesABrokenFile()
    {
        Write("Config.json", """{ "CertificateStore": "a", "UserGroups": { "G": [ "A" ] }, "Users": { "A": { "Msmt": { "Port": 1 } } } }""");
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
              "UserGroups": { "OPS": [ "alice", "BOB" ] },
              "Users": {
                "ALICE": {
                  "Role": "server", "IpHost": "10.0.0.1", "Msmt": { "Port": 1, "HandshakeTimeout": "00:00:07" }, "Hdlc": { "Address": 3, "Ports": [ "SL0", "SL1" ], "MaxInfoField": 512 }, "InterfacePort": 2,
                  "Parent": { "User": "ROOT", "Mode": "MsmtListen" },
                  "Children": [ "BOB", { "User": "CAROL", "Mode": "Hdlc", "Port": "ignored", "Address": 5 }, { "user": "DAN", "mode": "msmtconnect" } ],
                  "SecurityLevel": "HIGH",
                  "Data": { "desk": "4" },
                  "Headless": true
                }
              }
            }
            """);

        NetworkConfig config = NetworkConfig.Load(["--config", path]);
        UserInfo info = config.GetUserInfo("alice")!;
        NetworkUserConfig node = config.Find("Alice")!;

        Assert.Equal("ALICE", info.Name);
        Assert.Equal((UserRole.Server, 2), (info.Role, info.InterfacePort));
        Assert.Equal(("10.0.0.1", 1, (byte)3), (info.IpHost, info.MsmtPort, info.HdlcAddress));
        Assert.Equal(["SL0", "SL1"], info.HdlcPorts);
        Assert.Equal(512, node.Hdlc!.Value.GetProperty("MaxInfoField").GetInt32());
        Assert.Equal(new UserLink { User = "ROOT", Mode = ConnectionMode.MsmtListen }, info.Parent);
        Assert.Equal(
            [new UserLink { User = "BOB" }, new UserLink { User = "CAROL", Mode = ConnectionMode.Hdlc }, new UserLink { User = "DAN", Mode = ConnectionMode.MsmtConnect }],
            info.Children);
        Assert.Equal("HIGH", info.SecurityLevel);
        Assert.Equal("4", info.Data["desk"]);
        Assert.Equal(["OPS"], info.Groups);
        Assert.True(node.Headless);
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
    [InlineData("relay", UserRole.Relay)]
    [InlineData("Peer", null)]
    [InlineData(null, null)]
    [InlineData("Bogus", null)]
    public void GetRole_ParsesRecognizedNamesOnly(string? role, UserRole? expected) => Assert.Equal(expected, new NetworkUserConfig { Role = role }.GetRole());

    /// <summary>A link written as a plain name or an object both read, with the object form carrying the mode and serial settings, and an unrecognized mode is the default.</summary>
    [Fact]
    public void Links_ReadFromANameOrAnObject()
    {
        string path = Write("Links.json", """
            { "Users": { "A": { "Parent": "B", "Children": [ "C", { "User": "D", "Mode": "bogus" }, { "User": "E", "Mode": "Hdlc" } ] } } }
            """);

        UserInfo info = NetworkConfig.Load(["--config", path]).GetUserInfo("A")!;

        Assert.Equal(new UserLink { User = "B" }, info.Parent);
        Assert.Equal([new UserLink { User = "C" }, new UserLink { User = "D" }, new UserLink { User = "E", Mode = ConnectionMode.Hdlc }], info.Children);
    }

    /// <summary>The HDLC ports are the named ports, a single <c>*</c> for every port, or none, and the address and port are read only when numbers.</summary>
    [Fact]
    public void HdlcPortsAndAddress_AreReadFromTheHdlcSection()
    {
        string path = Write("Hdlc.json", """
            { "Users": { "ALL": { "Hdlc": { "Ports": "*" } }, "SOME": { "Hdlc": { "Address": 9, "Ports": [ "A", "B" ] } }, "NONE": { "Hdlc": { "Address": "x" } }, "BARE": {} } }
            """);

        NetworkConfig config = NetworkConfig.Load(["--config", path]);

        Assert.Equal(["*"], config.GetUserInfo("ALL")!.HdlcPorts);
        Assert.Equal(["A", "B"], config.GetUserInfo("SOME")!.HdlcPorts);
        Assert.Equal((byte?)9, config.GetUserInfo("SOME")!.HdlcAddress);
        Assert.Equal(((byte?)null, 0), (config.GetUserInfo("NONE")!.HdlcAddress, config.GetUserInfo("NONE")!.HdlcPorts.Count));
        Assert.Equal(((byte?)null, (int?)null, 0), (config.GetUserInfo("BARE")!.HdlcAddress, config.GetUserInfo("BARE")!.MsmtPort, config.GetUserInfo("BARE")!.HdlcPorts.Count));
    }
}
