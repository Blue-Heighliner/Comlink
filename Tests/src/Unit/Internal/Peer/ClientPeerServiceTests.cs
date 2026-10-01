namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="ClientPeerService"/> connection status tracking, send coalescing, and message dispatch.</summary>
public sealed class ClientPeerServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private static readonly ConnectionPoint serverPoint = new() { IpAddress = "10.0.0.1", Port = 9000 };

    private sealed class Fixture(ClientPeerService service, Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, TestObservable<PeerConnectionEventArgs> disconnected, TestObservable<PeerReceivedEventArgs> received, PeerConnection server)
    {
        private int isUp;

        public ClientPeerService Service { get; } = service;
        public Mock<IPeerTransport> Transport { get; } = transport;
        public TestObservable<PeerConnectionEventArgs> Connected { get; } = connected;
        public TestObservable<PeerConnectionEventArgs> Disconnected { get; } = disconnected;
        public TestObservable<PeerReceivedEventArgs> Received { get; } = received;
        public PeerConnection Server { get; } = server;

        public void Come() => Connected.Publish(new PeerConnectionEventArgs { Connection = Server });

        public void ComeOnce()
        {
            if (Interlocked.Exchange(ref isUp, 1) == 0) { Come(); }
        }

        public void Drop()
        {
            Interlocked.Exchange(ref isUp, 0);
            Disconnected.Publish(new PeerConnectionEventArgs { Connection = Server });
        }
    }

    private static PeerConnection ServerConnection(ConnectionPoint point, string name = "Server1", Action? drop = null)
        => new(point, point.IsSerial ? new SerialConnectionInfo() : new IpConnectionInfo(), drop ?? (() => { })) { User = new UserIdentity { Name = name } };

    private static Fixture Build(bool pointConfigured = true, ConnectionPoint? point = null, bool reachable = true, string serverName = "Server1")
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

        ConnectionPoint target = point ?? serverPoint;
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(p => p.OutgoingPoints).Returns(pointConfigured ? [target] : []);

        ClientPeerService service = new(transportFactory.Object, engineController.Object, noLogger);
        Fixture fixture = new(service, transport, connected, disconnected, received, ServerConnection(target, serverName));
        if (reachable)
        {
            transport.Setup(p => p.Connect(target, It.IsAny<CancellationToken>())).Returns(() =>
            {
                fixture.ComeOnce();
                return Task.FromResult(fixture.Server);
            });
        }
        else
        {
            transport.Setup(p => p.Connect(target, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("refused"));
        }

        return fixture;
    }

    /// <summary>Configures the transport so every request over the server connection immediately returns an acknowledgement.</summary>
    private static void AutoAcknowledge(Fixture fx, bool success = true)
        => fx.Transport.Setup(p => p.Request(fx.Server, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);

    private static readonly INetworkSerializer serializer = new ProtobufNetworkSerializer();

    private static ReadOnlyMemory<byte> Encode(TestMessage message)
    {
        using IMemoryOwner<byte> buf = serializer.Serialize(message);
        return buf.Memory.ToArray();
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

    private static async Task<(Fixture Fixture, CancellationTokenSource Cts, Task StartTask)> StartConnected()
    {
        Fixture fx = Build();
        AutoAcknowledge(fx);
        CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await WaitUntil(() => fx.Service.GetStatuses().Single().IsConnected, TimeSpan.FromSeconds(2));
        return (fx, cts, startTask);
    }

    private static int Heartbeats(Mock<IPeerTransport> transport) => transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request));

    /// <summary>Start with no outgoing point configured logs an error and never creates a transport.</summary>
    [Fact]
    public async Task Start_NoPointConfigured_DoesNotConnect()
    {
        Fixture fx = Build(pointConfigured: false);

        await fx.Service.Start(CancellationToken.None);

        fx.Transport.Verify(p => p.Connect(It.IsAny<ConnectionPoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Start connects to the configured server and never listens: connections are bidirectional, so the server delivers over the one the client opened.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_ConnectsToServerAndStartsNoListener(bool serial)
    {
        ConnectionPoint point = serial ? new ConnectionPoint { SerialPort = "SL0" } : serverPoint;
        Fixture fx = Build(point: point);
        AutoAcknowledge(fx);
        using CancellationTokenSource cts = new();

        Task startTask = fx.Service.Start(cts.Token);
        await WaitUntil(() => Heartbeats(fx.Transport) >= 1, TimeSpan.FromSeconds(2));

        fx.Transport.Verify(p => p.StartListener(It.IsAny<int>()), Times.Never);
        fx.Transport.Verify(p => p.Connect(point, It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Reconfigure leaves the server connection alone when its point did not change, and moves to the new point, closing the old one, when it did.</summary>
    [Fact]
    public async Task Reconfigure_ChangesTheServerPointOnlyWhenItChanged()
    {
        ConnectionPoint other = new() { IpAddress = "10.0.0.2", Port = 9000 };
        IReadOnlyList<ConnectionPoint> outgoing = [serverPoint];
        Mock<IPeerTransport> transport = new();
        transport.SetupGet(p => p.Connected).Returns(new TestObservable<PeerConnectionEventArgs>());
        transport.SetupGet(p => p.Disconnected).Returns(new TestObservable<PeerConnectionEventArgs>());
        transport.SetupGet(p => p.Received).Returns(new TestObservable<PeerReceivedEventArgs>());
        transport.Setup(p => p.Connect(It.IsAny<ConnectionPoint>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("refused"));
        Mock<IPeerTransportFactory> factory = new();
        factory.Setup(f => f.Create()).Returns(transport.Object);
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(p => p.OutgoingPoints).Returns(() => outgoing);
        ClientPeerService service = new(factory.Object, engineController.Object, noLogger);
        int statusChanges = 0;
        service.StatusesChanged += () => statusChanges++;
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await WaitUntil(() => transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Connect)), TimeSpan.FromSeconds(2));

        service.Reconfigure();
        transport.Verify(p => p.SetClosed(It.IsAny<ConnectionPoint>(), It.IsAny<bool>()), Times.Never);
        Assert.Equal(0, statusChanges);

        outgoing = [other];
        service.Reconfigure();
        await WaitUntil(() => transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Connect) && Equals(i.Arguments[0], other)), TimeSpan.FromSeconds(2));

        transport.Verify(p => p.SetClosed(serverPoint, true), Times.Once);
        Assert.Equal(1, statusChanges);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Only the first outgoing point is the server: a client has one long-term connection.</summary>
    [Fact]
    public async Task Start_SeveralPoints_ConnectsToTheFirstOnly()
    {
        ConnectionPoint other = new() { IpAddress = "10.0.0.2", Port = 9000 };
        Mock<IPeerTransport> transport = new();
        transport.SetupGet(p => p.Connected).Returns(new TestObservable<PeerConnectionEventArgs>());
        transport.SetupGet(p => p.Disconnected).Returns(new TestObservable<PeerConnectionEventArgs>());
        transport.SetupGet(p => p.Received).Returns(new TestObservable<PeerReceivedEventArgs>());
        transport.Setup(p => p.Connect(It.IsAny<ConnectionPoint>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("refused"));
        Mock<IPeerTransportFactory> factory = new();
        factory.Setup(f => f.Create()).Returns(transport.Object);
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(p => p.OutgoingPoints).Returns([serverPoint, other]);
        ClientPeerService service = new(factory.Object, engineController.Object, noLogger);
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);
        await WaitUntil(() => transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Connect)), TimeSpan.FromSeconds(2));

        transport.Verify(p => p.Connect(other, It.IsAny<CancellationToken>()), Times.Never);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Send fails immediately before Start has ever run, since no transport has been created yet.</summary>
    [Fact]
    public async Task Send_BeforeStart_ReturnsFalse()
    {
        Fixture fx = Build();

        bool ok = await fx.Service.Send("ANY-USER", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.False(ok);
    }

    /// <summary>Send fails while the server connection is not up, without trying to reach the server itself.</summary>
    [Fact]
    public async Task Send_ServerUnreachable_ReturnsFalse()
    {
        Fixture fx = Build(reachable: false);
        using CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await Task.Delay(50);

        bool ok = await fx.Service.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.False(ok);
        fx.Transport.Verify(p => p.Request(It.IsAny<PeerConnection>(), It.Is<ReadOnlyMemory<byte>>(payload => !TestHeartbeat.Is(payload)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Send transmits over the server connection and returns true once acknowledged.</summary>
    [Fact]
    public async Task Send_AfterStart_TransmitsToServerAndReturnsTrue()
    {
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();

        bool ok = await fx.Service.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.True(ok);
        fx.Transport.Verify(p => p.Request(
            fx.Server,
            It.Is<ReadOnlyMemory<byte>>(payload => !TestHeartbeat.Is(payload)),
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
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();

        TestMessage message = new() { MessageId = "M1", FromUser = "SOURCE" };
        bool[] results = await Task.WhenAll(
            fx.Service.Send("USER-A", message),
            fx.Service.Send("USER-B", message),
            fx.Service.Send("USER-C", message));

        Assert.All(results, Assert.True);
        fx.Transport.Verify(p => p.Request(It.IsAny<PeerConnection>(), It.Is<ReadOnlyMemory<byte>>(payload => !TestHeartbeat.Is(payload)), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A valid message received over the server connection fires MessageDelivered.</summary>
    [Fact]
    public async Task Received_ValidMessage_RaisesMessageDelivered()
    {
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();
        TaskCompletionSource<object> tcs = new();
        fx.Service.MessageDelivered += message => { tcs.TrySetResult(message); return Task.CompletedTask; };

        fx.Received.Publish(new PeerReceivedEventArgs { Connection = fx.Server, Payload = Encode(new TestMessage { MessageId = "MSG1", FromUser = "REMOTE" }) });

        TestMessage message = Assert.IsType<TestMessage>(await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal("MSG1", message.MessageId);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A message on any connection other than the server's is ignored: a client takes deliveries only from its server.</summary>
    [Fact]
    public async Task Received_FromAnotherConnection_IsIgnored()
    {
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();
        bool delivered = false;
        fx.Service.MessageDelivered += _ => { delivered = true; return Task.CompletedTask; };

        fx.Received.Publish(new PeerReceivedEventArgs
        {
            Connection = new PeerConnection(null, new IpConnectionInfo { IsInbound = true }, () => { }),
            Payload = Encode(new TestMessage { MessageId = "MSG1", FromUser = "REMOTE" })
        });
        await Task.Delay(100);

        Assert.False(delivered);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Before Start ever runs, GetStatuses reports a single disconnected row.</summary>
    [Fact]
    public void GetStatuses_BeforeConnecting_ReturnsDisconnectedRow()
    {
        Fixture fx = Build();

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses());

        Assert.False(status.IsConnected);
        Assert.Null(status.LastConnectedAt);
        Assert.Null(status.LastDisconnectedAt);
    }

    /// <summary>A bare IP connection to the server does not count as up: a server that has closed this client accepts the connection and drops it again without ever answering, which would otherwise flash the row green.</summary>
    [Fact]
    public async Task GetStatuses_ConnectionWithoutAcknowledgedHeartbeat_StaysDown()
    {
        Fixture fx = Build(reachable: false);
        int raised = 0;
        fx.Service.StatusesChanged += () => raised++;
        using CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await Task.Delay(50);

        fx.Come();

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses());
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
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses());

        Assert.NotNull(status.LastConnectedAt);
        Assert.Null(status.LastDisconnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>An unexpected disconnect (not one caused by this node's own Close/Refresh) wakes the heartbeat monitor immediately, instead of leaving it to sleep out its current interval before retrying.</summary>
    [Fact]
    public async Task OnDisconnected_UnexpectedDrop_RetriesHeartbeatImmediately()
    {
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();
        int countBefore = Heartbeats(fx.Transport);

        fx.Drop();

        await WaitUntil(() => Heartbeats(fx.Transport) > countBefore, TimeSpan.FromSeconds(1));

        cts.Cancel();
        await startTask;
    }

    /// <summary>When every attempt fails and each one is followed by a disconnect (a server accepting and immediately dropping a connection it has closed), the disconnects do not wake the monitor, so retries stay on the retry interval instead of becoming a tight loop.</summary>
    [Fact]
    public async Task OnDisconnected_WhileRetrying_DoesNotCauseTightRetryLoop()
    {
        Fixture fx = Build();
        fx.Transport.Setup(p => p.Request(fx.Server, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                fx.Drop();
                return Task.FromException<bool>(new IOException("dropped"));
            });
        using CancellationTokenSource cts = new();

        Task startTask = fx.Service.Start(cts.Token);
        await Task.Delay(500);

        Assert.InRange(Heartbeats(fx.Transport), 1, 2);
        cts.Cancel();
        await startTask;
    }

    /// <summary>The row carries the name the server's connection was identified as, and keeps it after the connection drops.</summary>
    [Fact]
    public async Task GetStatuses_ServerIdentity_NamesTheRow()
    {
        Fixture fx = Build(serverName: "Server1");
        AutoAcknowledge(fx);
        using CancellationTokenSource cts = new();
        Assert.Equal(string.Empty, Assert.Single(fx.Service.GetStatuses()).UserName);
        Task startTask = fx.Service.Start(cts.Token);

        await WaitUntil(() => fx.Service.GetStatuses().Single().IsConnected, TimeSpan.FromSeconds(2));
        Assert.Equal("Server1", Assert.Single(fx.Service.GetStatuses()).UserName);

        fx.Drop();
        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses());
        Assert.False(status.IsConnected);
        Assert.Equal("Server1", status.UserName);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A serial server link only comes up when the far end answers, so it counts as up as soon as it is established, and going down is tracked too.</summary>
    [Fact]
    public async Task GetStatuses_SerialServerConnectedThenDisconnected_TracksBoth()
    {
        Fixture fx = Build(point: new ConnectionPoint { SerialPort = "SL0" }, reachable: false, serverName: "SL0");
        using CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await Task.Delay(20);

        fx.Come();
        PeerConnectionStatus up = Assert.Single(fx.Service.GetStatuses());
        Assert.True(up.IsConnected);
        Assert.Equal("SL0", up.UserName);

        fx.Drop();
        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses());
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastDisconnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A connection to some other point, or one the remote node opened, is not the server and does not affect the row.</summary>
    [Fact]
    public async Task GetStatuses_OtherConnections_ServerRowUnchanged()
    {
        Fixture fx = Build(reachable: false);
        using CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await Task.Delay(20);

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = ServerConnection(new ConnectionPoint { IpAddress = "10.9.9.9", Port = 1 }, "Other") });
        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = new PeerConnection(null, new IpConnectionInfo { IsInbound = true }, () => { }) { User = new UserIdentity { Name = "Other" } } });

        Assert.False(Assert.Single(fx.Service.GetStatuses()).IsConnected);
        Assert.Equal(string.Empty, Assert.Single(fx.Service.GetStatuses()).UserName);

        cts.Cancel();
        await startTask;
    }

    /// <summary>After a Disconnected event for the server connection, GetStatuses reports the row as disconnected with a LastDisconnectedAt timestamp.</summary>
    [Fact]
    public async Task GetStatuses_ServerDisconnected_ReturnsDisconnectedRowWithLastDisconnectedAt()
    {
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();

        fx.Drop();

        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses());
        Assert.False(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);
        Assert.NotNull(status.LastDisconnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Once the server connection is up, UserConnected fires with its identity and GetConnectedUsers lists it.</summary>
    [Fact]
    public async Task Come_ServerConnects_RaisesUserConnectedAndListsThem()
    {
        Fixture fx = Build(point: new ConnectionPoint { SerialPort = "SL0" }, reachable: false, serverName: "SL0");
        List<string> connected = [];
        fx.Service.UserConnected += name => { connected.Add(name); return Task.CompletedTask; };
        using CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await Task.Delay(20);

        fx.Come();

        await WaitUntil(() => connected.Count > 0, TimeSpan.FromSeconds(2));
        Assert.Equal(["SL0"], connected);
        Assert.Equal(["SL0"], fx.Service.GetConnectedUsers());

        cts.Cancel();
        await startTask;
    }

    /// <summary>Losing the server connection fires UserDisconnected and empties GetConnectedUsers.</summary>
    [Fact]
    public async Task Drop_ServerDisconnects_RaisesUserDisconnectedAndEmptiesConnectedUsers()
    {
        Fixture fx = Build(point: new ConnectionPoint { SerialPort = "SL0" }, reachable: false, serverName: "SL0");
        List<string> disconnected = [];
        fx.Service.UserDisconnected += name => { disconnected.Add(name); return Task.CompletedTask; };
        using CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await Task.Delay(20);
        fx.Come();

        fx.Drop();

        await WaitUntil(() => disconnected.Count > 0, TimeSpan.FromSeconds(2));
        Assert.Equal(["SL0"], disconnected);
        Assert.Empty(fx.Service.GetConnectedUsers());

        cts.Cancel();
        await startTask;
    }

    /// <summary>Before any connection ever comes up, GetConnectedUsers is empty.</summary>
    [Fact]
    public void GetConnectedUsers_BeforeConnecting_ReturnsEmpty()
    {
        Fixture fx = Build(reachable: false);

        Assert.Empty(fx.Service.GetConnectedUsers());
    }

    /// <summary>
    /// Even with no real message ever sent by the caller, the background connection monitor proactively
    /// connects to the server and sends an empty heartbeat over the connection, and GetStatuses reports it
    /// connected, so the status table doesn't stay perpetually disconnected while idle.
    /// </summary>
    [Fact]
    public async Task GetStatuses_HeartbeatConnectsProactively_ReportsConnectedWithoutAnyRealSend()
    {
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();

        fx.Transport.Verify(p => p.Request(
            fx.Server,
            It.Is<ReadOnlyMemory<byte>>(payload => TestHeartbeat.Is(payload)),
            It.IsAny<PeerSendOptions>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses());
        Assert.True(status.IsConnected);
        Assert.NotNull(status.LastConnectedAt);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Closing the server connection closes the point in the transport, drops the connection, marks the row closed and down, and stops both heartbeats and sends.</summary>
    [Fact]
    public async Task SetClosed_True_ClosesPointDropsConnectionAndStopsSends()
    {
        Fixture fx = Build();
        AutoAcknowledge(fx);
        using CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await WaitUntil(() => fx.Service.GetStatuses().Single().IsConnected, TimeSpan.FromSeconds(2));
        int raised = 0;
        fx.Service.StatusesChanged += () => raised++;

        fx.Service.SetClosed(PeerConnectionKind.Server, string.Empty, true);

        fx.Transport.Verify(t => t.SetClosed(serverPoint, true), Times.Once);
        PeerConnectionStatus status = Assert.Single(fx.Service.GetStatuses());
        Assert.True(status.IsClosed);
        Assert.False(status.IsConnected);
        Assert.True(raised > 0);
        Assert.False(await fx.Service.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" }));

        cts.Cancel();
        await startTask;
    }

    /// <summary>A closed client drops the server connection it is holding and any that forms while it is closed, but accepts one again once reopened.</summary>
    [Fact]
    public async Task SetClosed_DropsConnections_UntilReopened()
    {
        int existingDrops = 0;
        int lateDrops = 0;
        int reopenedDrops = 0;
        Fixture fx = Build(reachable: false);
        using CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await Task.Delay(50);
        PeerConnection existing = ServerConnection(serverPoint, drop: () => existingDrops++);
        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = existing });

        fx.Service.SetClosed(PeerConnectionKind.Server, string.Empty, true);
        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = ServerConnection(serverPoint, drop: () => lateDrops++) });
        fx.Service.SetClosed(PeerConnectionKind.Server, string.Empty, false);
        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = ServerConnection(serverPoint, drop: () => reopenedDrops++) });

        Assert.Equal(1, existingDrops);
        Assert.Equal(1, lateDrops);
        Assert.Equal(0, reopenedDrops);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Reopening reopens the point and resumes heartbeats.</summary>
    [Fact]
    public async Task SetClosed_False_ReopensAndResumesHeartbeats()
    {
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();
        fx.Service.SetClosed(PeerConnectionKind.Server, string.Empty, true);
        await Task.Delay(100);
        int whileClosed = Heartbeats(fx.Transport);

        fx.Service.SetClosed(PeerConnectionKind.Server, string.Empty, false);
        await WaitUntil(() => Heartbeats(fx.Transport) > whileClosed, TimeSpan.FromSeconds(2));

        fx.Transport.Verify(t => t.SetClosed(serverPoint, false), Times.Once);
        Assert.False(Assert.Single(fx.Service.GetStatuses()).IsClosed);
        await WaitUntil(() => fx.Service.GetStatuses().Single().IsConnected, TimeSpan.FromSeconds(2));
        Assert.True(await fx.Service.Send("DEST", new TestMessage { MessageId = "M2", FromUser = "SOURCE" }));

        cts.Cancel();
        await startTask;
    }

    /// <summary>Refresh resets the server point and heartbeats straight away.</summary>
    [Fact]
    public async Task Refresh_ResetsPointAndHeartbeatsNow()
    {
        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();
        int before = Heartbeats(fx.Transport);

        fx.Service.Refresh(PeerConnectionKind.Server, string.Empty);

        fx.Transport.Verify(t => t.Reset(serverPoint), Times.Once);
        await WaitUntil(() => Heartbeats(fx.Transport) > before, TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }

    /// <summary>Refresh does nothing while closed, and neither call does anything before the service has started or for a Client-kind row.</summary>
    [Fact]
    public async Task RefreshAndSetClosed_IgnoredWhenNotApplicable()
    {
        Fixture notStarted = Build();
        notStarted.Service.SetClosed(PeerConnectionKind.Server, string.Empty, true);
        notStarted.Service.Refresh(PeerConnectionKind.Server, string.Empty);
        Assert.False(Assert.Single(notStarted.Service.GetStatuses()).IsClosed);

        (Fixture fx, CancellationTokenSource cts, Task startTask) = await StartConnected();
        fx.Service.SetClosed(PeerConnectionKind.Client, "X", true);
        Assert.False(Assert.Single(fx.Service.GetStatuses()).IsClosed);
        fx.Service.SetClosed(PeerConnectionKind.Server, string.Empty, true);
        fx.Service.Refresh(PeerConnectionKind.Server, string.Empty);
        fx.Transport.Verify(t => t.Reset(It.IsAny<ConnectionPoint>()), Times.Never);

        cts.Cancel();
        await startTask;
    }

    /// <summary>StatusesChanged fires when the server connection comes up, which is when its first heartbeat is acknowledged.</summary>
    [Fact]
    public async Task StatusesChanged_OnServerConnect_Fires()
    {
        Fixture fx = Build();
        AutoAcknowledge(fx);
        using CancellationTokenSource cts = new();

        TaskCompletionSource raised = new();
        fx.Service.StatusesChanged += () => raised.TrySetResult();

        Task startTask = fx.Service.Start(cts.Token);

        await raised.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }

    /// <summary>The service is registered under two interfaces, so the container disposes it twice; the transport is still disposed only once.</summary>
    [Fact]
    public async Task DisposeAsync_Twice_DisposesTransportOnce()
    {
        Fixture fx = Build();
        AutoAcknowledge(fx);
        using CancellationTokenSource cts = new();
        Task startTask = fx.Service.Start(cts.Token);
        await Task.Delay(20);
        cts.Cancel();
        await startTask;

        await fx.Service.DisposeAsync();
        await fx.Service.DisposeAsync();

        fx.Transport.Verify(t => t.DisposeAsync(), Times.Once);
    }
}
