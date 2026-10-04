namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="ServerRoutingService"/> child/server connection classification and message routing.</summary>
public sealed class ServerRoutingServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private static readonly ConnectionPoint serverBPoint = new() { IpAddress = "10.0.0.2", Port = 9002 };
    private static readonly ConnectionPoint serverBSerialPoint = new() { SerialPort = "SL9", SerialAddress = 3 };
    private static readonly ConnectionPoint clientA1SerialPoint = new() { SerialPort = "SL1" };

    private static bool IsRealPayload(ReadOnlyMemory<byte> payload) => !TestHeartbeat.Is(payload);

    private static UserIdentity Identity(string name) => new() { Name = name };

    /// <summary>Builds a connection a remote node opened to this one, identified as <paramref name="user"/>.</summary>
    private static PeerConnection Inbound(string user, Action? drop = null)
        => new(null, new IpConnectionInfo { IsInbound = true }, drop ?? (() => { })) { User = Identity(user) };

    /// <summary>Builds a connection this node opened to <paramref name="point"/>, identified as <paramref name="user"/>.</summary>
    private static PeerConnection Outbound(ConnectionPoint point, string user, Action? drop = null)
        => new(point, new IpConnectionInfo(), drop ?? (() => { })) { User = Identity(user) };

    /// <summary>Builds the connection of a serial link to <paramref name="point"/>, identified as <paramref name="user"/>.</summary>
    private static PeerConnection Serial(ConnectionPoint point, string user)
        => new(point, new SerialConnectionInfo { }, () => { }) { User = Identity(user) };

    private static PeerReceivedEventArgs ReceivedFrom(PeerConnection connection, ReadOnlyMemory<byte> payload)
        => new() { Connection = connection, Payload = payload };

    private sealed class Fixture(
        ServerRoutingService service,
        Mock<IPeerTransport> transport,
        TestObservable<PeerConnectionEventArgs> connected,
        TestObservable<PeerConnectionEventArgs> disconnected,
        TestObservable<PeerReceivedEventArgs> received,
        Task startTask,
        CancellationTokenSource cts)
    {
        public ServerRoutingService Service { get; } = service;
        public Mock<IPeerTransport> Transport { get; } = transport;
        public TestObservable<PeerConnectionEventArgs> Connected { get; } = connected;
        public TestObservable<PeerConnectionEventArgs> Disconnected { get; } = disconnected;
        public TestObservable<PeerReceivedEventArgs> Received { get; } = received;
        public Task StartTask { get; } = startTask;
        public CancellationTokenSource Cts { get; } = cts;

        public void Come(PeerConnection connection) => Connected.Publish(new PeerConnectionEventArgs { Connection = connection });

        public void Lose(PeerConnection connection) => Disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });

        public void Receive(PeerConnection connection, ReadOnlyMemory<byte> payload) => Received.Publish(ReceivedFrom(connection, payload));
    }

    /// <summary>
    /// Builds a service for "ServerA" (this instance) with children ClientA1/ClientA2, alongside "ServerB"
    /// with children ClientB1/ClientB2, and starts it so its receiver is live. <paramref name="outgoing"/> are its
    /// outgoing points (by default none); <paramref name="configureTransport"/>, if given, runs against the mocked
    /// transport before <c>Start</c> is called, so it can also observe the background monitors' own immediate
    /// heartbeats. Every request is acknowledged unless <paramref name="configureTransport"/> says otherwise.
    /// </summary>
    private static async Task<Fixture> BuildStarted(
        Action<Mock<IPeerTransport>, TestObservable<PeerConnectionEventArgs>>? configureTransport = null,
        Dictionary<string, ServerUserConfig>? userMap = null,
        IReadOnlyList<ConnectionPoint>? outgoing = null,
        string self = "ServerA",
        IMessageStorageService? storage = null)
    {
        userMap ??= new Dictionary<string, ServerUserConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerA"] = new ServerUserConfig { Children = ["ClientA1", "ClientA2"] },
            ["ServerB"] = new ServerUserConfig { Children = ["ClientB1", "ClientB2"] }
        };

        Mock<IPeerTransport> transport = new();
        TestObservable<PeerConnectionEventArgs> connected = new();
        TestObservable<PeerConnectionEventArgs> disconnected = new();
        TestObservable<PeerReceivedEventArgs> received = new();
        transport.SetupGet(p => p.Connected).Returns(connected);
        transport.SetupGet(p => p.Disconnected).Returns(disconnected);
        transport.SetupGet(p => p.Received).Returns(received);
        transport.Setup(p => p.Connect(It.IsAny<ConnectionPoint>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("unreachable"));
        transport.Setup(p => p.Request(It.IsAny<PeerConnection>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Mock<IPeerTransportFactory> transportFactory = new();
        transportFactory.Setup(f => f.Create()).Returns(transport.Object);

        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(p => p.Servers).Returns(() => new Dictionary<string, ServerUserConfig>(userMap, StringComparer.OrdinalIgnoreCase));
        engineController.Setup(p => p.PeerPort).Returns(9001);
        engineController.Setup(p => p.OutgoingPoints).Returns(outgoing ?? []);

        Mock<ICurrentUserProvider> currentUser = new();
        currentUser.SetupGet(p => p.UserName).Returns(self);

        ServerRoutingService service = new(transportFactory.Object, engineController.Object, currentUser.Object, storage ?? Mock.Of<IMessageStorageService>(), noLogger);

        configureTransport?.Invoke(transport, connected);

        CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        return new Fixture(service, transport, connected, disconnected, received, startTask, cts);
    }

    /// <summary>Makes <paramref name="point"/> reachable: connecting to it publishes the given connection as established (once) and returns it.</summary>
    private static void Reachable(Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, ConnectionPoint point, PeerConnection connection)
    {
        int isUp = 0;
        transport.Setup(p => p.Connect(point, It.IsAny<CancellationToken>())).Returns(() =>
        {
            if (Interlocked.Exchange(ref isUp, 1) == 0) { connected.Publish(new PeerConnectionEventArgs { Connection = connection }); }
            return Task.FromResult(connection);
        });
    }

    private static readonly ProtobufSerializer serializer = new();

    private static ReadOnlyMemory<byte> Encode(TestFrame message)
    {
        using IMemoryOwner<byte> buf = serializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    private static TestFrame MessageTo(params string[] users) => new()
    {
        MessageId = "M1",
        FromUser = "SOURCE",
        Addresses = [.. users.Select(u => new TestAddressEntry { UserName = u, Type = "To" })]
    };

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(10);
        }
    }

    private static int Requests(Fixture fx, PeerConnection connection, bool real = false)
        => fx.Transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request)
            && ReferenceEquals(i.Arguments[0], connection) && (!real || IsRealPayload((ReadOnlyMemory<byte>)i.Arguments[1])));

    private static int Requests(Mock<IPeerTransport> transport, PeerConnection connection, ReadOnlyMemory<byte> payload)
        => transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request) && ReferenceEquals(i.Arguments[0], connection) && ((ReadOnlyMemory<byte>)i.Arguments[1]).Span.SequenceEqual(payload.Span));

    private static bool SentReal(Fixture fx, PeerConnection connection) => Requests(fx, connection, real: true) > 0;

    private static async Task Stop(Fixture fx)
    {
        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A connection identified as one of this server's children is tracked as a child connection.</summary>
    [Fact]
    public async Task OnConnected_KnownChild_TrackedAsChild()
    {
        Fixture fx = await BuildStarted();
        int drops = 0;

        fx.Come(Inbound("ClientA1", () => drops++));

        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA1" && s.IsConnected && s.Kind == PeerConnectionKind.Client);
        Assert.Equal(0, drops);
        await Stop(fx);
    }

    /// <summary>A known child coming online raises UserConnected and appears in GetConnectedUsers; going offline raises UserDisconnected and removes them.</summary>
    [Fact]
    public async Task OnConnected_KnownChild_RaisesUserConnectedAndUserDisconnected()
    {
        Fixture fx = await BuildStarted();
        List<string> connected = [];
        List<string> disconnectedNames = [];
        fx.Service.UserConnected += name => { connected.Add(name); return Task.CompletedTask; };
        fx.Service.UserDisconnected += name => { disconnectedNames.Add(name); return Task.CompletedTask; };
        PeerConnection connection = Inbound("ClientA1");

        fx.Come(connection);

        await WaitUntil(() => connected.Count > 0, TimeSpan.FromSeconds(30));
        Assert.Equal(["ClientA1"], connected);
        Assert.Contains("ClientA1", fx.Service.GetConnectedUsers());

        fx.Lose(connection);

        await WaitUntil(() => disconnectedNames.Count > 0, TimeSpan.FromSeconds(30));
        Assert.Equal(["ClientA1"], disconnectedNames);
        Assert.DoesNotContain("ClientA1", fx.Service.GetConnectedUsers());
        await Stop(fx);
    }

    /// <summary>A connection dropped as unrecognized never counts as coming online, so it never raises UserConnected.</summary>
    [Fact]
    public async Task OnConnected_UnrecognizedIdentity_DoesNotRaiseUserConnected()
    {
        Fixture fx = await BuildStarted();
        bool raised = false;
        fx.Service.UserConnected += _ => { raised = true; return Task.CompletedTask; };

        fx.Come(Inbound("UNKNOWN-USER"));
        await Task.Delay(50);

        Assert.False(raised);
        await Stop(fx);
    }

    /// <summary>The identity may differ in case from the topology's spelling; the row keeps the configured name.</summary>
    [Fact]
    public async Task OnConnected_IdentityInDifferentCase_MatchesConfiguredName()
    {
        Fixture fx = await BuildStarted();

        fx.Come(Inbound("CLIENTA1"));

        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA1" && s.IsConnected);
        await Stop(fx);
    }

    /// <summary>A connection identified as neither a child nor another server in the cluster is dropped and ignored.</summary>
    [Fact]
    public async Task OnConnected_UnrecognizedIdentity_Dropped()
    {
        Fixture fx = await BuildStarted();
        int drops = 0;

        fx.Come(Inbound("UNKNOWN-USER", () => drops++));

        Assert.Equal(1, drops);
        Assert.All(fx.Service.GetStatuses(), s => Assert.False(s.IsConnected));
        await Stop(fx);
    }

    /// <summary>A connection identified as this server's own name is not a child or another server, so it is dropped.</summary>
    [Fact]
    public async Task OnConnected_IdentifiedAsSelf_Dropped()
    {
        Fixture fx = await BuildStarted();
        int drops = 0;

        fx.Come(Inbound("ServerA", () => drops++));

        Assert.Equal(1, drops);
        await Stop(fx);
    }

    /// <summary>The server listens on the peer port whatever role it plays, since nothing about where it is reached is configured per user.</summary>
    [Fact]
    public async Task Start_StartsListenerOnPeerPort()
    {
        Fixture fx = await BuildStarted();

        fx.Transport.Verify(t => t.StartListener(9001), Times.Once);
        await Stop(fx);
    }

    /// <summary>Reconfigure with an unchanged topology does nothing: no status change is announced and the listener is left alone.</summary>
    [Fact]
    public async Task Reconfigure_NothingChanged_TouchesNothing()
    {
        Fixture fx = await BuildStarted();
        int statusChanges = 0;
        fx.Service.StatusesChanged += () => statusChanges++;

        fx.Service.Reconfigure();

        Assert.Equal(0, statusChanges);
        fx.Transport.Verify(t => t.StopListener(), Times.Never);
        fx.Transport.Verify(t => t.StartListener(It.IsAny<int>()), Times.Once);
        await Stop(fx);
    }

    /// <summary>A child the topology no longer lists is disconnected and dropped from the statuses; the other children keep their connections.</summary>
    [Fact]
    public async Task Reconfigure_ChildRemoved_DisconnectsOnlyThatChild()
    {
        Dictionary<string, ServerUserConfig> userMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerA"] = new ServerUserConfig { Children = ["ClientA1", "ClientA2"] },
            ["ServerB"] = new ServerUserConfig { Children = ["ClientB1"] }
        };
        Fixture fx = await BuildStarted(userMap: userMap);
        int removedDrops = 0;
        int keptDrops = 0;
        fx.Come(Inbound("ClientA1", () => removedDrops++));
        fx.Come(Inbound("ClientA2", () => keptDrops++));
        int statusChanges = 0;
        fx.Service.StatusesChanged += () => statusChanges++;
        userMap["ServerA"] = new ServerUserConfig { Children = ["ClientA2", "ClientA3"] };

        fx.Service.Reconfigure();

        Assert.Equal((1, 0), (removedDrops, keptDrops));
        Assert.DoesNotContain(fx.Service.GetStatuses(), s => s.UserName == "ClientA1");
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA3" && !s.IsConnected);
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA2" && s.IsConnected);
        Assert.Equal(1, statusChanges);
        await Stop(fx);
    }

    /// <summary>A server whose own name is missing from the topology cannot route, and starts nothing.</summary>
    [Fact]
    public async Task Start_SelfNotInTopology_StartsNothing()
    {
        Fixture fx = await BuildStarted(self: "Stranger", outgoing: [serverBPoint]);

        fx.Transport.Verify(t => t.StartListener(It.IsAny<int>()), Times.Never);
        fx.Transport.Verify(t => t.Connect(It.IsAny<ConnectionPoint>(), It.IsAny<CancellationToken>()), Times.Never);
        await Stop(fx);
    }

    /// <summary>Every outgoing point, IP or serial, is connected to and kept connected, without knowing who is behind it.</summary>
    [Fact]
    public async Task Start_MaintainsEveryOutgoingPoint()
    {
        Fixture fx = await BuildStarted(outgoing: [serverBPoint, serverBSerialPoint]);

        await WaitUntil(
            () => fx.Transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Connect) && Equals(i.Arguments[0], serverBPoint))
                && fx.Transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Connect) && Equals(i.Arguments[0], serverBSerialPoint)),
            TimeSpan.FromSeconds(2));

        await Stop(fx);
    }

    /// <summary>A message from one child addressed to a sibling child goes straight over that sibling's connection, not to any server.</summary>
    [Fact]
    public async Task FromChild_AddressedToSiblingChild_RoutesToSibling()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection clientA2 = Inbound("ClientA2");
        PeerConnection serverB = Inbound("ServerB");
        fx.Come(clientA1);
        fx.Come(clientA2);
        fx.Come(serverB);

        fx.Receive(clientA1, Encode(MessageTo("ClientA2")));

        await WaitUntil(() => SentReal(fx, clientA2), TimeSpan.FromSeconds(30));
        Assert.Equal(1, Requests(fx, clientA2, real: true));
        Assert.Equal(0, Requests(fx, serverB, real: true));
        Assert.Equal(0, Requests(fx, clientA1, real: true));
        await Stop(fx);
    }

    private static Dictionary<string, ServerUserConfig> WithRelay() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["ServerA"] = new ServerUserConfig { Children = ["ClientA1", "RelayA"], Relays = new Dictionary<string, IReadOnlyList<string>> { ["RelayA"] = ["ClientR1", "ClientR2"] } },
        ["ServerB"] = new ServerUserConfig { Children = ["ClientB1"] }
    };

    /// <summary>A message for clients behind a relay goes to that relay once, as the original bytes, not to the clients themselves.</summary>
    [Fact]
    public async Task FromChild_AddressedToClientsBehindARelay_ForwardsToTheRelayOnce()
    {
        Fixture fx = await BuildStarted(userMap: WithRelay());
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection relayA = Inbound("RelayA");
        fx.Come(clientA1);
        fx.Come(relayA);
        ReadOnlyMemory<byte> payload = Encode(MessageTo("ClientR1", "ClientR2"));

        fx.Receive(clientA1, payload);

        await WaitUntil(() => SentReal(fx, relayA), TimeSpan.FromSeconds(30));
        await Task.Delay(100);
        Assert.Equal(1, Requests(fx, relayA, real: true));
        Assert.Equal(0, Requests(fx, clientA1, real: true));
        Assert.Equal(1, Requests(fx.Transport, relayA, payload));
        await Stop(fx);
    }

    /// <summary>A message a relay forwards up from one of its clients is routed on to a client behind another relay of this server.</summary>
    [Fact]
    public async Task FromRelay_AddressedToLocalClient_RoutesToThatClient()
    {
        Fixture fx = await BuildStarted(userMap: WithRelay());
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection relayA = Inbound("RelayA");
        fx.Come(clientA1);
        fx.Come(relayA);

        fx.Receive(relayA, Encode(MessageTo("ClientA1")));

        await WaitUntil(() => SentReal(fx, clientA1), TimeSpan.FromSeconds(30));
        Assert.Equal(0, Requests(fx, relayA, real: true));
        await Stop(fx);
    }

    /// <summary>A message for a client behind another server's relay is forwarded to that server.</summary>
    [Fact]
    public async Task FromChild_AddressedToClientBehindARemoteServersRelay_ForwardsToThatServer()
    {
        Dictionary<string, ServerUserConfig> map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerA"] = new ServerUserConfig { Children = ["ClientA1"] },
            ["ServerB"] = new ServerUserConfig { Children = ["RelayB"], Relays = new Dictionary<string, IReadOnlyList<string>> { ["RelayB"] = ["ClientRB1"] } }
        };
        Fixture fx = await BuildStarted(userMap: map);
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection serverB = Inbound("ServerB");
        fx.Come(clientA1);
        fx.Come(serverB);

        fx.Receive(clientA1, Encode(MessageTo("ClientRB1")));

        await WaitUntil(() => SentReal(fx, serverB), TimeSpan.FromSeconds(30));
        Assert.Equal(1, Requests(fx, serverB, real: true));
        await Stop(fx);
    }

    /// <summary>An external address is never routed, even when it is spelled like a child or a server: the server takes no action for it.</summary>
    [Fact]
    public async Task FromChild_ExternalAddressNamedLikeAChild_IsNotRelayed()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection clientA2 = Inbound("ClientA2");
        PeerConnection serverB = Inbound("ServerB");
        fx.Come(clientA1);
        fx.Come(clientA2);
        fx.Come(serverB);
        TestFrame message = new()
        {
            MessageId = "M1",
            FromUser = "SOURCE",
            Addresses = [new TestAddressEntry { UserName = "ClientA2", Type = "External" }, new TestAddressEntry { UserName = "ClientB1", Type = "External", Information = "By hand" }]
        };

        fx.Receive(clientA1, Encode(message));

        await Task.Delay(100);
        Assert.Equal(0, Requests(fx, clientA2, real: true));
        Assert.Equal(0, Requests(fx, serverB, real: true));
        await Stop(fx);
    }

    /// <summary>A message from a child addressed to a child of another server is forwarded over that server's connection once.</summary>
    [Fact]
    public async Task FromChild_AddressedToRemoteServersChild_ForwardsToThatServerOnce()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection serverB = Inbound("ServerB");
        fx.Come(clientA1);
        fx.Come(serverB);

        // Addressed to both of ServerB's children, which should still forward to ServerB exactly once.
        fx.Receive(clientA1, Encode(MessageTo("ClientB1", "ClientB2")));

        await WaitUntil(() => SentReal(fx, serverB), TimeSpan.FromSeconds(30));
        await Task.Delay(50);
        Assert.Equal(1, Requests(fx, serverB, real: true));
        await Stop(fx);
    }

    /// <summary>A server reached over a connection this server opened is forwarded to just the same as one that opened a connection to it.</summary>
    [Fact]
    public async Task FromChild_ForwardToServerReachedByOutgoingPoint_UsesThatConnection()
    {
        PeerConnection serverB = Outbound(serverBPoint, "ServerB");
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => Reachable(transport, connected, serverBPoint, serverB), outgoing: [serverBPoint]);
        PeerConnection clientA1 = Inbound("ClientA1");
        fx.Come(clientA1);
        await WaitUntil(() => fx.Service.GetStatuses().Single(s => s.UserName == "ServerB").IsConnected, TimeSpan.FromSeconds(30));

        fx.Receive(clientA1, Encode(MessageTo("ClientB1")));

        await WaitUntil(() => SentReal(fx, serverB), TimeSpan.FromSeconds(30));
        await Stop(fx);
    }

    /// <summary>A recipient with no connection identified as them is skipped, not thrown for.</summary>
    [Fact]
    public async Task FromChild_RecipientNotConnected_DoesNotSendOrThrow()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        fx.Come(clientA1);

        fx.Receive(clientA1, Encode(MessageTo("ClientA2")));

        await Task.Delay(50);
        fx.Transport.Verify(p => p.Request(It.IsAny<PeerConnection>(), It.Is<ReadOnlyMemory<byte>>(payload => IsRealPayload(payload)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        await Stop(fx);
    }

    /// <summary>A message received from another server is delivered only to local children it addresses, never re-forwarded to other servers.</summary>
    [Fact]
    public async Task FromServer_AddressedToLocalChild_DeliversLocallyOnlyNeverReforwarded()
    {
        Fixture fx = await BuildStarted();
        PeerConnection serverB = Inbound("ServerB");
        PeerConnection clientA1 = Inbound("ClientA1");
        fx.Come(serverB);
        fx.Come(clientA1);

        fx.Receive(serverB, Encode(MessageTo("ClientA1", "ClientB1")));

        await WaitUntil(() => SentReal(fx, clientA1), TimeSpan.FromSeconds(30));
        await Task.Delay(50);
        Assert.Equal(1, Requests(fx, clientA1, real: true));
        Assert.Equal(0, Requests(fx, serverB, real: true));
        await Stop(fx);
    }

    /// <summary>GetStatuses returns one row per own child client (ClientA1, ClientA2) plus one row for the other server (ServerB), excluding this instance's own name, all initially disconnected.</summary>
    [Fact]
    public async Task GetStatuses_Started_ReturnsChildAndServerRowsDisconnected()
    {
        Fixture fx = await BuildStarted();

        IReadOnlyList<PeerConnectionStatus> statuses = fx.Service.GetStatuses();

        Assert.Equal(3, statuses.Count);
        Assert.Contains(statuses, s => s.UserName == "ClientA1" && !s.IsConnected && s.Kind == PeerConnectionKind.Client);
        Assert.Contains(statuses, s => s.UserName == "ClientA2" && !s.IsConnected);
        Assert.Contains(statuses, s => s.UserName == "ServerB" && !s.IsConnected && s.Kind == PeerConnectionKind.Server);
        await Stop(fx);
    }

    /// <summary>
    /// Even with no real message ever routed, the background monitor of an outgoing point proactively connects and sends
    /// an empty heartbeat, and GetStatuses reports the server behind it connected once that heartbeat is acknowledged.
    /// </summary>
    [Fact]
    public async Task GetStatuses_OutgoingPointHeartbeat_ReportsServerConnectedWithoutAnyRealTraffic()
    {
        PeerConnection serverB = Outbound(serverBPoint, "ServerB");
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => Reachable(transport, connected, serverBPoint, serverB), outgoing: [serverBPoint]);

        await WaitUntil(() => fx.Service.GetStatuses().Single(s => s.UserName == "ServerB").IsConnected, TimeSpan.FromSeconds(30));

        fx.Transport.Verify(p => p.Request(serverB, It.Is<ReadOnlyMemory<byte>>(payload => TestHeartbeat.Is(payload)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB");
        Assert.NotNull(status.LastConnectedAt);
        await Stop(fx);
    }

    /// <summary>A bare outbound IP connection to another server does not count as up until a heartbeat over it is acknowledged, so a server that closed this one and drops the connection straight away never flashes green.</summary>
    [Fact]
    public async Task GetStatuses_ServerConnectedOutboundWithoutHeartbeatAck_StaysDown()
    {
        Fixture fx = await BuildStarted();
        int raised = 0;
        fx.Service.StatusesChanged += () => raised++;

        fx.Come(Outbound(serverBPoint, "ServerB"));

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB");
        Assert.False(status.IsConnected);
        Assert.Null(status.LastConnectedAt);
        Assert.Equal(0, raised);
        await Stop(fx);
    }

    /// <summary>After a known child's connection disconnects, GetStatuses reports it disconnected with a LastDisconnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ChildDisconnects_ReturnsDisconnectedRowWithLastDisconnectedAt()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");

        fx.Come(clientA1);
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA1" && s.IsConnected);
        fx.Lose(clientA1);

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA1");
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.NotNull(status.LastDisconnectedAt);
        await Stop(fx);
    }

    /// <summary>Once a known child client connects, GetStatuses reports its row as connected with a LastConnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ChildConnects_ReturnsConnectedChildRow()
    {
        Fixture fx = await BuildStarted();

        fx.Come(Inbound("ClientA1"));

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA1");
        Assert.True(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.Null(status.LastDisconnectedAt);
        await Stop(fx);
    }

    /// <summary>Once a recognized sibling server connects and then disconnects, GetStatuses reports its row as disconnected with a LastDisconnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ServerDisconnects_ReturnsDisconnectedRowWithLastDisconnectedAt()
    {
        Fixture fx = await BuildStarted();
        PeerConnection serverB = Inbound("ServerB");

        fx.Come(serverB);
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ServerB" && s.IsConnected);
        fx.Lose(serverB);

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB");
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.NotNull(status.LastDisconnectedAt);
        await Stop(fx);
    }

    /// <summary>Malformed (non-empty, non-deserializable) bytes from a recognized child are dropped silently: no relay Request happens and nothing throws.</summary>
    [Fact]
    public async Task FromChild_MalformedPayload_IsDroppedWithoutSendOrThrow()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(clientA1);
        fx.Come(clientA2);

        fx.Receive(clientA1, new byte[] { 0xFF, 0xFE, 0xFD });

        await Task.Delay(50);
        fx.Transport.Verify(p => p.Request(It.IsAny<PeerConnection>(), It.Is<ReadOnlyMemory<byte>>(payload => IsRealPayload(payload)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        await Stop(fx);
    }

    /// <summary>A message from a child with no identifier is invalid: it is neither routed nor stored.</summary>
    [Fact]
    public async Task FromChild_MessageWithoutId_IsDropped()
    {
        Mock<IMessageStorageService> storage = new();
        Fixture fx = await BuildStarted(storage: storage.Object);
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(clientA1);
        fx.Come(clientA2);
        TestFrame message = MessageTo("ClientA2");
        message.MessageId = string.Empty;

        fx.Receive(clientA1, Encode(message));

        await Task.Delay(150);
        Assert.Equal(0, Requests(fx, clientA2));
        storage.Verify(s => s.Store(It.IsAny<object>()), Times.Never);
        await Stop(fx);
    }

    /// <summary>A heartbeat (an empty message) from a child is not a real message and is not relayed or stored.</summary>
    [Fact]
    public async Task FromChild_Heartbeat_IsIgnored()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(clientA1);
        fx.Come(clientA2);

        fx.Receive(clientA1, TestHeartbeat.Bytes());

        await Task.Delay(50);
        Assert.Equal(0, Requests(fx, clientA2));
        await Stop(fx);
    }

    /// <summary>A message on a connection that was rejected, or never announced, is ignored.</summary>
    [Fact]
    public async Task Received_OnUnknownConnection_IsIgnored()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(clientA2);

        fx.Receive(Inbound("ClientA1"), Encode(MessageTo("ClientA2")));
        fx.Receive(Inbound("Stranger"), Encode(MessageTo("ClientA2")));

        await Task.Delay(50);
        Assert.Equal(0, Requests(fx, clientA2));
        await Stop(fx);
    }

    /// <summary>A server does not compose messages, only transports them, so sending one of its own fails and nothing goes out.</summary>
    [Fact]
    public async Task Send_FromTheServerItself_Fails()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(clientA2);

        bool ok = await fx.Service.Send("ClientA2", MessageTo("ClientA2"));

        Assert.False(ok);
        Assert.Equal(0, Requests(fx, clientA2, real: true));
        await Stop(fx);
    }

    /// <summary>A child cabled over serial is recognized by the identity its link was given, and its link coming up and going down drives its status row.</summary>
    [Fact]
    public async Task SerialChild_ConnectedThenDisconnected_TracksStatus()
    {
        Fixture fx = await BuildStarted();
        PeerConnection link = Serial(clientA1SerialPoint, "ClientA1");

        fx.Come(link);
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA1" && s.IsConnected && s.Kind == PeerConnectionKind.Client);

        fx.Lose(link);
        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA1");
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastDisconnectedAt);
        await Stop(fx);
    }

    /// <summary>A sibling server cabled over serial is tracked as a server row.</summary>
    [Fact]
    public async Task SerialServer_ConnectedThenDisconnected_TracksStatus()
    {
        Fixture fx = await BuildStarted();
        PeerConnection link = Serial(serverBSerialPoint, "ServerB");

        fx.Come(link);
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ServerB" && s.IsConnected && s.Kind == PeerConnectionKind.Server);

        fx.Lose(link);
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ServerB" && !s.IsConnected);
        await Stop(fx);
    }

    /// <summary>A message arriving on a child's serial link is attributed to that child and routed onward like any other child message, over the serial link to a serial server.</summary>
    [Fact]
    public async Task SerialChild_MessageReceived_RoutedAsFromThatChild()
    {
        Fixture fx = await BuildStarted();
        PeerConnection link = Serial(clientA1SerialPoint, "ClientA1");
        PeerConnection serverB = Serial(serverBSerialPoint, "ServerB");
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(link);
        fx.Come(serverB);
        fx.Come(clientA2);

        fx.Receive(link, Encode(MessageTo("ClientA2", "ClientB1")));

        await WaitUntil(() => SentReal(fx, clientA2) && SentReal(fx, serverB), TimeSpan.FromSeconds(30));
        Assert.Equal(1, Requests(fx, clientA2, real: true));
        Assert.Equal(1, Requests(fx, serverB, real: true));
        await Stop(fx);
    }

    /// <summary>A message arriving on the serial link of a sibling server is treated as already routed: delivered to local children only, never re-forwarded.</summary>
    [Fact]
    public async Task SerialServer_MessageReceived_DeliversToLocalChildrenOnly()
    {
        Fixture fx = await BuildStarted();
        PeerConnection serverB = Serial(serverBSerialPoint, "ServerB");
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(serverB);
        fx.Come(clientA2);

        fx.Receive(serverB, Encode(MessageTo("ClientA2", "ClientB1")));

        await WaitUntil(() => SentReal(fx, clientA2), TimeSpan.FromSeconds(30));
        Assert.Equal(0, Requests(fx, serverB, real: true));
        await Stop(fx);
    }

    /// <summary>A message arriving on a serial link nobody has been identified on is ignored.</summary>
    [Fact]
    public async Task SerialUnknownLink_MessageReceived_Ignored()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(clientA2);

        fx.Receive(Serial(new ConnectionPoint { SerialPort = "NOPE" }, "NOPE"), Encode(MessageTo("ClientA2")));

        await Task.Delay(50);
        Assert.Equal(0, Requests(fx, clientA2));
        await Stop(fx);
    }

    /// <summary>Closing a child marks its row closed and down, drops its connections, and leaves other rows alone.</summary>
    [Fact]
    public async Task SetClosed_Child_DropsConnectionsAndMarksRow()
    {
        Fixture fx = await BuildStarted();
        int drops = 0;
        fx.Come(Inbound("ClientA1", () => drops++));
        fx.Come(Inbound("ClientA2"));
        int raised = 0;
        fx.Service.StatusesChanged += () => raised++;

        fx.Service.SetClosed(PeerConnectionKind.Client, "ClientA1", true);

        Assert.Equal(1, drops);
        PeerConnectionStatus closed = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA1");
        Assert.True(closed.IsClosed);
        Assert.False(closed.IsConnected);
        Assert.NotNull(closed.LastDisconnectedAt);
        PeerConnectionStatus other = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA2");
        Assert.False(other.IsClosed);
        Assert.True(other.IsConnected);
        Assert.True(raised > 0);
        await Stop(fx);
    }

    /// <summary>Closing a sibling server closes the outgoing point it was reached through, and stops that point's heartbeats.</summary>
    [Fact]
    public async Task SetClosed_Server_ClosesItsOutgoingPoint()
    {
        PeerConnection serverB = Outbound(serverBPoint, "ServerB");
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => Reachable(transport, connected, serverBPoint, serverB), outgoing: [serverBPoint]);
        await WaitUntil(() => fx.Service.GetStatuses().Single(s => s.UserName == "ServerB").IsConnected, TimeSpan.FromSeconds(30));

        fx.Service.SetClosed(PeerConnectionKind.Server, "ServerB", true);

        fx.Transport.Verify(t => t.SetClosed(serverBPoint, true), Times.Once);
        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB");
        Assert.True(status.IsClosed);
        Assert.False(status.IsConnected);
        await Stop(fx);
    }

    /// <summary>A closed child's existing connection is dropped, and new ones from it are rejected until it is reopened.</summary>
    [Fact]
    public async Task SetClosed_Child_DropsExistingAndRejectsNewUntilReopened()
    {
        Fixture fx = await BuildStarted();
        int existing = 0;
        int rejected = 0;
        int accepted = 0;
        fx.Come(Inbound("ClientA1", () => existing++));

        fx.Service.SetClosed(PeerConnectionKind.Client, "ClientA1", true);
        fx.Come(Inbound("ClientA1", () => rejected++));
        Assert.False(Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA1").IsConnected);

        fx.Service.SetClosed(PeerConnectionKind.Client, "ClientA1", false);
        fx.Come(Inbound("ClientA1", () => accepted++));

        Assert.Equal(1, existing);
        Assert.Equal(1, rejected);
        Assert.Equal(0, accepted);
        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA1");
        Assert.False(status.IsClosed);
        Assert.True(status.IsConnected);
        await Stop(fx);
    }

    /// <summary>Nothing is delivered to a closed child, while other children still receive.</summary>
    [Fact]
    public async Task SetClosed_Child_MessagesToItAreNotSent()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(clientA1);
        fx.Come(clientA2);
        fx.Service.SetClosed(PeerConnectionKind.Client, "ClientA2", true);

        fx.Receive(clientA1, Encode(MessageTo("ClientA2")));
        await Task.Delay(100);

        Assert.Equal(0, Requests(fx, clientA2, real: true));
        await Stop(fx);
    }

    /// <summary>Nothing is forwarded to a closed sibling server.</summary>
    [Fact]
    public async Task SetClosed_Server_MessagesToItAreNotForwarded()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection serverB = Inbound("ServerB");
        fx.Come(clientA1);
        fx.Come(serverB);
        fx.Service.SetClosed(PeerConnectionKind.Server, "ServerB", true);

        fx.Receive(clientA1, Encode(MessageTo("ClientB1")));
        await Task.Delay(100);

        Assert.Equal(0, Requests(fx, serverB, real: true));
        await Stop(fx);
    }

    /// <summary>A closed server's outgoing point gets no heartbeats, and reopening it resumes them straight away.</summary>
    [Fact]
    public async Task SetClosed_Server_StopsHeartbeats_ReopenResumesThem()
    {
        PeerConnection serverB = Outbound(serverBPoint, "ServerB");
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => Reachable(transport, connected, serverBPoint, serverB), outgoing: [serverBPoint]);
        await WaitUntil(() => Requests(fx, serverB) >= 1, TimeSpan.FromSeconds(30));

        fx.Service.SetClosed(PeerConnectionKind.Server, "ServerB", true);
        await Task.Delay(100);
        int whileClosed = Requests(fx, serverB);
        await Task.Delay(150);
        Assert.Equal(whileClosed, Requests(fx, serverB));

        fx.Service.SetClosed(PeerConnectionKind.Server, "ServerB", false);
        await WaitUntil(() => Requests(fx, serverB) > whileClosed, TimeSpan.FromSeconds(30));
        fx.Transport.Verify(t => t.SetClosed(serverBPoint, false), Times.Once);
        await Stop(fx);
    }

    /// <summary>Refresh resets the point a server is reached through, drops its connections, and heartbeats straight away; it does nothing while closed.</summary>
    [Fact]
    public async Task Refresh_Server_ResetsPointDropsConnectionsAndHeartbeatsNow_IgnoredWhenClosed()
    {
        int drops = 0;
        PeerConnection serverB = Outbound(serverBPoint, "ServerB", () => drops++);
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => Reachable(transport, connected, serverBPoint, serverB), outgoing: [serverBPoint]);
        await WaitUntil(() => Requests(fx, serverB) >= 1, TimeSpan.FromSeconds(30));
        int before = Requests(fx, serverB);

        fx.Service.Refresh(PeerConnectionKind.Server, "ServerB");

        fx.Transport.Verify(t => t.Reset(serverBPoint), Times.Once);
        Assert.Equal(1, drops);
        await WaitUntil(() => Requests(fx, serverB) > before, TimeSpan.FromSeconds(30));

        fx.Service.SetClosed(PeerConnectionKind.Server, "ServerB", true);
        fx.Service.Refresh(PeerConnectionKind.Server, "ServerB");
        fx.Transport.Verify(t => t.Reset(serverBPoint), Times.Once);
        await Stop(fx);
    }

    /// <summary>Refreshing a child that connected to this server drops its connection so it forms a new one.</summary>
    [Fact]
    public async Task Refresh_Child_DropsItsConnections()
    {
        Fixture fx = await BuildStarted();
        int drops = 0;
        fx.Come(Inbound("ClientA1", () => drops++));

        fx.Service.Refresh(PeerConnectionKind.Client, "ClientA1");

        Assert.Equal(1, drops);
        await Stop(fx);
    }

    /// <summary>Closing or refreshing a name the server does not track does nothing.</summary>
    [Fact]
    public async Task SetClosedAndRefresh_UnknownName_DoNothing()
    {
        Fixture fx = await BuildStarted(outgoing: [serverBPoint]);
        int drops = 0;
        fx.Come(Inbound("ClientA1", () => drops++));

        fx.Service.SetClosed(PeerConnectionKind.Client, "Nobody", true);
        fx.Service.Refresh(PeerConnectionKind.Client, "Nobody");

        fx.Transport.Verify(t => t.SetClosed(It.IsAny<ConnectionPoint>(), It.IsAny<bool>()), Times.Never);
        fx.Transport.Verify(t => t.Reset(It.IsAny<ConnectionPoint>()), Times.Never);
        Assert.Equal(0, drops);
        await Stop(fx);
    }

    /// <summary>An outbound connection going down is reflected on its row, not only inbound ones.</summary>
    [Fact]
    public async Task OutboundConnectionDisconnects_MarksRowDown()
    {
        PeerConnection serverB = Outbound(serverBPoint, "ServerB");
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => Reachable(transport, connected, serverBPoint, serverB), outgoing: [serverBPoint]);
        await WaitUntil(() => fx.Service.GetStatuses().Single(s => s.UserName == "ServerB").IsConnected, TimeSpan.FromSeconds(30));

        fx.Lose(serverB);

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB");
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastDisconnectedAt);
        await Stop(fx);
    }

    /// <summary>A sibling server connected both ways stays up when one direction drops, and only goes down once neither is left.</summary>
    [Fact]
    public async Task OneOfTwoConnectionsDrops_RowStaysUpUntilBothAreGone()
    {
        PeerConnection outbound = Outbound(serverBPoint, "ServerB");
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => Reachable(transport, connected, serverBPoint, outbound), outgoing: [serverBPoint]);
        PeerConnection inbound = Inbound("ServerB");
        fx.Come(inbound);
        await WaitUntil(() => fx.Service.GetStatuses().Single(s => s.UserName == "ServerB").IsConnected, TimeSpan.FromSeconds(30));

        fx.Lose(inbound);
        Assert.True(Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB").IsConnected);

        fx.Lose(outbound);
        Assert.False(Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB").IsConnected);
        await Stop(fx);
    }

    /// <summary>An unexpected drop of the only live connection through an outgoing point wakes its heartbeat monitor immediately, instead of leaving it to sleep out its current interval before retrying.</summary>
    [Fact]
    public async Task OnDisconnected_UnexpectedDrop_RetriesHeartbeatImmediately()
    {
        PeerConnection serverB = Outbound(serverBPoint, "ServerB");
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => Reachable(transport, connected, serverBPoint, serverB), outgoing: [serverBPoint]);
        await WaitUntil(() => fx.Service.GetStatuses().Single(s => s.UserName == "ServerB").IsConnected, TimeSpan.FromSeconds(30));
        int countBefore = Requests(fx, serverB);

        fx.Lose(serverB);

        await WaitUntil(() => Requests(fx, serverB) > countBefore, TimeSpan.FromSeconds(30));
        await Stop(fx);
    }

    /// <summary>A message relayed by the server keeps the priority it was sent with, for children and for other servers alike.</summary>
    [Fact]
    public async Task Relay_KeepsMessagePriority()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection clientA2 = Inbound("ClientA2");
        PeerConnection serverB = Inbound("ServerB");
        fx.Come(clientA1);
        fx.Come(clientA2);
        fx.Come(serverB);
        TestFrame message = MessageTo("ClientA2", "ClientB1");
        message.Priority = "LEVEL7";

        fx.Receive(clientA1, Encode(message));

        await WaitUntil(() => SentReal(fx, clientA2) && SentReal(fx, serverB), TimeSpan.FromSeconds(30));
        fx.Transport.Verify(p => p.Request(clientA2, It.Is<ReadOnlyMemory<byte>>(payload => IsRealPayload(payload)), It.Is<PeerSendOptions>(o => o.Priority == 7), It.IsAny<CancellationToken>()), Times.Once);
        fx.Transport.Verify(p => p.Request(serverB, It.Is<ReadOnlyMemory<byte>>(payload => IsRealPayload(payload)), It.Is<PeerSendOptions>(o => o.Priority == 7), It.IsAny<CancellationToken>()), Times.Once);
        await Stop(fx);
    }

    /// <summary>One unreachable recipient does not hold up delivery to the others: sends go out concurrently.</summary>
    [Fact]
    public async Task Relay_SlowRecipient_DoesNotDelayOthers()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection clientA2 = Inbound("ClientA2");
        PeerConnection serverB = Inbound("ServerB");
        TaskCompletionSource<bool> stuck = new();
        fx.Transport.Setup(p => p.Request(clientA2, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>())).Returns(stuck.Task);
        fx.Come(clientA1);
        fx.Come(clientA2);
        fx.Come(serverB);

        fx.Receive(clientA1, Encode(MessageTo("ClientA2", "ClientB1")));

        await WaitUntil(() => SentReal(fx, serverB), TimeSpan.FromSeconds(30));
        stuck.SetResult(true);
        await Stop(fx);
    }

    /// <summary>A message that arrives from a user after its connection was closed is not relayed.</summary>
    [Fact]
    public async Task MessageFromClosedUser_IsNotRelayed()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection clientA2 = Inbound("ClientA2");
        fx.Come(clientA1);
        fx.Come(clientA2);
        fx.Service.SetClosed(PeerConnectionKind.Client, "ClientA1", true);

        fx.Receive(clientA1, Encode(MessageTo("ClientA2")));
        await Task.Delay(100);

        Assert.Equal(0, Requests(fx, clientA2, real: true));
        await Stop(fx);
    }

    /// <summary>Registered as both IPeerService and IConnectionStatusService, the service is disposed twice; the second is a no-op.</summary>
    [Fact]
    public async Task DisposeAsync_Twice_DisposesTransportOnce()
    {
        Fixture fx = await BuildStarted();
        await Stop(fx);

        await fx.Service.DisposeAsync();
        await fx.Service.DisposeAsync();

        fx.Transport.Verify(t => t.DisposeAsync(), Times.Once);
    }

    /// <summary>A heartbeat acknowledged for a user that has since been closed does not bring its row back up.</summary>
    [Fact]
    public async Task HeartbeatAcknowledged_ForClosedUser_DoesNotMarkUp()
    {
        PeerConnection serverB = Outbound(serverBPoint, "ServerB");
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => Reachable(transport, connected, serverBPoint, serverB), outgoing: [serverBPoint]);
        await WaitUntil(() => fx.Service.GetStatuses().Single(s => s.UserName == "ServerB").IsConnected, TimeSpan.FromSeconds(30));

        fx.Service.SetClosed(PeerConnectionKind.Server, "ServerB", true);
        await Task.Delay(100);

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB");
        Assert.True(status.IsClosed);
        Assert.False(status.IsConnected);
        await Stop(fx);
    }

    private static TestFrame RetrievalTo(string server, string from) => new()
    {
        MessageId = "REQ1",
        FromUser = from,
        IsRetrieval = true,
        Addresses = [new TestAddressEntry { UserName = server, Type = "To" }]
    };

    /// <summary>A message routed from a child is handed to storage.</summary>
    [Fact]
    public async Task FromChild_Message_IsHandedToStorage()
    {
        Mock<IMessageStorageService> storage = new();
        Fixture fx = await BuildStarted(storage: storage.Object);
        PeerConnection clientA1 = Inbound("ClientA1");
        fx.Come(clientA1);

        fx.Receive(clientA1, Encode(MessageTo("ClientA2")));

        await WaitUntil(() => storage.Invocations.Count > 0, TimeSpan.FromSeconds(30));
        storage.Verify(s => s.Store(It.Is<object>(m => ((TestFrame)m).MessageId == "M1")), Times.Once);
        await Stop(fx);
    }

    /// <summary>A message routed from another server is not stored: a server stores only what its own children send.</summary>
    [Fact]
    public async Task FromServer_Message_IsNotStored()
    {
        Mock<IMessageStorageService> storage = new();
        Fixture fx = await BuildStarted(storage: storage.Object);
        PeerConnection serverB = Inbound("ServerB");
        fx.Come(serverB);

        fx.Receive(serverB, Encode(MessageTo("ClientA1")));

        await Task.Delay(300);
        storage.Verify(s => s.Store(It.IsAny<object>()), Times.Never);
        await Stop(fx);
    }

    /// <summary>A retrieval request addressed to this server is answered: each found copy is routed to the requesting child, and the request itself is never stored or relayed.</summary>
    [Fact]
    public async Task FromChild_RetrievalRequestForThisServer_SendsFoundCopiesToRequester()
    {
        Mock<IMessageStorageService> storage = new();
        TestFrame copy = MessageTo("ClientA1");
        storage.Setup(s => s.Find("ClientA1", It.IsAny<object>())).ReturnsAsync(new List<object> { copy });
        Fixture fx = await BuildStarted(storage: storage.Object);
        PeerConnection clientA1 = Inbound("ClientA1");
        fx.Come(clientA1);

        fx.Receive(clientA1, Encode(RetrievalTo("ServerA", "ClientA1")));

        await WaitUntil(() => SentReal(fx, clientA1), TimeSpan.FromSeconds(30));
        Assert.Equal(1, Requests(fx, clientA1, real: true));
        storage.Verify(s => s.Store(It.IsAny<object>()), Times.Never);
        await Stop(fx);
    }

    /// <summary>A retrieval request addressed to another server is forwarded to it and not answered here.</summary>
    [Fact]
    public async Task FromChild_RetrievalRequestForAnotherServer_IsForwardedNotAnswered()
    {
        Mock<IMessageStorageService> storage = new();
        Fixture fx = await BuildStarted(storage: storage.Object);
        PeerConnection clientA1 = Inbound("ClientA1");
        PeerConnection serverB = Inbound("ServerB");
        fx.Come(clientA1);
        fx.Come(serverB);

        fx.Receive(clientA1, Encode(RetrievalTo("ServerB", "ClientA1")));

        await WaitUntil(() => SentReal(fx, serverB), TimeSpan.FromSeconds(30));
        Assert.Equal(1, Requests(fx, serverB, real: true));
        storage.Verify(s => s.Find(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        storage.Verify(s => s.Store(It.IsAny<object>()), Times.Never);
        await Stop(fx);
    }

    /// <summary>A retrieval request forwarded from another server is answered for the user named as its sender, and the copies are routed back through that server.</summary>
    [Fact]
    public async Task FromServer_RetrievalRequestForThisServer_AnswersForTheSenderAndRoutesBack()
    {
        Mock<IMessageStorageService> storage = new();
        storage.Setup(s => s.Find("ClientB1", It.IsAny<object>())).ReturnsAsync(new List<object> { MessageTo("ClientB1") });
        Fixture fx = await BuildStarted(storage: storage.Object);
        PeerConnection serverB = Inbound("ServerB");
        fx.Come(serverB);

        fx.Receive(serverB, Encode(RetrievalTo("ServerA", "ClientB1")));

        await WaitUntil(() => SentReal(fx, serverB), TimeSpan.FromSeconds(30));
        Assert.Equal(1, Requests(fx, serverB, real: true));
        await Stop(fx);
    }
}
