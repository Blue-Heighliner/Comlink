namespace BlueHeighliner.Comlink.Tests.Unit.Peer;

/// <summary>Unit tests for <see cref="ServerRoutingService"/> child/server connection classification and message routing.</summary>
public sealed class ServerRoutingServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private static readonly UserEndpoint serverAEndpoint = new() { IpAddress = "10.0.0.1", Port = 9001 };
    private static readonly UserEndpoint serverBEndpoint = new() { IpAddress = "10.0.0.2", Port = 9002 };
    private static readonly UserEndpoint clientA1Endpoint = new() { IpAddress = "10.0.1.1", Port = 9101 };
    private static readonly UserEndpoint clientA2Endpoint = new() { IpAddress = "10.0.1.2", Port = 9102 };
    private static readonly UserEndpoint clientA1SerialEndpoint = new() { SerialPort = "SL1" };
    private static readonly UserEndpoint serverBSerialEndpoint = new() { SerialPort = "SL9", SerialAddress = 3 };

    /// <summary>Distinguishes a real routed message from a background <see cref="PeerConnectionMonitor"/> heartbeat (an empty payload), which every started fixture also sends to each of its hierarchical targets.</summary>
    private static bool IsRealPayload(ReadOnlyMemory<byte> payload) => payload.Length > 0;

    /// <summary>Builds an inbound (accepted) IP connection identified by <paramref name="userName"/>'s certificate subject.</summary>
    private static PeerConnection BuildInboundConnection(string userName, Action? drop = null)
        => new(null, true, $"CN=USER-{userName}", drop ?? (() => { }));

    /// <summary>Builds the connection of a serial link this node opened to <paramref name="endpoint"/>.</summary>
    private static PeerConnection BuildSerialConnection(UserEndpoint endpoint) => new(endpoint, false, null, () => { });

    private static PeerReceivedEventArgs ReceivedFrom(PeerConnection connection, ReadOnlyMemory<byte> payload)
        => new() { Connection = connection, Payload = payload };

    /// <summary>Configures <paramref name="transport"/> so every Request publishes Connected (for the endpoint it was sent to) via <paramref name="connectedObservable"/> and resolves successfully, simulating an on-demand outbound connection.</summary>
    private static void AutoConnectAndAcknowledge(Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connectedObservable, bool success = true)
        => transport.Setup(p => p.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .Returns<UserEndpoint, ReadOnlyMemory<byte>, PeerSendOptions?, CancellationToken>((target, _, _, _) =>
            {
                connectedObservable.Publish(new PeerConnectionEventArgs { Connection = new PeerConnection(target, false, null, () => { }) });
                return Task.FromResult(success);
            });

    private sealed record Fixture(
        ServerRoutingService Service,
        Mock<IPeerTransport> Transport,
        TestObservable<PeerConnectionEventArgs> Connected,
        TestObservable<PeerConnectionEventArgs> Disconnected,
        TestObservable<PeerReceivedEventArgs> Received,
        Task StartTask,
        CancellationTokenSource Cts);

    /// <summary>
    /// Builds a service for "ServerA" (this instance) with children ClientA1/ClientA2, alongside "ServerB"
    /// with children ClientB1/ClientB2, and starts it so its receiver is live. <paramref name="configureTransport"/>,
    /// if given, runs against the mocked transport before <c>Start</c> is called, so it can also observe the
    /// background connection monitor's own immediate heartbeat sends. <paramref name="userMap"/> and
    /// <paramref name="childEndpoints"/> override the default all-IP topology.
    /// </summary>
    private static async Task<Fixture> BuildStarted(
        Action<Mock<IPeerTransport>, TestObservable<PeerConnectionEventArgs>>? configureTransport = null,
        Dictionary<string, ServerUserConfig>? userMap = null,
        Dictionary<string, UserEndpoint>? childEndpoints = null)
    {
        userMap ??= new Dictionary<string, ServerUserConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerA"] = new ServerUserConfig { Endpoint = serverAEndpoint, ChildClients = ["ClientA1", "ClientA2"] },
            ["ServerB"] = new ServerUserConfig { Endpoint = serverBEndpoint, ChildClients = ["ClientB1", "ClientB2"] }
        };
        childEndpoints ??= new Dictionary<string, UserEndpoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClientA1"] = clientA1Endpoint,
            ["ClientA2"] = clientA2Endpoint
        };

        Mock<IPeerTransport> transport = new();
        TestObservable<PeerConnectionEventArgs> connected = new();
        TestObservable<PeerConnectionEventArgs> disconnected = new();
        TestObservable<PeerReceivedEventArgs> received = new();
        transport.SetupGet(p => p.Connected).Returns(connected);
        transport.SetupGet(p => p.Disconnected).Returns(disconnected);
        transport.SetupGet(p => p.Received).Returns(received);

        Mock<IPeerTransportFactory> transportFactory = new();
        transportFactory.Setup(f => f.Create()).Returns(transport.Object);

        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(p => p.Servers).Returns(userMap);
        engineController.Setup(p => p.GetCertificateName(It.IsAny<string>())).Returns((string name) => $"USER-{name}");
        engineController.Setup(p => p.GetEndpoint(It.IsAny<string>())).Returns((string name) => childEndpoints.GetValueOrDefault(name));

        Mock<ICurrentUserProvider> currentUser = new();
        currentUser.SetupGet(p => p.UserName).Returns("ServerA");

        ServerRoutingService service = new(transportFactory.Object, engineController.Object, currentUser.Object, noLogger);

        configureTransport?.Invoke(transport, connected);

        CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        return new Fixture(service, transport, connected, disconnected, received, startTask, cts);
    }

    private static ReadOnlyMemory<byte> Encode(TestMessage message)
    {
        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    private static TestMessage MessageTo(params string[] users) => new()
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

    private static bool RequestedRealPayloadTo(Fixture fx, int port)
        => fx.Transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Request) && ((UserEndpoint)i.Arguments[0]).Port == port && IsRealPayload((ReadOnlyMemory<byte>)i.Arguments[1]));

    /// <summary>An inbound connection whose certificate identifies a known child of this server is tracked as a child connection.</summary>
    [Fact]
    public async Task OnConnected_KnownChild_TrackedAsChild()
    {
        Fixture fx = await BuildStarted();
        int drops = 0;
        PeerConnection clientA1 = BuildInboundConnection("ClientA1", () => drops++);

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = clientA1 });

        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA1" && s.IsConnected);
        Assert.Equal(0, drops);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>An inbound connection whose certificate identifies an unrecognized identity is dropped and ignored.</summary>
    [Fact]
    public async Task OnConnected_UnrecognizedIdentity_Dropped()
    {
        Fixture fx = await BuildStarted();
        int drops = 0;
        PeerConnection stranger = BuildInboundConnection("UNKNOWN-USER", () => drops++);

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = stranger });

        Assert.Equal(1, drops);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>The server listens on its own IP endpoint's port.</summary>
    [Fact]
    public async Task Start_IpEndpoint_StartsListener()
    {
        Fixture fx = await BuildStarted();

        fx.Transport.Verify(t => t.StartListener(serverAEndpoint.Port), Times.Once);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A server whose own endpoint is serial has no IP address to listen on, so it starts no listener.</summary>
    [Fact]
    public async Task Start_SerialOwnEndpoint_DoesNotStartListener()
    {
        Dictionary<string, ServerUserConfig> userMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerA"] = new ServerUserConfig { Endpoint = new UserEndpoint { SerialPort = "SL0" }, ChildClients = ["ClientA1"] },
            ["ServerB"] = new ServerUserConfig { Endpoint = serverBEndpoint, ChildClients = ["ClientB1"] }
        };
        Fixture fx = await BuildStarted(userMap: userMap);

        fx.Transport.Verify(t => t.StartListener(It.IsAny<int>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message from one child addressed to a sibling child is routed directly to that sibling, not forwarded to any server.</summary>
    [Fact]
    public async Task FromChild_AddressedToSiblingChild_RoutesToSibling()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Transport, fx.Connected);

        PeerConnection clientA1 = BuildInboundConnection("ClientA1");
        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = clientA1 });

        fx.Received.Publish(ReceivedFrom(clientA1, Encode(MessageTo("ClientA2"))));

        await WaitUntil(() => RequestedRealPayloadTo(fx, clientA2Endpoint.Port), TimeSpan.FromSeconds(2));
        fx.Transport.Verify(p => p.Request(It.Is<UserEndpoint>(t => t.Port == clientA2Endpoint.Port), It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        fx.Transport.Verify(p => p.Request(It.Is<UserEndpoint>(t => t.Port == serverBEndpoint.Port), It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message from a child addressed to a child of another server is forwarded to that server once.</summary>
    [Fact]
    public async Task FromChild_AddressedToRemoteServersChild_ForwardsToThatServerOnce()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Transport, fx.Connected);

        PeerConnection clientA1 = BuildInboundConnection("ClientA1");
        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = clientA1 });

        // Addressed to both of ServerB's children, which should still forward to ServerB exactly once.
        fx.Received.Publish(ReceivedFrom(clientA1, Encode(MessageTo("ClientB1", "ClientB2"))));

        await WaitUntil(() => RequestedRealPayloadTo(fx, serverBEndpoint.Port), TimeSpan.FromSeconds(2));
        fx.Transport.Verify(p => p.Request(It.Is<UserEndpoint>(t => t.Port == serverBEndpoint.Port), It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A child with no configured endpoint (missing from the Users map) is silently skipped, not thrown for, when addressed.</summary>
    [Fact]
    public async Task FromChild_SiblingHasNoConfiguredEndpoint_DoesNotSendOrThrow()
    {
        Dictionary<string, ServerUserConfig> userMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerA"] = new ServerUserConfig { Endpoint = serverAEndpoint, ChildClients = ["ClientA1", "ClientA2"] }
        };
        Fixture fx = await BuildStarted(userMap: userMap, childEndpoints: []);

        PeerConnection clientA1 = BuildInboundConnection("ClientA1");
        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = clientA1 });

        fx.Received.Publish(ReceivedFrom(clientA1, Encode(MessageTo("ClientA2"))));

        await Task.Delay(50);
        fx.Transport.Verify(p => p.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message received from another server is delivered only to local children it addresses, never re-forwarded to other servers.</summary>
    [Fact]
    public async Task FromServer_AddressedToLocalChild_DeliversLocallyOnlyNeverReforwarded()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Transport, fx.Connected);

        PeerConnection serverB = BuildInboundConnection("ServerB");
        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = serverB });

        fx.Received.Publish(ReceivedFrom(serverB, Encode(MessageTo("ClientA1"))));

        await WaitUntil(() => RequestedRealPayloadTo(fx, clientA1Endpoint.Port), TimeSpan.FromSeconds(2));
        fx.Transport.Verify(p => p.Request(It.Is<UserEndpoint>(t => t.Port == clientA1Endpoint.Port), It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        // Never re-forwarded back out to ServerB.
        fx.Transport.Verify(p => p.Request(It.Is<UserEndpoint>(t => t.Port == serverBEndpoint.Port), It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>GetStatuses returns one row per own child client (ClientA1, ClientA2) plus one row for the other server (ServerB), excluding this instance's own name, all initially disconnected.</summary>
    [Fact]
    public async Task GetStatuses_Started_ReturnsChildAndServerRowsDisconnected()
    {
        Fixture fx = await BuildStarted();

        IReadOnlyList<PeerConnectionStatus> statuses = fx.Service.GetStatuses();

        Assert.Equal(3, statuses.Count);
        Assert.Contains(statuses, s => s.UserName == "ClientA1" && !s.IsConnected);
        Assert.Contains(statuses, s => s.UserName == "ClientA2" && !s.IsConnected);
        Assert.Contains(statuses, s => s.UserName == "ServerB" && !s.IsConnected);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>
    /// Even with no real message ever routed, the background connection monitor proactively sends an empty
    /// heartbeat request to every own child and every sibling server, and GetStatuses reports them connected
    /// once those heartbeats' Connected events arrive, so the status table doesn't stay perpetually
    /// disconnected while idle.
    /// </summary>
    [Fact]
    public async Task GetStatuses_HeartbeatConnectsChildrenAndServersProactively_ReportsConnectedWithoutAnyRealTraffic()
    {
        Fixture fx = await BuildStarted(configureTransport: (transport, connected) => AutoConnectAndAcknowledge(transport, connected));

        await WaitUntil(() => fx.Service.GetStatuses().All(s => s.IsConnected), TimeSpan.FromSeconds(2));

        foreach (UserEndpoint expected in new[] { clientA1Endpoint, clientA2Endpoint, serverBEndpoint })
        {
            fx.Transport.Verify(p => p.Request(
                It.Is<UserEndpoint>(t => t.Port == expected.Port),
                It.Is<ReadOnlyMemory<byte>>(payload => payload.Length == 0),
                It.IsAny<PeerSendOptions>(),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        IReadOnlyList<PeerConnectionStatus> statuses = fx.Service.GetStatuses();
        Assert.Equal(3, statuses.Count);
        Assert.All(statuses, s => Assert.True(s.IsConnected));
        Assert.All(statuses, s => Assert.NotNull(s.LastConnectedAt));

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>Once an outbound connection to another server is established (e.g. via routing a message to it), GetStatuses reports it connected with a LastConnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ServerConnectedOutbound_ReturnsConnectedRow()
    {
        Fixture fx = await BuildStarted();

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = new PeerConnection(serverBEndpoint, false, null, () => { }) });

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB");
        Assert.True(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.Null(status.LastDisconnectedAt);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>After a known child's connection disconnects, GetStatuses reports it disconnected with a LastDisconnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ChildDisconnects_ReturnsDisconnectedRowWithLastDisconnectedAt()
    {
        Fixture fx = await BuildStarted();
        PeerConnection clientA1 = BuildInboundConnection("ClientA1");

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = clientA1 });
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA1" && s.IsConnected);

        fx.Disconnected.Publish(new PeerConnectionEventArgs { Connection = clientA1 });

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA1");
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.NotNull(status.LastDisconnectedAt);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>Once a known child client connects, GetStatuses reports its row as connected with a LastConnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ChildConnects_ReturnsConnectedChildRow()
    {
        Fixture fx = await BuildStarted();

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = BuildInboundConnection("ClientA1") });

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA1");
        Assert.True(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.Null(status.LastDisconnectedAt);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>Once a recognized sibling server connects and then disconnects, GetStatuses reports its row as disconnected with a LastDisconnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ServerDisconnects_ReturnsDisconnectedRowWithLastDisconnectedAt()
    {
        Fixture fx = await BuildStarted();
        PeerConnection serverB = BuildInboundConnection("ServerB");

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = serverB });
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ServerB" && s.IsConnected);

        fx.Disconnected.Publish(new PeerConnectionEventArgs { Connection = serverB });

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB");
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.NotNull(status.LastDisconnectedAt);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>Malformed (non-empty, non-deserializable) bytes from a recognized child are dropped silently: no relay Request happens and nothing throws.</summary>
    [Fact]
    public async Task FromChild_MalformedPayload_IsDroppedWithoutSendOrThrow()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Transport, fx.Connected);

        PeerConnection clientA1 = BuildInboundConnection("ClientA1");
        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = clientA1 });

        fx.Received.Publish(ReceivedFrom(clientA1, new byte[] { 0xFF, 0xFE, 0xFD }));

        await Task.Delay(50);
        fx.Transport.Verify(p => p.Request(It.IsAny<UserEndpoint>(), It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>IPeerService.Send (this server instance originating its own message) is routed exactly like a message received from itself as a child.</summary>
    [Fact]
    public async Task Send_FromServerItself_RoutesLikeAChildMessage()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Transport, fx.Connected);

        TestMessage message = MessageTo("ClientA2");
        message.MessageId = "SELF-M1";

        bool ok = await fx.Service.Send("ClientA2", message);

        Assert.True(ok);
        await WaitUntil(() => RequestedRealPayloadTo(fx, clientA2Endpoint.Port), TimeSpan.FromSeconds(2));
        fx.Transport.Verify(p => p.Request(It.Is<UserEndpoint>(t => t.Port == clientA2Endpoint.Port), It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    private static Dictionary<string, ServerUserConfig> SerialTopology() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["ServerA"] = new ServerUserConfig { Endpoint = serverAEndpoint, ChildClients = ["ClientA1", "ClientA2"] },
        ["ServerB"] = new ServerUserConfig { Endpoint = serverBSerialEndpoint, ChildClients = ["ClientB1"] }
    };

    private static Dictionary<string, UserEndpoint> SerialChildren() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["ClientA1"] = clientA1SerialEndpoint,
        ["ClientA2"] = clientA2Endpoint
    };

    /// <summary>A child cabled over serial is recognized by the port it is configured on, with no certificate, and its link coming up and going down drives its status row.</summary>
    [Fact]
    public async Task SerialChild_ConnectedThenDisconnected_TracksStatusByEndpoint()
    {
        Fixture fx = await BuildStarted(userMap: SerialTopology(), childEndpoints: SerialChildren());
        PeerConnection link = BuildSerialConnection(new UserEndpoint { SerialPort = "sl1" });

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = link });
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA1" && s.IsConnected && s.Kind == PeerConnectionKind.Client);

        fx.Disconnected.Publish(new PeerConnectionEventArgs { Connection = link });
        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ClientA1");
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastDisconnectedAt);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A sibling server cabled over serial is tracked as a server row by its configured endpoint.</summary>
    [Fact]
    public async Task SerialServer_ConnectedThenDisconnected_TracksStatusByEndpoint()
    {
        Fixture fx = await BuildStarted(userMap: SerialTopology(), childEndpoints: SerialChildren());
        PeerConnection link = BuildSerialConnection(serverBSerialEndpoint);

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = link });
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ServerB" && s.IsConnected && s.Kind == PeerConnectionKind.Server);

        fx.Disconnected.Publish(new PeerConnectionEventArgs { Connection = link });
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ServerB" && !s.IsConnected);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message arriving on a child's serial link is attributed to that child by port and routed onward like any other child message.</summary>
    [Fact]
    public async Task SerialChild_MessageReceived_RoutedAsFromThatChild()
    {
        Fixture fx = await BuildStarted(userMap: SerialTopology(), childEndpoints: SerialChildren());
        AutoConnectAndAcknowledge(fx.Transport, fx.Connected);
        PeerConnection link = BuildSerialConnection(clientA1SerialEndpoint);

        fx.Received.Publish(ReceivedFrom(link, Encode(MessageTo("ClientA2", "ClientB1"))));

        await WaitUntil(() => RequestedRealPayloadTo(fx, clientA2Endpoint.Port), TimeSpan.FromSeconds(2));
        fx.Transport.Verify(p => p.Request(It.Is<UserEndpoint>(t => t.Port == clientA2Endpoint.Port), It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        fx.Transport.Verify(p => p.Request(serverBSerialEndpoint, It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message arriving on the serial link of a sibling server is treated as already routed: delivered to local children only, never re-forwarded.</summary>
    [Fact]
    public async Task SerialServer_MessageReceived_DeliversToLocalChildrenOnly()
    {
        Fixture fx = await BuildStarted(userMap: SerialTopology(), childEndpoints: SerialChildren());
        AutoConnectAndAcknowledge(fx.Transport, fx.Connected);
        PeerConnection link = BuildSerialConnection(serverBSerialEndpoint);

        fx.Received.Publish(ReceivedFrom(link, Encode(MessageTo("ClientA2", "ClientB1"))));

        await WaitUntil(() => RequestedRealPayloadTo(fx, clientA2Endpoint.Port), TimeSpan.FromSeconds(2));
        fx.Transport.Verify(p => p.Request(serverBSerialEndpoint, It.Is<ReadOnlyMemory<byte>>(p => IsRealPayload(p)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message arriving on a serial link no configured user is cabled to is ignored.</summary>
    [Fact]
    public async Task SerialUnknownLink_MessageReceived_Ignored()
    {
        Fixture fx = await BuildStarted(userMap: SerialTopology(), childEndpoints: SerialChildren());
        AutoConnectAndAcknowledge(fx.Transport, fx.Connected);

        fx.Received.Publish(ReceivedFrom(BuildSerialConnection(new UserEndpoint { SerialPort = "NOPE" }), Encode(MessageTo("ClientA2"))));

        await Task.Delay(50);
        Assert.False(RequestedRealPayloadTo(fx, clientA2Endpoint.Port));

        fx.Cts.Cancel();
        await fx.StartTask;
    }
}
