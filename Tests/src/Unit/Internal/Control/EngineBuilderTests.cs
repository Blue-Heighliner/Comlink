namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="EngineBuilder"/>: what a host states in its configuration is what <see cref="EngineController"/> reports, and anything left unstated takes the default.</summary>
public sealed class EngineBuilderTests
{
    private sealed class Configuration(Func<IEngineBuilder, IEngineBuilder> configure) : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => configure(new TestEngineConfiguration().Configure(engine));
    }

    private static (EngineBuilder Builder, EngineController Controller) Build(Func<IEngineBuilder, IEngineBuilder> configure)
    {
        EngineBuilder builder = EngineBuilder.Build(new Configuration(configure));
        return (builder, new EngineController(builder, new CurrentUserProvider()));
    }

    /// <summary>A configuration that never states a message type cannot start the engine.</summary>
    [Fact]
    public void Build_WithoutMessageType_Throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new EmptyConfiguration()));

        Assert.Contains("Message<TMessage>", error.Message);
    }

    private sealed class EmptyConfiguration : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => engine;
    }

    /// <summary>The engine cannot build a controller from a builder that has no message mapping.</summary>
    [Fact]
    public void Controller_WithoutMessageMapping_Throws()
        => Assert.Throws<InvalidOperationException>(() => new EngineController(new EngineBuilder(), new CurrentUserProvider()));

    /// <summary>Configure is run exactly once, against the builder the engine will read.</summary>
    [Fact]
    public void Build_RunsConfigurationOnceAndKeepsWhatItStated()
    {
        int calls = 0;
        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => { calls++; return engine.AppName("MyApp"); }));

        Assert.Equal(1, calls);
        Assert.Equal("MyApp", builder.AppNameValue);
    }

    /// <summary>Settings left unstated take the engine's defaults.</summary>
    [Fact]
    public void Defaults_AreTheEnginesOwn()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Equal("HOME", controller.HomeText);
        Assert.False(controller.IsKioskMode);
        Assert.Null(controller.WindowIconUri);
        Assert.Null(controller.DebugUserName);
        Assert.Equal(50021, controller.PeerPort);
        Assert.Equal(50020, controller.InterfacePort);
        Assert.Equal("ALERT", controller.AlertLabel);
        Assert.Equal(TimeSpan.FromSeconds(30), controller.AlarmSoundDuration);
        Assert.True(controller.QuickConfirmationEnabled);
        Assert.True(controller.ComposeAlertsEnabled);
        Assert.Equal("Tag", controller.TagLabel);
        Assert.True(controller.TagsEnabled);
        Assert.False(controller.PrintReceivedDefaultEnabled);
        Assert.Equal(NodeRole.Peer, controller.Role);
        Assert.Equal("COMLINK-ROOT", controller.TrustedAuthorityCertificateName);
        Assert.False(controller.ConfigFileEnabled);
        Assert.Empty(controller.OutgoingPoints);
        Assert.Empty(controller.Servers);
        Assert.Empty(controller.ExternalSystems);
        Assert.Null(controller.ExternalServer);
        Assert.Empty(controller.UserConnectedHooks);
        Assert.Empty(controller.UserDisconnectedHooks);
        Assert.Empty(controller.MessageReceivedHooks);
        Assert.Null(controller.PacketType);
        Assert.Single(controller.Priorities);
        Assert.Equal("Normal", controller.Priorities[0].Name);
        Assert.Equal("USER", controller.GetCertificateName("USER"));
        Assert.True(controller.CanDelete(FolderType.Inbox));
        Assert.Equal(1, controller.GetPrintCount(new TestMessage()));
        Assert.Equal([AddressType.To, AddressType.Cc, AddressType.External], controller.AddressTypes.Select(t => t.Type));
        Assert.Equal(["To", "Cc", "External"], controller.AddressTypes.Select(t => t.Label));
    }

    /// <summary>Everything a host can state about the application and the ports is reported back.</summary>
    [Fact]
    public void Stated_AppAndPortSettings_AreReported()
    {
        Uri icon = new("avares://Host/icon.png");
        (_, EngineController controller) = Build(engine => engine
            .AppName("MyApp").AppVersion("2.3.4").DataPath("/data/app").KioskMode().HomeText("Welcome").WindowIcon(icon)
            .DebugUser("DEBUG").PeerPort(1234).InterfacePort(5678).ConfigFile());

        Assert.Equal("MyApp", controller.AppName);
        Assert.Equal("2.3.4", controller.AppVersion);
        Assert.Equal("/data/app", controller.AppDataPath);
        Assert.True(controller.IsKioskMode);
        Assert.Equal("Welcome", controller.HomeText);
        Assert.Equal(icon, controller.WindowIconUri);
        Assert.Equal("DEBUG", controller.DebugUserName);
        Assert.Equal(1234, controller.PeerPort);
        Assert.Equal(5678, controller.InterfacePort);
        Assert.True(controller.ConfigFileEnabled);
    }

    /// <summary>Without a stated data path the default follows the application name.</summary>
    [Fact]
    public void AppDataPath_DefaultFollowsTheAppName()
    {
        (_, EngineController controller) = Build(engine => engine.AppName("MyApp"));

        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyApp"), controller.AppDataPath);
    }

    /// <summary>Alert, tag, priority and print settings a host states replace the defaults.</summary>
    [Fact]
    public void Stated_CompositionAndAlertSettings_AreReported()
    {
        (_, EngineController controller) = Build(engine => engine
            .AlertLabel("ALARM").AlarmDuration(TimeSpan.FromSeconds(5)).QuickConfirmation(false).ComposeAlerts(false)
            .Priorities(("Low", 0), ("High", 9))
            .Tags(false, "Category").BlockTag("SPAM", null).BlockTag(null, 9)
            .PrintReceived().PrintCount<TestMessage>(message => message.IsAlert ? 2 : 1)
            .CanDelete(folder => folder == FolderType.Drafts));

        Assert.Equal("ALARM", controller.AlertLabel);
        Assert.Equal(TimeSpan.FromSeconds(5), controller.AlarmSoundDuration);
        Assert.False(controller.QuickConfirmationEnabled);
        Assert.False(controller.ComposeAlertsEnabled);
        Assert.Equal(["Low", "High"], controller.Priorities.Select(p => p.Name));
        Assert.False(controller.TagsEnabled);
        Assert.Equal("Category", controller.TagLabel);
        Assert.Equal(2, controller.BlockedCombinations.Count);
        Assert.True(controller.PrintReceivedDefaultEnabled);
        Assert.Equal(2, controller.GetPrintCount(new TestMessage { IsAlert = true }));
        Assert.Equal(1, controller.GetPrintCount(new TestMessage()));
        Assert.True(controller.CanDelete(FolderType.Drafts));
        Assert.False(controller.CanDelete(FolderType.Inbox));
    }

    /// <summary>Turning tags on or off without a label keeps the default label.</summary>
    [Fact]
    public void Tags_WithoutLabel_KeepsTheDefaultLabel()
    {
        (_, EngineController controller) = Build(engine => engine.Tags(false));

        Assert.False(controller.TagsEnabled);
        Assert.Equal("Tag", controller.TagLabel);
    }

    /// <summary>An overridden address type label replaces the default for that type only; the others keep theirs.</summary>
    [Fact]
    public void AddressTypeLabel_Stated_ReplacesOnlyThatTypesDefault()
    {
        (_, EngineController controller) = Build(engine => engine.AddressTypeLabel(AddressType.External, "OUTSIDE"));

        Assert.Equal(["To", "Cc", "OUTSIDE"], controller.AddressTypes.Select(t => t.Label));
        Assert.Equal([AddressType.To, AddressType.Cc, AddressType.External], controller.AddressTypes.Select(t => t.Type));
    }

    /// <summary>Stating priorities twice replaces the earlier list rather than adding to it.</summary>
    [Fact]
    public void Priorities_StatedTwice_ReplacesTheEarlierList()
    {
        (_, EngineController controller) = Build(engine => engine
            .Priorities(("A", 1))
            .Priorities(("B", 2)));

        Assert.Equal("B", Assert.Single(controller.Priorities).Name);
    }

    /// <summary>Users, groups and the data attached to users are reported, with data stated by name merged and winning over a lookup.</summary>
    [Fact]
    public void Stated_UsersGroupsAndData_AreReported()
    {
        (_, EngineController controller) = Build(engine => engine
            .Users("ALICE", "BOB").Users("CAROL")
            .Group("OPS", "ALICE", "BOB").Group("ALL", "OPS", "CAROL")
            .UserData(name => new Dictionary<string, string> { ["from"] = "lookup", ["only-lookup"] = name })
            .UserData("ALICE", new Dictionary<string, string> { ["from"] = "stated", ["role"] = "clerk" })
            .UserData("ALICE", new Dictionary<string, string> { ["desk"] = "4" }));

        Assert.Equal(["ALICE", "BOB", "CAROL"], controller.Users);
        Assert.Equal(["ALICE", "BOB"], controller.UserGroups["ops"]);
        Assert.Equal(["OPS", "CAROL"], controller.UserGroups["ALL"]);
        IReadOnlyDictionary<string, string> alice = controller.GetUserData("alice");
        Assert.Equal("stated", alice["from"]);
        Assert.Equal("clerk", alice["role"]);
        Assert.Equal("4", alice["desk"]);
        Assert.Equal("alice", alice["only-lookup"]);
        Assert.Equal("lookup", controller.GetUserData("BOB")["from"]);
    }

    /// <summary>A user with nothing stated has no data.</summary>
    [Fact]
    public void GetUserData_NothingStated_IsEmpty()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Empty(controller.GetUserData("ANYONE"));
    }

    /// <summary>Installation codes resolve through the stated resolver, and by default only the code CODE is recognized.</summary>
    [Fact]
    public void UserCodes_StatedResolverReplacesTheDefault()
    {
        (_, EngineController stated) = Build(engine => engine.UserCodes(code => code == "X" ? new UserInfo { Name = "XUSER", Code = "X" } : null));
        (_, EngineController fallback) = Build(engine => engine);

        Assert.Equal("XUSER", stated.ResolveCode("X")!.Name);
        Assert.Null(stated.ResolveCode("CODE"));
        Assert.Equal("TEST", fallback.ResolveCode("code")!.Name);
        Assert.Null(fallback.ResolveCode("X"));
    }

    /// <summary>Certificate settings a host states are used, including for the MSMT options when it supplies its own.</summary>
    [Fact]
    public void Stated_CertificateSettings_AreUsed()
    {
        MsmtSessionPeerOptions options = new() { Credentials = new MsmtCredentials { Identity = TestMsmtCertificates.Create().Server, TrustedAuthorities = [] } };
        (_, EngineController controller) = Build(engine => engine
            .CertificateName(user => $"cert-{user}").TrustedAuthority("MY-ROOT").ConnectionOptions(() => options));

        Assert.Equal("cert-BOB", controller.GetCertificateName("BOB"));
        Assert.Equal("MY-ROOT", controller.TrustedAuthorityCertificateName);
        Assert.Same(options, controller.ConnectionOptions);
    }

    /// <summary>The network role, the points this node connects to, and the server topology are reported.</summary>
    [Fact]
    public void Stated_NetworkSettings_AreReported()
    {
        ConnectionPoint first = new() { IpAddress = "10.0.0.1", Port = 1 };
        ConnectionPoint second = new() { SerialPort = "SL0" };
        (_, EngineController controller) = Build(engine => engine
            .Role(NodeRole.Server).OutgoingPoint(first).OutgoingPoint(second)
            .Server("Server1", "Client1", "Client2").Server("Server2", "Client3"));

        Assert.Equal(NodeRole.Server, controller.Role);
        Assert.Equal([first, second], controller.OutgoingPoints);
        Assert.Equal(["Client1", "Client2"], controller.Servers["server1"].ChildClients);
        Assert.Equal(["Client3"], controller.Servers["Server2"].ChildClients);
    }

    /// <summary>The identification hook and the connection message, response and serializer are reported and used.</summary>
    [Fact]
    public void Stated_Identification_IsUsed()
    {
        ConnectionInfo info = new() { Host = "10.0.0.1" };
        (_, EngineController controller) = Build(engine => engine
            .Identify(connection => new UserIdentity { Name = connection.Host })
            .ConnectionMessage<TestHello>(connection => new TestHello { Name = connection.Host })
            .ConnectionResponse<TestWelcome>(connection => new TestWelcome { Name = "R", Station = 3 }));

        Assert.Equal("10.0.0.1", controller.IdentifyConnection(info)!.Name);
        Assert.Equal(typeof(TestHello), controller.ConnectionMessageType);
        Assert.Equal(typeof(TestWelcome), controller.ConnectionResponseType);
        Assert.Equal("10.0.0.1", ((TestHello)controller.CreateConnectionMessage(info)!).Name);
        Assert.Equal(3, ((TestWelcome)controller.CreateConnectionResponse(info)!).Station);
        Assert.NotNull(controller.ConnectionSerializer);
    }

    /// <summary>Without a connection message there is no exchange and no serializer, and the identification hook leaves the decision to the engine.</summary>
    [Fact]
    public void Unstated_Identification_IsOff()
    {
        (_, EngineController controller) = Build(engine => engine);
        ConnectionInfo info = new();

        Assert.Null(controller.IdentifyConnection(info));
        Assert.Null(controller.ConnectionMessageType);
        Assert.Null(controller.ConnectionResponseType);
        Assert.Null(controller.ConnectionSerializer);
        Assert.Null(controller.CreateConnectionMessage(info));
        Assert.Null(controller.CreateConnectionResponse(info));
    }

    /// <summary>A serializer a host states for the connection message replaces the default.</summary>
    [Fact]
    public void ConnectionSerializer_Stated_ReplacesTheDefault()
    {
        INetworkSerializer serializer = Mock.Of<INetworkSerializer>();
        (_, EngineController controller) = Build(engine => engine.ConnectionMessage<TestHello>(_ => null).ConnectionSerializer(serializer));

        Assert.Same(serializer, controller.ConnectionSerializer);
    }

    /// <summary>External systems are reported in the order added, without duplicates, and the upstream hub is added if it was not already.</summary>
    [Fact]
    public void ExternalSystems_AddedOnceInOrder_AndServerIsAddedIfNew()
    {
        IExternalSystem first = Mock.Of<IExternalSystem>();
        IExternalSystem hub = Mock.Of<IExternalSystem>();
        (_, EngineController controller) = Build(engine => engine.ExternalSystem(first).ExternalSystem(first).ExternalServer(hub));

        Assert.Equal([first, hub], controller.ExternalSystems);
        Assert.Same(hub, controller.ExternalServer);
    }

    /// <summary>OnUserConnected/OnUserDisconnected/OnMessageReceived each accumulate every hook added, in order, rather than replacing the last one.</summary>
    [Fact]
    public void ConnectionAndMessageHooks_AddedInOrder_AllAreKept()
    {
        Action<IUserConnectionHookContext> connectedA = _ => { };
        Action<IUserConnectionHookContext> connectedB = _ => { };
        Action<IUserConnectionHookContext> disconnectedA = _ => { };
        Action<IMessageReceivedHookContext> receivedA = _ => { };
        (_, EngineController controller) = Build(engine => engine
            .OnUserConnected(connectedA).OnUserConnected(connectedB)
            .OnUserDisconnected(disconnectedA)
            .OnMessageReceived(receivedA));

        Assert.Equal([connectedA, connectedB], controller.UserConnectedHooks);
        Assert.Equal([disconnectedA], controller.UserDisconnectedHooks);
        Assert.Equal([receivedA], controller.MessageReceivedHooks);
    }

    private sealed class Dependency(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class InjectedConfiguration(Dependency dependency, ILoggerFactory loggers) : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => new TestEngineConfiguration().Configure(engine)
            .AppName(dependency.Name)
            .HomeText(loggers is null ? "no logging" : "logging");
    }

    /// <summary>The configuration is constructed through dependency injection: it receives the services the host registered and the logging services the engine always provides.</summary>
    [Fact]
    public async Task Build_Generic_InjectsServicesIntoTheConfiguration()
    {
        await using EngineBuilder builder = EngineBuilder.Build<InjectedConfiguration>(services => services.AddSingleton(new Dependency("from-di")));

        Assert.Equal("from-di", builder.AppNameValue);
        Assert.Equal("logging", builder.HomeTextValue);
    }

    /// <summary>A configuration whose dependencies were not registered cannot be constructed, and fails with the container's own error.</summary>
    [Fact]
    public void Build_Generic_MissingDependency_Throws()
        => Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build<InjectedConfiguration>(null));

    private sealed class PlainConfiguration : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => new TestEngineConfiguration().Configure(engine);
    }

    /// <summary>A configuration with no dependencies needs no registrations at all.</summary>
    [Fact]
    public async Task Build_Generic_NoDependencies_NeedsNoRegistrations()
    {
        await using EngineBuilder builder = EngineBuilder.Build<PlainConfiguration>(null);

        Assert.NotNull(builder.MessageMap);
    }

    /// <summary>A configuration that turns out incomplete fails the build, after the container it was built in has been disposed.</summary>
    [Fact]
    public void Build_Generic_IncompleteConfiguration_Throws()
        => Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build<EmptyConfiguration>(null));

    private sealed class DisposableService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class DisposingConfiguration(DisposableService service) : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => new TestEngineConfiguration().Configure(engine).CanDelete(_ => !service.IsDisposed);
    }

    /// <summary>The container the configuration was built in lives until the builder is disposed, since the configuration may have given the engine functions that use what was injected.</summary>
    [Fact]
    public async Task Build_Generic_KeepsTheContainerAliveUntilDisposed()
    {
        DisposableService service = new();
        EngineBuilder builder = EngineBuilder.Build<DisposingConfiguration>(services => services.AddSingleton(_ => service));
        Assert.True(builder.CanDeleteValue!(FolderType.Inbox));

        await builder.DisposeAsync();

        Assert.True(service.IsDisposed);
    }

    /// <summary>Every fluent call returns the builder, so a configuration can be one expression.</summary>
    [Fact]
    public void FluentCalls_ReturnTheBuilder()
    {
        EngineBuilder builder = new();

        Assert.Same(builder, builder.AppName("a").AppVersion("1").DataPath("/d").KioskMode().HomeText("h").PeerPort(1).InterfacePort(2).ConfigFile());
    }

    /// <summary>No server stores messages unless the configuration says so.</summary>
    [Fact]
    public void StorageServers_Unstated_IsEmpty()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Empty(controller.StorageServers);
    }

    /// <summary>ServerStorage names the storing servers; calling it again adds to them without duplicating a name (case-insensitive).</summary>
    [Fact]
    public void ServerStorage_AccumulatesDistinctServerNames()
    {
        (_, EngineController controller) = Build(engine => engine.ServerStorage("Server1", "Server2").ServerStorage("SERVER1", "Server3"));

        Assert.Equal(["Server1", "Server2", "Server3"], controller.StorageServers);
    }
}
