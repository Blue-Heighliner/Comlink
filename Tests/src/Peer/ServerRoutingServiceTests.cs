namespace BlueHeighliner.Comlink.Tests.Peer;

/// <summary>Unit tests for <see cref="ServerRoutingService"/> child/server connection classification and message routing.</summary>
public sealed class ServerRoutingServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private static readonly UserEndpoint serverAEndpoint = new() { IpAddress = "10.0.0.1", Port = 9001 };
    private static readonly UserEndpoint serverBEndpoint = new() { IpAddress = "10.0.0.2", Port = 9002 };
    private static readonly UserEndpoint clientA1Endpoint = new() { IpAddress = "10.0.1.1", Port = 9101 };
    private static readonly UserEndpoint clientA2Endpoint = new() { IpAddress = "10.0.1.2", Port = 9102 };

    /// <summary>Distinguishes a real routed message from a background <see cref="MsmtConnectionMonitor"/> heartbeat (an empty payload), which every started fixture also sends to each of its hierarchical targets.</summary>
    private static bool IsRealPayload(IMemoryOwner<byte> payload) => payload.Memory.Length > 0;

    /// <summary>Builds an inbound (accepted) connection mock identified by <paramref name="userName"/>'s certificate subject.</summary>
    private static Mock<IMsmtConnection> BuildInboundConnection(string userName, MsmtTarget? target = null)
    {
        Mock<IMsmtConnection> connection = new();
        connection.SetupGet(c => c.Identity).Returns(new MsmtIdentity { Subject = $"CN=USER-{userName}", Issuer = string.Empty, SerialNumber = string.Empty, Thumbprint = string.Empty });
        connection.SetupGet(c => c.Target).Returns(target ?? new MsmtTarget { Host = "0.0.0.0", Port = 0 });
        connection.SetupGet(c => c.Sender).Returns((IMsmtLink?)null);
        connection.SetupGet(c => c.Receiver).Returns(Mock.Of<IMsmtLink>());
        return connection;
    }

    private static MsmtReceivedEventArgs ReceivedFrom(IMsmtConnection connection, IMemoryOwner<byte> payload)
        => new() { Link = Mock.Of<IMsmtLink>(l => l.Connection == connection), Payload = payload, Responder = Mock.Of<IMsmtResponder>(), IsResponseRequested = false };

    /// <summary>Configures <paramref name="peer"/> so every Request publishes Connected (matching the sent-to target) via <paramref name="connectedObservable"/> and resolves successfully, simulating an on-demand outbound MSMT connection.</summary>
    private static void AutoConnectAndAcknowledge(Mock<IMsmtPeer> peer, TestObservable<MsmtConnectedEventArgs> connectedObservable, bool success = true)
        => peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .Returns<MsmtNameTarget, IMemoryOwner<byte>, MsmtSendOptions?, CancellationToken>((target, _, _, _) =>
            {
                Mock<IMsmtConnection> connection = new();
                connection.SetupGet(c => c.Target).Returns(new MsmtTarget { Host = target.Host, Port = target.Port });
                connection.SetupGet(c => c.Sender).Returns(Mock.Of<IMsmtLink>());
                connectedObservable.Publish(new MsmtConnectedEventArgs { Connection = connection.Object });
                return Task.FromResult(new MsmtResponse { Success = success, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });
            });

    private sealed record Fixture(
        ServerRoutingService Service,
        Mock<IMsmtPeer> Peer,
        TestObservable<MsmtConnectedEventArgs> Connected,
        TestObservable<MsmtDisconnectedEventArgs> Disconnected,
        TestObservable<MsmtReceivedEventArgs> Received,
        Task StartTask,
        CancellationTokenSource Cts);

    /// <summary>
    /// Builds a service for "ServerA" (this instance) with children ClientA1/ClientA2, alongside "ServerB"
    /// with children ClientB1/ClientB2, and starts it so its receiver is live. <paramref name="configurePeer"/>,
    /// if given, runs against the mocked peer before <c>Start</c> is called, so it can also observe the
    /// background connection monitor's own immediate heartbeat sends.
    /// </summary>
    private static async Task<Fixture> BuildStarted(Action<Mock<IMsmtPeer>, TestObservable<MsmtConnectedEventArgs>>? configurePeer = null)
    {
        Dictionary<string, ServerUserConfig> userMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerA"] = new ServerUserConfig { Endpoint = serverAEndpoint, ChildClients = ["ClientA1", "ClientA2"] },
            ["ServerB"] = new ServerUserConfig { Endpoint = serverBEndpoint, ChildClients = ["ClientB1", "ClientB2"] }
        };
        Dictionary<string, UserEndpoint> childEndpoints = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ClientA1"] = clientA1Endpoint,
            ["ClientA2"] = clientA2Endpoint
        };

        Mock<IMsmtPeer> peer = new();
        TestObservable<MsmtConnectedEventArgs> connected = new();
        TestObservable<MsmtDisconnectedEventArgs> disconnected = new();
        TestObservable<MsmtReceivedEventArgs> received = new();
        peer.SetupGet(p => p.Connected).Returns(connected);
        peer.SetupGet(p => p.Disconnected).Returns(disconnected);
        peer.SetupGet(p => p.Received).Returns(received);

        Mock<IMsmtPeerFactory> peerFactory = new();
        peerFactory.Setup(f => f.Create(It.IsAny<MsmtOptions>())).Returns(peer.Object);

        (X509Certificate2 identity, _, X509Certificate2Collection trustedAuthorities) = TestMsmtCertificates.Create();

        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(p => p.Servers).Returns(userMap);
        engineController.Setup(p => p.ConnectionOptions).Returns(new MsmtOptions
        {
            Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = trustedAuthorities }
        });
        engineController.Setup(p => p.GetCertificateName(It.IsAny<string>())).Returns((string name) => $"USER-{name}");
        engineController.Setup(p => p.GetEndpoint(It.IsAny<string>())).Returns((string name) => childEndpoints.GetValueOrDefault(name));

        Mock<ICurrentUserProvider> currentUser = new();
        currentUser.SetupGet(p => p.UserName).Returns("ServerA");

        ServerRoutingService service = new(peerFactory.Object, engineController.Object, currentUser.Object, noLogger);

        configurePeer?.Invoke(peer, connected);

        CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        return new Fixture(service, peer, connected, disconnected, received, startTask, cts);
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

    /// <summary>An inbound connection whose certificate identifies a known child of this server is tracked as a child connection.</summary>
    [Fact]
    public async Task OnConnected_KnownChild_TrackedAsChild()
    {
        Fixture fx = await BuildStarted();
        Mock<IMsmtConnection> clientA1 = BuildInboundConnection("ClientA1");

        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = clientA1.Object });

        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA1" && s.IsConnected);
        clientA1.Verify(c => c.Drop(), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>An inbound connection whose certificate identifies an unrecognized identity is dropped and ignored.</summary>
    [Fact]
    public async Task OnConnected_UnrecognizedIdentity_Dropped()
    {
        Fixture fx = await BuildStarted();
        MsmtTarget strangerTarget = new() { Host = "10.0.9.9", Port = 12345 };
        Mock<IMsmtConnection> stranger = BuildInboundConnection("UNKNOWN-USER", strangerTarget);

        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = stranger.Object });

        stranger.Verify(c => c.Drop(), Times.Once);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message from one child addressed to a sibling child is routed directly to that sibling, not forwarded to any server.</summary>
    [Fact]
    public async Task FromChild_AddressedToSiblingChild_RoutesToSibling()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Peer, fx.Connected);

        Mock<IMsmtConnection> clientA1 = BuildInboundConnection("ClientA1");
        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = clientA1.Object });

        fx.Received.Publish(ReceivedFrom(clientA1.Object, new UnownedMemory(Encode(MessageTo("ClientA2")))));

        await WaitUntil(() => fx.Peer.Invocations.Any(i => i.Method.Name == nameof(IMsmtPeer.Request) && ((MsmtNameTarget)i.Arguments[0]).Port == clientA2Endpoint.Port && IsRealPayload((IMemoryOwner<byte>)i.Arguments[1])), TimeSpan.FromSeconds(2));
        fx.Peer.Verify(p => p.Request(It.Is<MsmtNameTarget>(t => t.Port == clientA2Endpoint.Port), It.Is<IMemoryOwner<byte>>(p => IsRealPayload(p)), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        fx.Peer.Verify(p => p.Request(It.Is<MsmtNameTarget>(t => t.Port == serverBEndpoint.Port), It.Is<IMemoryOwner<byte>>(p => IsRealPayload(p)), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message from a child addressed to a child of another server is forwarded to that server once.</summary>
    [Fact]
    public async Task FromChild_AddressedToRemoteServersChild_ForwardsToThatServerOnce()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Peer, fx.Connected);

        Mock<IMsmtConnection> clientA1 = BuildInboundConnection("ClientA1");
        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = clientA1.Object });

        // Addressed to both of ServerB's children — should still forward to ServerB exactly once.
        fx.Received.Publish(ReceivedFrom(clientA1.Object, new UnownedMemory(Encode(MessageTo("ClientB1", "ClientB2")))));

        await WaitUntil(() => fx.Peer.Invocations.Any(i => i.Method.Name == nameof(IMsmtPeer.Request) && ((MsmtNameTarget)i.Arguments[0]).Port == serverBEndpoint.Port && IsRealPayload((IMemoryOwner<byte>)i.Arguments[1])), TimeSpan.FromSeconds(2));
        fx.Peer.Verify(p => p.Request(It.Is<MsmtNameTarget>(t => t.Port == serverBEndpoint.Port), It.Is<IMemoryOwner<byte>>(p => IsRealPayload(p)), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);

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

        Mock<IMsmtPeer> peer = new();
        TestObservable<MsmtConnectedEventArgs> connected = new();
        TestObservable<MsmtDisconnectedEventArgs> disconnected = new();
        TestObservable<MsmtReceivedEventArgs> received = new();
        peer.SetupGet(p => p.Connected).Returns(connected);
        peer.SetupGet(p => p.Disconnected).Returns(disconnected);
        peer.SetupGet(p => p.Received).Returns(received);

        Mock<IMsmtPeerFactory> peerFactory = new();
        peerFactory.Setup(f => f.Create(It.IsAny<MsmtOptions>())).Returns(peer.Object);

        (X509Certificate2 identity, _, X509Certificate2Collection trustedAuthorities) = TestMsmtCertificates.Create();

        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(p => p.Servers).Returns(userMap);
        engineController.Setup(p => p.ConnectionOptions).Returns(new MsmtOptions
        {
            Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = trustedAuthorities }
        });
        engineController.Setup(p => p.GetCertificateName(It.IsAny<string>())).Returns((string name) => $"USER-{name}");
        engineController.Setup(p => p.GetEndpoint(It.IsAny<string>())).Returns((UserEndpoint?)null);

        Mock<ICurrentUserProvider> currentUser = new();
        currentUser.SetupGet(p => p.UserName).Returns("ServerA");

        ServerRoutingService service = new(peerFactory.Object, engineController.Object, currentUser.Object, noLogger);
        CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        Mock<IMsmtConnection> clientA1 = BuildInboundConnection("ClientA1");
        connected.Publish(new MsmtConnectedEventArgs { Connection = clientA1.Object });

        received.Publish(ReceivedFrom(clientA1.Object, new UnownedMemory(Encode(MessageTo("ClientA2")))));

        await Task.Delay(50);
        peer.Verify(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A message received from another server is delivered only to local children it addresses, never re-forwarded to other servers.</summary>
    [Fact]
    public async Task FromServer_AddressedToLocalChild_DeliversLocallyOnlyNeverReforwarded()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Peer, fx.Connected);

        Mock<IMsmtConnection> serverB = BuildInboundConnection("ServerB", new MsmtTarget { Host = serverBEndpoint.IpAddress, Port = serverBEndpoint.Port });
        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = serverB.Object });

        fx.Received.Publish(ReceivedFrom(serverB.Object, new UnownedMemory(Encode(MessageTo("ClientA1")))));

        await WaitUntil(() => fx.Peer.Invocations.Any(i => i.Method.Name == nameof(IMsmtPeer.Request) && ((MsmtNameTarget)i.Arguments[0]).Port == clientA1Endpoint.Port && IsRealPayload((IMemoryOwner<byte>)i.Arguments[1])), TimeSpan.FromSeconds(2));
        fx.Peer.Verify(p => p.Request(It.Is<MsmtNameTarget>(t => t.Port == clientA1Endpoint.Port), It.Is<IMemoryOwner<byte>>(p => IsRealPayload(p)), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        // Never re-forwarded back out to ServerB.
        fx.Peer.Verify(p => p.Request(It.Is<MsmtNameTarget>(t => t.Port == serverBEndpoint.Port), It.Is<IMemoryOwner<byte>>(p => IsRealPayload(p)), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

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
    /// heartbeat request to every own child and every sibling server - reusing the same on-demand connection
    /// a real message would use - and GetStatuses reports them connected once those heartbeats' Connected
    /// events arrive, so the status table doesn't stay perpetually disconnected while idle.
    /// </summary>
    [Fact]
    public async Task GetStatuses_HeartbeatConnectsChildrenAndServersProactively_ReportsConnectedWithoutAnyRealTraffic()
    {
        Fixture fx = await BuildStarted(configurePeer: (peer, connected) => AutoConnectAndAcknowledge(peer, connected));

        await WaitUntil(() => fx.Service.GetStatuses().All(s => s.IsConnected), TimeSpan.FromSeconds(2));

        fx.Peer.Verify(p => p.Request(
            It.Is<MsmtNameTarget>(t => t.Port == clientA1Endpoint.Port),
            It.Is<IMemoryOwner<byte>>(payload => payload.Memory.Length == 0),
            It.IsAny<MsmtSendOptions>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        fx.Peer.Verify(p => p.Request(
            It.Is<MsmtNameTarget>(t => t.Port == clientA2Endpoint.Port),
            It.Is<IMemoryOwner<byte>>(payload => payload.Memory.Length == 0),
            It.IsAny<MsmtSendOptions>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        fx.Peer.Verify(p => p.Request(
            It.Is<MsmtNameTarget>(t => t.Port == serverBEndpoint.Port),
            It.Is<IMemoryOwner<byte>>(payload => payload.Memory.Length == 0),
            It.IsAny<MsmtSendOptions>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

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

        Mock<IMsmtConnection> serverB = new();
        serverB.SetupGet(c => c.Target).Returns(new MsmtTarget { Host = serverBEndpoint.IpAddress, Port = serverBEndpoint.Port });
        serverB.SetupGet(c => c.Sender).Returns(Mock.Of<IMsmtLink>());
        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = serverB.Object });

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
        Mock<IMsmtConnection> clientA1 = BuildInboundConnection("ClientA1");

        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = clientA1.Object });
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ClientA1" && s.IsConnected);

        fx.Disconnected.Publish(new MsmtDisconnectedEventArgs { Connection = clientA1.Object });

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
        Mock<IMsmtConnection> clientA1 = BuildInboundConnection("ClientA1");

        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = clientA1.Object });

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
        Mock<IMsmtConnection> serverB = BuildInboundConnection("ServerB", new MsmtTarget { Host = serverBEndpoint.IpAddress, Port = serverBEndpoint.Port });

        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = serverB.Object });
        Assert.Contains(fx.Service.GetStatuses(), s => s.UserName == "ServerB" && s.IsConnected);

        fx.Disconnected.Publish(new MsmtDisconnectedEventArgs { Connection = serverB.Object });

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses(), s => s.UserName == "ServerB");
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.NotNull(status.LastDisconnectedAt);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>Malformed (non-empty, non-deserializable) bytes from a recognized child are dropped silently - no relay Request happens and nothing throws.</summary>
    [Fact]
    public async Task FromChild_MalformedPayload_IsDroppedWithoutSendOrThrow()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Peer, fx.Connected);

        Mock<IMsmtConnection> clientA1 = BuildInboundConnection("ClientA1");
        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = clientA1.Object });

        fx.Received.Publish(ReceivedFrom(clientA1.Object, new UnownedMemory(new byte[] { 0xFF, 0xFE, 0xFD })));

        await Task.Delay(50);
        fx.Peer.Verify(p => p.Request(It.IsAny<MsmtNameTarget>(), It.Is<IMemoryOwner<byte>>(p => IsRealPayload(p)), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>IPeerService.Send (this server instance originating its own message) is routed exactly like a message received from itself as a child.</summary>
    [Fact]
    public async Task Send_FromServerItself_RoutesLikeAChildMessage()
    {
        Fixture fx = await BuildStarted();
        AutoConnectAndAcknowledge(fx.Peer, fx.Connected);

        TestMessage message = MessageTo("ClientA2");
        message.MessageId = "SELF-M1";

        bool ok = await fx.Service.Send("ClientA2", message);

        Assert.True(ok);
        await WaitUntil(() => fx.Peer.Invocations.Any(i => i.Method.Name == nameof(IMsmtPeer.Request) && ((MsmtNameTarget)i.Arguments[0]).Port == clientA2Endpoint.Port && IsRealPayload((IMemoryOwner<byte>)i.Arguments[1])), TimeSpan.FromSeconds(2));
        fx.Peer.Verify(p => p.Request(It.Is<MsmtNameTarget>(t => t.Port == clientA2Endpoint.Port), It.Is<IMemoryOwner<byte>>(p => IsRealPayload(p)), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    private sealed class UnownedMemory(ReadOnlyMemory<byte> data) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = data.ToArray();
        public void Dispose() { }
    }
}
