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

    /// <summary>A bare IP connection to the server does not count as up: a server that has closed this client accepts the connection and drops it again without ever answering, which would otherwise flash the row green.</summary>
    [Fact]
    public async Task GetStatuses_ConnectionWithoutAcknowledgedHeartbeat_StaysDown()
    {
        (ClientPeerService service, _, TestObservable<PeerConnectionEventArgs> connected, _, _) = Build();
        int raised = 0;
        service.StatusesChanged += () => raised++;
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(50);

        connected.Publish(new PeerConnectionEventArgs { Connection = ConnectionTo(serverEndpoint) });

        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.False(status.IsConnected);
        Assert.Null(status.LastConnectedAt);
        Assert.Equal(0, raised);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Once a heartbeat to the server is acknowledged, GetStatuses reports the connected row with a LastConnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_HeartbeatAcknowledged_ReturnsConnectedRow()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, _) = Build();
        AutoAcknowledge(transport);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);

        await WaitUntil(() => service.GetStatuses().Single().IsConnected, TimeSpan.FromSeconds(2));

        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.NotNull(status.LastConnectedAt);
        Assert.Null(status.LastDisconnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>The row carries the name from the server's certificate, and keeps it after the connection drops.</summary>
    [Fact]
    public async Task GetStatuses_ServerCertificate_NamesTheRow()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, TestObservable<PeerConnectionEventArgs> disconnected, _) = Build();
        AutoAcknowledge(transport);
        using CancellationTokenSource cts = new();
        Assert.Equal(string.Empty, Assert.Single(service.GetStatuses()).UserName);
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);
        PeerConnection connection = new(serverEndpoint, false, "CN=Server1, O=Comlink", () => { });

        connected.Publish(new PeerConnectionEventArgs { Connection = connection });
        await WaitUntil(() => service.GetStatuses().Single().IsConnected, TimeSpan.FromSeconds(2));
        Assert.Equal("Server1", Assert.Single(service.GetStatuses()).UserName);

        disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });
        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.False(status.IsConnected);
        Assert.Equal("Server1", status.UserName);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A serial server has no certificate, so its row is named after the port.</summary>
    [Fact]
    public async Task GetStatuses_SerialServer_NamedAfterPort()
    {
        UserEndpoint serial = new() { SerialPort = "SL0" };
        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, _) = Build(endpoint: serial);
        AutoAcknowledge(transport);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        Assert.Equal("SL0", Assert.Single(service.GetStatuses()).UserName);

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
        (ClientPeerService service, Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, TestObservable<PeerConnectionEventArgs> disconnected, _) = Build();
        AutoAcknowledge(transport);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await WaitUntil(() => service.GetStatuses().Single().IsConnected, TimeSpan.FromSeconds(2));

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

    private static (ClientPeerService Service, Mock<IPeerTransport> Transport, TestObservable<PeerConnectionEventArgs> Connected, TestObservable<PeerConnectionEventArgs> Disconnected, CancellationTokenSource Cts, Task StartTask) BuildRunning()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, TestObservable<PeerConnectionEventArgs> disconnected, _) = Build();
        AutoAcknowledge(transport);
        CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        return (service, transport, connected, disconnected, cts, startTask);
    }

    private static int Heartbeats(Mock<IPeerTransport> transport) => transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request));

    /// <summary>Closing the server connection closes the endpoint in the transport, marks the row closed and down, and stops both heartbeats and sends.</summary>
    [Fact]
    public async Task SetClosed_True_ClosesEndpointStopsHeartbeatsAndSends()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, _, CancellationTokenSource cts, Task startTask) = BuildRunning();
        await Task.Delay(50);
        connected.Publish(new PeerConnectionEventArgs { Connection = ConnectionTo(serverEndpoint) });
        int raised = 0;
        service.StatusesChanged += () => raised++;

        service.SetClosed(PeerConnectionKind.Server, string.Empty, true);

        transport.Verify(t => t.SetClosed(serverEndpoint, true), Times.Once);
        PeerConnectionStatus status = Assert.Single(service.GetStatuses());
        Assert.True(status.IsClosed);
        Assert.False(status.IsConnected);
        Assert.True(raised > 0);
        Assert.False(await service.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" }));

        cts.Cancel();
        await startTask;
    }

    /// <summary>A closed client drops the connections the server opened to it and rejects new ones, but accepts them again once reopened.</summary>
    [Fact]
    public async Task SetClosed_DropsAndRejectsInboundConnections_UntilReopened()
    {
        (ClientPeerService service, _, TestObservable<PeerConnectionEventArgs> connected, _, CancellationTokenSource cts, Task startTask) = BuildRunning();
        await Task.Delay(50);
        int existingDrops = 0;
        int lateDrops = 0;
        int reopenedDrops = 0;
        connected.Publish(new PeerConnectionEventArgs { Connection = new PeerConnection(null, true, "CN=Server", () => existingDrops++) });

        service.SetClosed(PeerConnectionKind.Server, string.Empty, true);
        connected.Publish(new PeerConnectionEventArgs { Connection = new PeerConnection(null, true, "CN=Server", () => lateDrops++) });
        service.SetClosed(PeerConnectionKind.Server, string.Empty, false);
        connected.Publish(new PeerConnectionEventArgs { Connection = new PeerConnection(null, true, "CN=Server", () => reopenedDrops++) });

        Assert.Equal(1, existingDrops);
        Assert.Equal(1, lateDrops);
        Assert.Equal(0, reopenedDrops);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Reopening reopens the endpoint and resumes heartbeats.</summary>
    [Fact]
    public async Task SetClosed_False_ReopensAndResumesHeartbeats()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, CancellationTokenSource cts, Task startTask) = BuildRunning();
        await Task.Delay(50);
        service.SetClosed(PeerConnectionKind.Server, string.Empty, true);
        await Task.Delay(100);
        int whileClosed = Heartbeats(transport);

        service.SetClosed(PeerConnectionKind.Server, string.Empty, false);
        await WaitUntil(() => Heartbeats(transport) > whileClosed, TimeSpan.FromSeconds(2));

        transport.Verify(t => t.SetClosed(serverEndpoint, false), Times.Once);
        Assert.False(Assert.Single(service.GetStatuses()).IsClosed);
        Assert.True(await service.Send("DEST", new TestMessage { MessageId = "M2", FromUser = "SOURCE" }));

        cts.Cancel();
        await startTask;
    }

    /// <summary>Refresh resets the server endpoint, drops the connections the server opened, and heartbeats straight away.</summary>
    [Fact]
    public async Task Refresh_ResetsEndpointDropsInboundAndHeartbeatsNow()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, _, CancellationTokenSource cts, Task startTask) = BuildRunning();
        await WaitUntil(() => Heartbeats(transport) >= 1, TimeSpan.FromSeconds(2));
        int drops = 0;
        connected.Publish(new PeerConnectionEventArgs { Connection = new PeerConnection(null, true, "CN=Server", () => drops++) });
        int before = Heartbeats(transport);

        service.Refresh(PeerConnectionKind.Server, string.Empty);

        transport.Verify(t => t.Reset(serverEndpoint), Times.Once);
        Assert.Equal(1, drops);
        await WaitUntil(() => Heartbeats(transport) > before, TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }

    /// <summary>Refresh does nothing while closed, and neither call does anything before the service has started or for a Client-kind row.</summary>
    [Fact]
    public async Task RefreshAndSetClosed_IgnoredWhenNotApplicable()
    {
        (ClientPeerService notStarted, _, _, _, _) = Build();
        notStarted.SetClosed(PeerConnectionKind.Server, string.Empty, true);
        notStarted.Refresh(PeerConnectionKind.Server, string.Empty);
        Assert.False(Assert.Single(notStarted.GetStatuses()).IsClosed);

        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, CancellationTokenSource cts, Task startTask) = BuildRunning();
        await Task.Delay(50);
        service.SetClosed(PeerConnectionKind.Client, "X", true);
        Assert.False(Assert.Single(service.GetStatuses()).IsClosed);
        service.SetClosed(PeerConnectionKind.Server, string.Empty, true);
        service.Refresh(PeerConnectionKind.Server, string.Empty);
        transport.Verify(t => t.Reset(It.IsAny<UserEndpoint>()), Times.Never);

        cts.Cancel();
        await startTask;
    }

    /// <summary>StatusesChanged fires when the server connection comes up, which is when its first heartbeat is acknowledged.</summary>
    [Fact]
    public async Task StatusesChanged_OnServerConnect_Fires()
    {
        (ClientPeerService service, Mock<IPeerTransport> transport, _, _, _) = Build();
        AutoAcknowledge(transport);
        using CancellationTokenSource cts = new();

        TaskCompletionSource raised = new();
        service.StatusesChanged += () => raised.TrySetResult();

        Task startTask = service.Start(cts.Token);

        await raised.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }
}
