namespace BlueHeighliner.Comlink.Tests.Unit.Peer;

/// <summary>Unit tests for <see cref="ClientPeerService"/> connection status tracking, send coalescing, and message dispatch.</summary>
public sealed class ClientPeerServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private static readonly UserEndpoint serverEndpoint = new() { IpAddress = "10.0.0.1", Port = 9000 };

    private static (ClientPeerService Service, Mock<IPeerTransport> Transport, TestObservable<PeerConnectionEventArgs> Connected, TestObservable<PeerConnectionEventArgs> Disconnected, TestObservable<PeerReceivedEventArgs> Received) Build(bool endpointConfigured = true, UserEndpoint? endpoint = null)
    {
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
        engineController.Setup(p => p.ServerEndpoint).Returns(endpointConfigured ? endpoint ?? serverEndpoint : null);

        ClientPeerService service = new(transportFactory.Object, engineController.Object, noLogger);
        return (service, transport, connected, disconnected, received);
    }

    /// <summary>Configures <paramref name="transport"/> so every <see cref="IPeerTransport.Request"/> call immediately returns a successful acknowledgement.</summary>
    private static void AutoAcknowledge(Mock<IPeerTransport> transport, bool success = true)
        => transport.Setup(p => p.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);

    private static PeerConnection ConnectionTo(UserEndpoint endpoint) => new(endpoint, false, null, () => { });

    private static ReadOnlyMemory<byte> Encode(TestMessage message)
    {
        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    /// <summary>Start with no server endpoint configured logs an error and never creates a transport.</summary>
    [Fact]
    public async Task Start_NoEndpointConfigured_DoesNotStartListener()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, _) = Build(endpointConfigured: false);

        await service.Start(CancellationToken.None);

        transport.Verify(p => p.StartListener(It.IsAny<int>()), Times.Never);
    }

    /// <summary>Start creates a transport for the configured IP server and starts its listener so the server can deliver messages back.</summary>
    [Fact]
    public async Task Start_ConfiguredIpServer_StartsListener()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, _) = Build();
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        transport.Verify(p => p.StartListener(It.IsAny<int>()), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A serial link is one bidirectional cable, so a client whose server is reached over serial starts no IP listener.</summary>
    [Fact]
    public async Task Start_ConfiguredSerialServer_DoesNotStartListener()
    {
        UserEndpoint serial = new() { SerialPort = "SL0" };
        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, _) = Build(endpoint: serial);
        AutoAcknowledge(transport);
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        transport.Verify(p => p.StartListener(It.IsAny<int>()), Times.Never);
        transport.Verify(p => p.Request(serial, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Send fails immediately before Start has ever run, since no transport has been created yet.</summary>
    [Fact]
    public async Task Send_BeforeStart_ReturnsFalse()
    {
        (ClientPeerService service, _, _, _, _) = Build();

        bool ok = await service.Send("ANY-USER", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.False(ok);
    }

    /// <summary>Send transmits to the configured server endpoint and returns true once acknowledged.</summary>
    [Fact]
    public async Task Send_AfterStart_TransmitsToServerAndReturnsTrue()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, _) = Build();
        AutoAcknowledge(transport);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        bool ok = await service.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.True(ok);
        transport.Verify(p => p.Request(
            It.Is<UserEndpoint>(t => t.IpAddress == serverEndpoint.IpAddress && t.Port == serverEndpoint.Port),
            It.Is<ReadOnlyMemory<byte>>(payload => payload.Length > 0),
            It.IsAny<PeerSendOptions>(),
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
        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, _) = Build();
        AutoAcknowledge(transport);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        TestMessage message = new() { MessageId = "M1", FromUser = "SOURCE" };
        bool[] results = await Task.WhenAll(
            service.Send("USER-A", message),
            service.Send("USER-B", message),
            service.Send("USER-C", message));

        Assert.All(results, Assert.True);
        transport.Verify(p => p.Request(It.IsAny<UserEndpoint>(), It.Is<ReadOnlyMemory<byte>>(payload => payload.Length > 0), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A valid message received over the transport fires MessageDelivered.</summary>
    [Fact]
    public async Task Received_ValidMessage_RaisesMessageDelivered()
    {
        (ClientPeerService service, _, _, _, TestObservable<PeerReceivedEventArgs> received) = Build();
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        TaskCompletionSource<object> tcs = new();
        service.MessageDelivered += message => { tcs.TrySetResult(message); return Task.CompletedTask; };

        received.Publish(new PeerReceivedEventArgs
        {
            Connection = new PeerConnection(null, true, null, () => { }),
            Payload = Encode(new TestMessage { MessageId = "MSG1", FromUser = "REMOTE" })
        });

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

    /// <summary>Once the transport reports a Connected event for the configured server endpoint, GetStatuses reports the connected row with a LastConnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ServerConnected_ReturnsConnectedRow()
    {
        (ClientPeerService service, _, TestObservable<PeerConnectionEventArgs> connected, _, _) = Build();
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        connected.Publish(new PeerConnectionEventArgs { Connection = ConnectionTo(serverEndpoint) });

        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.True(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.Null(status.LastDisconnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A serial server link coming up and going down is tracked exactly like an IP one.</summary>
    [Fact]
    public async Task GetStatuses_SerialServerConnectedThenDisconnected_TracksBoth()
    {
        UserEndpoint serial = new() { SerialPort = "SL0" };
        (ClientPeerService service, Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, TestObservable<PeerConnectionEventArgs> disconnected, _) = Build(endpoint: serial);
        AutoAcknowledge(transport);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        PeerConnection connection = ConnectionTo(new UserEndpoint { SerialPort = "sl0" });
        connected.Publish(new PeerConnectionEventArgs { Connection = connection });
        Assert.True(Assert.Single(service.GetStatuses()).IsConnected);

        disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });
        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastDisconnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A connection to some other endpoint does not affect the server row.</summary>
    [Fact]
    public async Task GetStatuses_OtherEndpointConnected_ServerRowUnchanged()
    {
        (ClientPeerService service, _, TestObservable<PeerConnectionEventArgs> connected, _, _) = Build();
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        connected.Publish(new PeerConnectionEventArgs { Connection = ConnectionTo(new UserEndpoint { IpAddress = "10.9.9.9", Port = 1 }) });
        connected.Publish(new PeerConnectionEventArgs { Connection = new PeerConnection(null, true, null, () => { }) });

        Assert.False(Assert.Single(service.GetStatuses()).IsConnected);

        cts.Cancel();
        await startTask;
    }

    /// <summary>After a Disconnected event for the server endpoint, GetStatuses reports the row as disconnected with a LastDisconnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ServerDisconnected_ReturnsDisconnectedRowWithLastDisconnectedAt()
    {
        (ClientPeerService service, _, TestObservable<PeerConnectionEventArgs> connected, TestObservable<PeerConnectionEventArgs> disconnected, _) = Build();
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        PeerConnection connection = ConnectionTo(serverEndpoint);
        connected.Publish(new PeerConnectionEventArgs { Connection = connection });
        disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });

        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.NotNull(status.LastDisconnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>
    /// Even with no real message ever sent by the caller, the background connection monitor proactively
    /// sends an empty heartbeat request to the server, and GetStatuses reports it connected once
    /// that heartbeat's Connected event arrives, so the status table doesn't stay perpetually disconnected
    /// while idle.
    /// </summary>
    [Fact]
    public async Task GetStatuses_HeartbeatConnectsProactively_ReportsConnectedWithoutAnyRealSend()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, _, _) = Build();
        bool connectedRaised = false;
        transport.Setup(p => p.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .Returns<UserEndpoint, ReadOnlyMemory<byte>, PeerSendOptions?, CancellationToken>((target, _, _, _) =>
            {
                if (!connectedRaised)
                {
                    connectedRaised = true;
                    connected.Publish(new PeerConnectionEventArgs { Connection = ConnectionTo(target) });
                }
                return Task.FromResult(true);
            });

        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await WaitUntil(() => service.GetStatuses().Single().IsConnected, TimeSpan.FromSeconds(2));

        transport.Verify(p => p.Request(
            It.Is<UserEndpoint>(t => t.IpAddress == serverEndpoint.IpAddress && t.Port == serverEndpoint.Port),
            It.Is<ReadOnlyMemory<byte>>(payload => payload.Length == 0),
            It.IsAny<PeerSendOptions>(),
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

    /// <summary>StatusesChanged fires when the server endpoint connects.</summary>
    [Fact]
    public async Task StatusesChanged_OnServerConnect_Fires()
    {
        (ClientPeerService service, _, TestObservable<PeerConnectionEventArgs> connected, _, _) = Build();
        using CancellationTokenSource cts = new();

        TaskCompletionSource raised = new();
        service.StatusesChanged += () => raised.TrySetResult();

        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        connected.Publish(new PeerConnectionEventArgs { Connection = ConnectionTo(serverEndpoint) });

        await raised.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }
}
