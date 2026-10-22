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

    /// <summary>A file with a role that is not Client or Server does not load, so networking cannot start.</summary>
    [Theory]
    [InlineData("Relay")]
    [InlineData("Bogus")]
    public void Load_UnrecognizedRole_Fails(string role)
    {
        File.WriteAllText(Path.Combine(directory, "Config.json"), $$"""{ "Users": { "ALICE": { "Role": "{{role}}" }, "BOB": { "Role": "Client" } } }""");

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => NetworkConfig.Load([], directory));

        Assert.Contains($"\"{role}\" of user ALICE", error.Message);
    }

    /// <summary>With no argument and no Config.json in the app data folder, the network is empty.</summary>
    [Fact]
    public void Load_NoArgumentAndNoFile_IsEmpty()
    {
        NetworkConfig config = NetworkConfig.Load([], directory);

        Assert.Empty(config.Users);
        Assert.Empty(config.UserGroups);
        Assert.Null(config.AuthorityCertificateOverride);
        Assert.Null(config.UserCertificateOverride);
    }

    /// <summary>Without --config, Config.json in the app data folder is used.</summary>
    [Fact]
    public void Load_NoArgument_UsesConfigJsonInTheAppDataFolder()
    {
        Write("Config.json", """{ "Users": { "ALICE": { "IpHost": "10.0.0.5", "Msmt": { "Port": 1234 } } } }""");

        NetworkConfig config = NetworkConfig.Load([], directory);

        Assert.Equal(("10.0.0.5", 1234), (config.Find("ALICE")!.IpHost, config.Find("ALICE")!.GetMsmtPort()));
    }

    /// <summary>--config names the file to use, even when Config.json exists.</summary>
    [Fact]
    public void Load_ConfigArgument_OverridesTheDefaultFile()
    {
        Write("Config.json", """{ "Users": { "DEFAULT": {} } }""");
        string custom = Write("Custom.json", """{ "Users": { "CUSTOM": {} } }""");

        NetworkConfig config = NetworkConfig.Load(["--config", custom], directory);

        Assert.Equal(["CUSTOM"], config.Users.Keys);
    }

    /// <summary>Argument values are read after their name, and a name that is last or missing has no value.</summary>
    [Fact]
    public void Argument_ReadsTheValueAfterItsName()
    {
        Assert.Equal("data", NetworkConfig.Argument(["--x", "1", "--data", "data"], "--data"));
        Assert.Null(NetworkConfig.Argument(["--data"], "--data"));
        Assert.Null(NetworkConfig.Argument([], "--data"));
    }

    /// <summary>Reload reads the file again and replaces the users and groups, and leaves everything as it was when the file cannot be read.</summary>
    [Fact]
    public void Reload_ReplacesWhatTheFileSays_AndSurvivesABrokenFile()
    {
        Write("Config.json", """{ "UserGroups": { "G": [ "A" ] }, "Users": { "A": { "Msmt": { "Port": 1 } } } }""");
        NetworkConfig config = NetworkConfig.Load([], directory);

        Write("Config.json", """{ "UserGroups": { "H": [ "B" ] }, "Users": { "B": { "PeerPort": 2 } } }""");
        config.Reload();

        Assert.Equal(["H"], config.UserGroups.Keys);
        Assert.Equal(["B"], config.Users.Keys);

        Write("Config.json", "{ broken");
        Assert.ThrowsAny<Exception>(() => config.Reload());
        Assert.Equal(["B"], config.Users.Keys);
    }

    private sealed record DeskProfile(string Desk, int Floor);

    private string WriteProfiles()
        => Write("Profiles.json", """
            {
              "Users": {
                "ALICE": { "Profile": { "desk": "4B", "FLOOR": 2 } },
                "BOB": { },
                "CAROL": { "Profile": null }
              }
            }
            """);

    /// <summary>A user's Profile is read into the type the host stated, names matched without regard to case; a user with no Profile key, or a null one, has none.</summary>
    [Fact]
    public void Profile_IsReadIntoTheStatedType()
    {
        NetworkConfig config = NetworkConfig.Load(["--config", WriteProfiles()], directory, typeof(DeskProfile));

        Assert.Equal(new DeskProfile("4B", 2), config.GetUserInfo("ALICE")!.Profile);
        Assert.Null(config.GetUserInfo("BOB")!.Profile);
        Assert.Null(config.GetUserInfo("CAROL")!.Profile);
    }

    private enum Rank
    {
        Junior,
        Senior
    }

    [Flags]
    private enum Access
    {
        None = 0,
        Read = 1,
        Write = 2
    }

    private sealed record Office(string City, string? Zip);

    private sealed record Employee(string Name, Rank Rank, Access Access, Office Home, List<Office> Offices, IReadOnlyList<string> Tags, Dictionary<string, int> Scores, Office? Backup, int? Age, TimeSpan Shift, DateOnly Start, Rank[] Past, IReadOnlyDictionary<string, Office> ByCode)
    {
        public string Extra { get; init; } = "default";
    }

    /// <summary>Lists, arrays, dictionaries, nested records, enums (by name in any case, flags included), nullables and the common value types all read into the profile record.</summary>
    [Fact]
    public void Profile_ComplexShapes_AreReadIntoTheRecord()
    {
        string path = Write("Complex.json", """
            {
              "Users": {
                "ALICE": {
                  "Profile": {
                    "name": "Alice",
                    "rank": "senior",
                    "ACCESS": "Read, Write",
                    "home": { "city": "Oslo", "zip": null },
                    "offices": [ { "City": "Paris", "Zip": "75001" }, { "City": "Rome" } ],
                    "tags": [ "a", "b" ],
                    "scores": { "math": 90, "art": 70 },
                    "backup": { "City": "Lima" },
                    "age": 41,
                    "shift": "08:30:00",
                    "start": "2024-02-29",
                    "past": [ "Junior", "senior", "SENIOR" ],
                    "byCode": { "X": { "City": "Cairo" } },
                    "unknownKey": [ 1, 2, 3 ],
                    "extra": "set"
                  }
                }
              }
            }
            """);

        Employee profile = Assert.IsType<Employee>(NetworkConfig.Load(["--config", path], directory, typeof(Employee)).GetUserInfo("ALICE")!.Profile);

        Assert.Equal(("Alice", Rank.Senior, Access.Read | Access.Write, "set"), (profile.Name, profile.Rank, profile.Access, profile.Extra));
        Assert.Equal(new Office("Oslo", null), profile.Home);
        Assert.Equal([new Office("Paris", "75001"), new Office("Rome", null)], profile.Offices);
        Assert.Equal(["a", "b"], profile.Tags);
        Assert.Equal(90, profile.Scores["math"]);
        Assert.Equal(new Office("Lima", null), profile.Backup);
        Assert.Equal((41, TimeSpan.FromMinutes(510), new DateOnly(2024, 2, 29)), (profile.Age, profile.Shift, profile.Start));
        Assert.Equal([Rank.Junior, Rank.Senior, Rank.Senior], profile.Past);
        Assert.Equal(new Office("Cairo", null), profile.ByCode["X"]);
    }

    /// <summary>An enum written as a number or as a name that is not one of the enum's members, or a type that cannot be read from JSON at all, fails the load naming the user, as a mismatched shape does.</summary>
    [Theory]
    [InlineData("""{ "Name": "A", "Rank": "Boss" }""", typeof(Employee))]
    [InlineData("""{ "Name": "A", "Rank": 7 }""", typeof(Employee))]
    [InlineData("""{ "Name": "A" }""", typeof(IDisposable))]
    public void Profile_UnreadableValue_Fails(string profileJson, Type type)
    {
        string path = Write("Unreadable.json", $$"""{ "Users": { "ALICE": { "Profile": {{profileJson}} } } }""");

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => NetworkConfig.Load(["--config", path], directory, type));

        Assert.Contains("profile of user ALICE", error.Message);
    }

    /// <summary>Without a stated type the profile is ignored, whatever the file holds.</summary>
    [Fact]
    public void Profile_WithoutAStatedType_IsNull() => Assert.Null(NetworkConfig.Load(["--config", WriteProfiles()], directory).GetUserInfo("ALICE")!.Profile);

    /// <summary>A profile that cannot be read into the stated type fails the load and names the user, so a bad file is refused at start-up and on a refresh rather than when the profile is used.</summary>
    [Fact]
    public void Profile_NotMatchingTheStatedType_Fails()
    {
        string path = Write("Bad.json", """{ "Users": { "ALICE": { "Profile": { "Desk": 7, "Floor": "high" } } } }""");

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => NetworkConfig.Load(["--config", path], directory, typeof(DeskProfile)));

        Assert.Contains("profile of user ALICE", error.Message);
        Assert.Contains(nameof(DeskProfile), error.Message);
    }

    /// <summary>A reload reads the profiles again with the same type, so an edited profile is picked up and a bad one is refused.</summary>
    [Fact]
    public void Profile_Reload_ReadsAgainWithTheSameType()
    {
        string path = Write("Config.json", """{ "Users": { "ALICE": { "Profile": { "Desk": "1", "Floor": 1 } } } }""");
        NetworkConfig config = NetworkConfig.Load([], directory, typeof(DeskProfile));
        Assert.Equal(new DeskProfile("1", 1), config.GetUserInfo("ALICE")!.Profile);

        File.WriteAllText(path, """{ "Users": { "ALICE": { "Profile": { "Desk": "2", "Floor": 3 } } } }""");
        config.Reload();
        Assert.Equal(new DeskProfile("2", 3), config.GetUserInfo("ALICE")!.Profile);

        File.WriteAllText(path, """{ "Users": { "ALICE": { "Profile": { "Floor": "x" } } } }""");
        Assert.Throws<InvalidDataException>(() => config.Reload());
        Assert.Equal(new DeskProfile("2", 3), config.GetUserInfo("ALICE")!.Profile);
    }

    /// <summary>A user's Level names a member of the host's level enum, in any case and whatever the level is labelled; a user who states none has none.</summary>
    [Fact]
    public void Level_NamesAMemberOfTheLevelEnum()
    {
        string path = Write("Levels.json", """{ "Users": { "A": { "Level": "restricted" }, "B": { "Level": " High " }, "C": {} } }""");

        NetworkConfig config = NetworkConfig.Load(["--config", path], directory, levels: [TestLevel.Restricted, TestLevel.High]);

        Assert.Equal(TestLevel.Restricted, config.GetUserInfo("A")!.Level);
        Assert.Equal(TestLevel.High, config.GetUserInfo("B")!.Level);
        Assert.Null(config.GetUserInfo("C")!.Level);
    }

    /// <summary>A Level that is not one of the configured levels fails the load and names the user, the level and the ones there are, and so does a Level when none are configured; a member of the enum that was not stated as a level does not count.</summary>
    [Theory]
    [InlineData("Bogus")]
    [InlineData("Secret")]
    [InlineData("SECRET-LABEL")]
    public void Level_NotConfigured_FailsTheLoad(string level)
    {
        string path = Write("Bad.json", $$"""{ "Users": { "ALICE": { "Level": "{{level}}" } } }""");

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => NetworkConfig.Load(["--config", path], directory, levels: [TestLevel.Public, TestLevel.Restricted]));
        InvalidDataException none = Assert.Throws<InvalidDataException>(() => NetworkConfig.Load(["--config", path], directory));

        Assert.Contains($"\"{level}\" of user ALICE", error.Message);
        Assert.Contains("Public, Restricted", error.Message);
        Assert.Contains("none are configured", none.Message);
    }

    /// <summary>A reload checks the levels again against the ones the host stated, so an edit to an unconfigured level is refused and changes nothing.</summary>
    [Fact]
    public void Level_Reload_ChecksAgain()
    {
        string path = Write("Config.json", """{ "Users": { "A": { "Level": "High" } } }""");
        NetworkConfig config = NetworkConfig.Load([], directory, levels: [TestLevel.Low, TestLevel.High]);

        File.WriteAllText(path, """{ "Users": { "A": { "Level": "Low" } } }""");
        config.Reload();
        Assert.Equal(TestLevel.Low, config.GetUserInfo("A")!.Level);

        File.WriteAllText(path, """{ "Users": { "A": { "Level": "Secret" } } }""");
        Assert.Throws<InvalidDataException>(() => config.Reload());
        Assert.Equal(TestLevel.Low, config.GetUserInfo("A")!.Level);
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
                  "Level": "HIGH",
                  "AutoForwarders": [ "Alerts", "Backups" ],
                  "Headless": true
                }
              }
            }
            """);

        NetworkConfig config = NetworkConfig.Load(["--config", path], directory, levels: [TestLevel.Low, TestLevel.High]);
        UserInformation info = config.GetUserInfo("alice")!;
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
        Assert.Equal(TestLevel.High, info.Level);
        Assert.Equal(["Alerts", "Backups"], info.AutoForwarders);
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

    /// <summary>The --authority and --certificate arguments are read like --config, and keys the engine does not know are ignored.</summary>
    [Fact]
    public void CertificateArguments_AreReadAndUnknownKeysIgnored()
    {
        string path = Write("Unknown.json", """{ "Unknown": 1, "Users": { "A": { "Unknown": { "desk": "4" } } } }""");

        NetworkConfig config = NetworkConfig.Load(["--config", path, "--authority", "root.cer", "--certificate", "me.pfx"], directory);

        Assert.Equal(("root.cer", "me.pfx"), (config.AuthorityCertificateOverride, config.UserCertificateOverride));
        Assert.Single(config.Users);
        Assert.Equal("A", config.GetUserInfo("A")!.Name);
        Assert.Null(NetworkConfig.Load([], directory).AuthorityCertificateOverride);
    }

    /// <summary>A recognized role parses regardless of case; an unset or unrecognized one is not a role.</summary>
    [Theory]
    [InlineData("server", UserRole.Server)]
    [InlineData("CLIENT", UserRole.Client)]
    [InlineData("relay", null)]
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

        UserInformation info = NetworkConfig.Load(["--config", path], directory).GetUserInfo("A")!;

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

        NetworkConfig config = NetworkConfig.Load(["--config", path], directory);

        Assert.Equal(["*"], config.GetUserInfo("ALL")!.HdlcPorts);
        Assert.Equal(["A", "B"], config.GetUserInfo("SOME")!.HdlcPorts);
        Assert.Equal((byte?)9, config.GetUserInfo("SOME")!.HdlcAddress);
        Assert.Equal(((byte?)null, 0), (config.GetUserInfo("NONE")!.HdlcAddress, config.GetUserInfo("NONE")!.HdlcPorts.Count));
        Assert.Equal(((byte?)null, (int?)null, 0), (config.GetUserInfo("BARE")!.HdlcAddress, config.GetUserInfo("BARE")!.MsmtPort, config.GetUserInfo("BARE")!.HdlcPorts.Count));
    }
}
