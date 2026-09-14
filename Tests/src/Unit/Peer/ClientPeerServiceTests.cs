namespace BlueHeighliner.Comlink.Tests.Unit.Peer;

/// <summary>Unit tests for <see cref="ClientPeerService"/> connection status tracking, send coalescing, and message dispatch.</summary>
public sealed class ClientPeerServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private static readonly UserEndpoint serverEndpoint = new() { IpAddress = "10.0.0.1", Port = 9000 };

    private static (ClientPeerService Service, Mock<IMsmtPeer> Peer, TestObservable<MsmtConnectedEventArgs> Connected, TestObservable<MsmtDisconnectedEventArgs> Disconnected, TestObservable<MsmtReceivedEventArgs> Received) Build(bool endpointConfigured = true)
    {
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
        engineController.Setup(p => p.ServerEndpoint).Returns(endpointConfigured ? serverEndpoint : null);
        engineController.Setup(p => p.ConnectionOptions).Returns(new MsmtOptions
        {
            Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = trustedAuthorities }
        });

        ClientPeerService service = new(peerFactory.Object, engineController.Object, noLogger);
        return (service, peer, connected, disconnected, received);
    }

    /// <summary>Configures <paramref name="peer"/> so every <see cref="IMsmtPeer.Request"/> call immediately returns a successful acknowledgement.</summary>
    private static void AutoAcknowledge(Mock<IMsmtPeer> peer, bool success = true)
        => peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MsmtResponse { Success = success, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });

    private static ReadOnlyMemory<byte> Encode(TestMessage message)
    {
        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    /// <summary>Start with no server endpoint configured logs an error and never creates a peer.</summary>
    [Fact]
    public async Task Start_NoEndpointConfigured_DoesNotCreatePeer()
    {
        (ClientPeerService service, Mock<IMsmtPeer> peer, _, _, _) = Build(endpointConfigured: false);

        await service.Start(CancellationToken.None);

        peer.Verify(p => p.StartListener(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>Start creates a peer for the configured server and starts its listener so the server can deliver messages back.</summary>
    [Fact]
    public async Task Start_ConfiguredServer_StartsListener()
    {
        (ClientPeerService service, Mock<IMsmtPeer> peer, _, _, _) = Build();
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        peer.Verify(p => p.StartListener(It.IsAny<int>(), It.IsAny<string>()), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Send fails immediately before Start has ever run, since no peer has been created yet.</summary>
    [Fact]
    public async Task Send_BeforeStart_ReturnsFalse()
    {
        (ClientPeerService service, _, _, _, _) = Build();

        bool ok = await service.Send("ANY-USER", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.False(ok);
    }

    /// <summary>Send transmits to the configured server target and returns true once acknowledged.</summary>
    [Fact]
    public async Task Send_AfterStart_TransmitsToServerAndReturnsTrue()
    {
        (ClientPeerService service, Mock<IMsmtPeer> peer, _, _, _) = Build();
        AutoAcknowledge(peer);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        bool ok = await service.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.True(ok);
        peer.Verify(p => p.Request(
            It.Is<MsmtNameTarget>(t => t.Host == serverEndpoint.IpAddress && t.Port == serverEndpoint.Port),
            It.Is<IMemoryOwner<byte>>(payload => payload.Memory.Length > 0),
            It.IsAny<MsmtSendOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>
    /// Multiple Send calls for the same message ID (e.g. one per group member expanded by
    /// MessageRoutingService) are coalesced into a single physical transmission.
    /// </summary>
    [Fact]
    public async Task Send_SameMessageIdCalledConcurrently_TransmitsOnlyOnce()
    {
        (ClientPeerService service, Mock<IMsmtPeer> peer, _, _, _) = Build();
        AutoAcknowledge(peer);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        TestMessage message = new() { MessageId = "M1", FromUser = "SOURCE" };
        bool[] results = await Task.WhenAll(
            service.Send("USER-A", message),
            service.Send("USER-B", message),
            service.Send("USER-C", message));

        Assert.All(results, Assert.True);
        peer.Verify(p => p.Request(It.IsAny<MsmtNameTarget>(), It.Is<IMemoryOwner<byte>>(payload => payload.Memory.Length > 0), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A valid message received over the peer fires MessageDelivered.</summary>
    [Fact]
    public async Task Received_ValidMessage_RaisesMessageDelivered()
    {
        (ClientPeerService service, _, _, _, TestObservable<MsmtReceivedEventArgs> received) = Build();
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        TaskCompletionSource<object> tcs = new();
        service.MessageDelivered += message => { tcs.TrySetResult(message); return Task.CompletedTask; };

        MsmtReceivedEventArgs args = new()
        {
            Link = Mock.Of<IMsmtLink>(),
            Payload = new UnownedMemory(Encode(new TestMessage { MessageId = "MSG1", FromUser = "REMOTE" })),
            Responder = Mock.Of<IMsmtResponder>(),
            IsResponseRequested = false
        };
        received.Publish(args);

        object receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        TestMessage message = Assert.IsType<TestMessage>(receivedMessage);
        Assert.Equal("MSG1", message.MessageId);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Before Start ever runs, GetStatuses reports a single disconnected row.</summary>
    [Fact]
    public void GetStatuses_BeforeConnecting_ReturnsDisconnectedRow()
    {
        (ClientPeerService service, _, _, _, _) = Build();

        PeerConnectionStatus status = Assert.Single(service.GetStatuses());

        Assert.False(status.IsConnected);
        Assert.Null(status.LastConnectedAt);
        Assert.Null(status.LastDisconnectedAt);
    }

    /// <summary>Once the peer reports a Connected event for the configured server target, GetStatuses reports the connected row with a LastConnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ServerConnected_ReturnsConnectedRow()
    {
        (ClientPeerService service, _, TestObservable<MsmtConnectedEventArgs> connected, _, _) = Build();
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        connected.Publish(new MsmtConnectedEventArgs
        {
            Connection = Mock.Of<IMsmtConnection>(c => c.Target == new MsmtTarget { Host = serverEndpoint.IpAddress, Port = serverEndpoint.Port } && c.Sender == Mock.Of<IMsmtLink>())
        });

        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.True(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.Null(status.LastDisconnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>After a Disconnected event for the server target, GetStatuses reports the row as disconnected with a LastDisconnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ServerDisconnected_ReturnsDisconnectedRowWithLastDisconnectedAt()
    {
        (ClientPeerService service, _, TestObservable<MsmtConnectedEventArgs> connected, TestObservable<MsmtDisconnectedEventArgs> disconnected, _) = Build();
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        Mock<IMsmtConnection> connection = new();
        connection.SetupGet(c => c.Target).Returns(new MsmtTarget { Host = serverEndpoint.IpAddress, Port = serverEndpoint.Port });
        connection.SetupGet(c => c.Sender).Returns(Mock.Of<IMsmtLink>());

        connected.Publish(new MsmtConnectedEventArgs { Connection = connection.Object });
        disconnected.Publish(new MsmtDisconnectedEventArgs { Connection = connection.Object });

        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.NotNull(status.LastDisconnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>
    /// Even with no real message ever sent by the caller, the background connection monitor proactively
    /// sends an empty heartbeat request to the server - reusing the same on-demand connection <see
    /// cref="IMsmtPeer.Request"/> would create for a real message - and GetStatuses reports it connected once
    /// that heartbeat's Connected event arrives, so the status table doesn't stay perpetually disconnected
    /// while idle.
    /// </summary>
    [Fact]
    public async Task GetStatuses_HeartbeatConnectsProactively_ReportsConnectedWithoutAnyRealSend()
    {
        (ClientPeerService service, Mock<IMsmtPeer> peer, TestObservable<MsmtConnectedEventArgs> connected, _, _) = Build();
        bool connectedRaised = false;
        peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .Returns<MsmtNameTarget, IMemoryOwner<byte>, MsmtSendOptions?, CancellationToken>((target, _, _, _) =>
            {
                if (!connectedRaised)
                {
                    connectedRaised = true;
                    connected.Publish(new MsmtConnectedEventArgs
                    {
                        Connection = Mock.Of<IMsmtConnection>(c => c.Target == new MsmtTarget { Host = target.Host, Port = target.Port } && c.Sender == Mock.Of<IMsmtLink>())
                    });
                }
                return Task.FromResult(new MsmtResponse { Success = true, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });
            });

        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await WaitUntil(() => service.GetStatuses().Single().IsConnected, TimeSpan.FromSeconds(2));

        peer.Verify(p => p.Request(
            It.Is<MsmtNameTarget>(t => t.Host == serverEndpoint.IpAddress && t.Port == serverEndpoint.Port),
            It.Is<IMemoryOwner<byte>>(payload => payload.Memory.Length == 0),
            It.IsAny<MsmtSendOptions>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.True(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);

        cts.Cancel();
        await startTask;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(10);
        }
    }

    /// <summary>StatusesChanged fires when the server target connects.</summary>
    [Fact]
    public async Task StatusesChanged_OnServerConnect_Fires()
    {
        (ClientPeerService service, _, TestObservable<MsmtConnectedEventArgs> connected, _, _) = Build();
        using CancellationTokenSource cts = new();

        TaskCompletionSource raised = new();
        service.StatusesChanged += () => raised.TrySetResult();

        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        connected.Publish(new MsmtConnectedEventArgs
        {
            Connection = Mock.Of<IMsmtConnection>(c => c.Target == new MsmtTarget { Host = serverEndpoint.IpAddress, Port = serverEndpoint.Port } && c.Sender == Mock.Of<IMsmtLink>())
        });

        await raised.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }

    private sealed class UnownedMemory(ReadOnlyMemory<byte> data) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = data.ToArray();
        public void Dispose() { }
    }
}
