namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="PeerService"/> message routing and delivery-status dispatch.</summary>
public sealed class PeerServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private static readonly ConnectionPoint fakeConnectionPoint = new() { IpAddress = "127.0.0.1", Port = 12345 };

    private static Mock<IPeerTransport> BuildPeerMock()
    {
        Mock<IPeerTransport> peer = new();
        peer.SetupGet(p => p.Received).Returns(new TestObservable<PeerReceivedEventArgs>());
        peer.SetupGet(p => p.Connected).Returns(new TestObservable<PeerConnectionEventArgs>());
        peer.SetupGet(p => p.Disconnected).Returns(new TestObservable<PeerConnectionEventArgs>());
        peer.Setup(p => p.Connect(It.IsAny<ConnectionPoint>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("no route"));
        return peer;
    }

    /// <summary>Publishes a newly established connection identified as <paramref name="user"/> on <paramref name="peer"/>, the way the transport does once a connection has been identified.</summary>
    private static PeerConnection Reach(Mock<IPeerTransport> peer, string user, bool inbound = false)
    {
        PeerConnection connection = new(inbound ? null : fakeConnectionPoint, new IpConnectionInfo { IsInbound = inbound }, () => { }) { User = new UserIdentity { Name = user } };
        ((TestObservable<PeerConnectionEventArgs>)peer.Object.Connected).Publish(new PeerConnectionEventArgs { Connection = connection });
        return connection;
    }

    private static void Lose(Mock<IPeerTransport> peer, PeerConnection connection)
        => ((TestObservable<PeerConnectionEventArgs>)peer.Object.Disconnected).Publish(new PeerConnectionEventArgs { Connection = connection });

    private static PeerService BuildService(Mock<IPeerTransport> peerMock, Mock<TestEngineController> engineControllerMock)
        => new(peerMock.Object, engineControllerMock.Object, noLogger);

    private static Mock<TestEngineController> BuildUserDirectory()
    {
        return new Mock<TestEngineController> { CallBase = true };
    }

    /// <summary>Configures <paramref name="peer"/> so every <see cref="IPeerTransport.Request"/> call immediately returns a successful acknowledgement.</summary>
    private static void AutoAcknowledge(Mock<IPeerTransport> peer, bool success = true)
        => peer.Setup(p => p.Request(It.IsAny<PeerConnection>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);

    private static readonly ProtobufSerializer serializer = new();

    private static ReadOnlyMemory<byte> Encode(TestFrame message)
    {
        using IMemoryOwner<byte> buf = serializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    /// <summary>A valid message fires FrameDelivered with the correct fields.</summary>
    [Fact]
    public async Task HandleMessage_ValidMessage_RaisesMessageDeliveredEvent()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        object? received = null;
        svc.FrameDelivered += p => { received = p; return Task.CompletedTask; };

        TestFrame payload = new()
        {
            MessageId = "MSG1",
            FromUser = "REMOTE",
            Body = "Hello"
        };
        bool ok = await svc.HandleMessage(Encode(payload));

        Assert.True(ok);
        Assert.NotNull(received);
        TestFrame receivedMessage = Assert.IsType<TestFrame>(received);
        Assert.Equal("MSG1", receivedMessage.MessageId);
        Assert.Equal("REMOTE", receivedMessage.FromUser);
    }

    /// <summary>Corrupted (non-protobuf) bytes return false without throwing.</summary>
    [Fact]
    public async Task HandleMessage_CorruptData_ReturnsFalse()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        bool ok = await svc.HandleMessage(new byte[] { 0xFF, 0xFE, 0xFD });

        Assert.False(ok);
    }

    /// <summary>A message carrying a non-empty ReadReceiptMessageId raises ReadReceiptReceived, not FrameDelivered.</summary>
    [Fact]
    public async Task HandleMessage_ReadReceipt_RaisesReadReceiptReceivedNotMessageDelivered()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        object? delivered = null;
        svc.FrameDelivered += p => { delivered = p; return Task.CompletedTask; };
        (string MessageId, string ConfirmingUser)? confirmation = null;
        svc.ReadReceiptReceived += (messageId, user) => { confirmation = (messageId, user); return Task.CompletedTask; };

        TestFrame payload = new() { FromUser = "REMOTE", ReadReceiptMessageId = "ORIGINAL-MSG-1" };
        bool ok = await svc.HandleMessage(Encode(payload));

        Assert.True(ok);
        Assert.Null(delivered);
        Assert.NotNull(confirmation);
        Assert.Equal("ORIGINAL-MSG-1", confirmation!.Value.MessageId);
        Assert.Equal("REMOTE", confirmation.Value.ConfirmingUser);
    }

    /// <summary>An ordinary message (empty ReadReceiptMessageId) raises FrameDelivered, not ReadReceiptReceived.</summary>
    [Fact]
    public async Task HandleMessage_OrdinaryMessage_RaisesMessageDeliveredNotReadReceiptReceived()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        bool confirmationFired = false;
        svc.ReadReceiptReceived += (_, _) => { confirmationFired = true; return Task.CompletedTask; };

        TestFrame payload = new() { MessageId = "MSG1", FromUser = "REMOTE" };
        bool ok = await svc.HandleMessage(Encode(payload));

        Assert.True(ok);
        Assert.False(confirmationFired);
    }

    /// <summary>Send serializes the message and forwards it over the connection identified as the user, returning once it is acknowledged.</summary>
    [Fact]
    public async Task Send_ForwardsSerializedMessageOverTheUsersConnection()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        PeerService svc = BuildService(peer, BuildUserDirectory());
        PeerConnection connection = Reach(peer, "DEST");
        TestFrame msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        peer.Verify(p => p.Request(
            connection,
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.Is<PeerSendOptions>(o => o.Priority == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A connection the remote node opened carries messages to it just like one this node opened, and user names match ignoring case.</summary>
    [Fact]
    public async Task Send_InboundConnection_IsUsedAndUserMatchedIgnoringCase()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        PeerService svc = BuildService(peer, BuildUserDirectory());
        PeerConnection inbound = Reach(peer, "Dest", inbound: true);

        bool ok = await svc.Send("DEST", new TestFrame { MessageId = "M1", FromUser = "SOURCE" });

        Assert.True(ok);
        peer.Verify(p => p.Request(inbound, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>When a user has more than one connection, the newest is the one used.</summary>
    [Fact]
    public async Task Send_SeveralConnectionsForOneUser_UsesTheNewest()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        PeerService svc = BuildService(peer, BuildUserDirectory());
        PeerConnection older = Reach(peer, "DEST");
        PeerConnection newer = Reach(peer, "DEST", inbound: true);

        await svc.Send("DEST", new TestFrame { MessageId = "M1", FromUser = "SOURCE" });

        peer.Verify(p => p.Request(newer, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        peer.Verify(p => p.Request(older, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Once a user's connection is lost, sends to them fail until they are identified on a connection again.</summary>
    [Fact]
    public async Task Send_AfterConnectionLost_ReturnsFalse_UntilReconnected()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        PeerService svc = BuildService(peer, BuildUserDirectory());
        PeerConnection connection = Reach(peer, "DEST");
        Lose(peer, connection);

        Assert.False(await svc.Send("DEST", new TestFrame { MessageId = "M1", FromUser = "SOURCE" }));

        Reach(peer, "DEST");
        Assert.True(await svc.Send("DEST", new TestFrame { MessageId = "M2", FromUser = "SOURCE" }));
    }

    /// <summary>Send serializes the message through IEngineController.FrameSerializer rather than a hardcoded format, so a host override is honored.</summary>
    [Fact]
    public async Task Send_UsesEngineControllersNetworkSerializer()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        Mock<TestEngineController> userDirectory = BuildUserDirectory();
        Mock<IFrameSerializer> customSerializer = new();
        customSerializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns(new FixedMemoryOwner([9, 9, 9]));
        userDirectory.Setup(l => l.FrameSerializer).Returns(customSerializer.Object);
        PeerService svc = BuildService(peer, userDirectory);
        Reach(peer, "DEST");
        TestFrame msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        customSerializer.Verify(s => s.Serialize(msg), Times.Once);
        peer.Verify(p => p.Request(
            It.IsAny<PeerConnection>(),
            It.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(new byte[] { 9, 9, 9 })),
            It.IsAny<PeerSendOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Delivering locally waits for every subscriber of FrameDelivered, not only the last, so a slow earlier one (storing the message) has finished when it returns.</summary>
    [Fact]
    public async Task DeliverLocal_AwaitsEverySubscriber()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        PeerService svc = BuildService(peer, BuildUserDirectory());
        bool firstDone = false;
        svc.FrameDelivered += async _ => { await Task.Delay(50); firstDone = true; };
        svc.FrameDelivered += _ => Task.CompletedTask;

        await svc.DeliverLocal(new TestFrame { MessageId = "M1", FromUser = "SOURCE" });

        Assert.True(firstDone);
    }

    /// <summary>Send passes the message's IEngineController.GetPriority value through as the MSMT send priority.</summary>
    [Fact]
    public async Task Send_UsesMessagePriorityAsSendPriority()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        PeerService svc = BuildService(peer, BuildUserDirectory());
        Reach(peer, "DEST");
        TestFrame msg = new() { MessageId = "M1", FromUser = "SOURCE", Priority = 3 };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        peer.Verify(p => p.Request(
            It.IsAny<PeerConnection>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.Is<PeerSendOptions>(o => o.Priority == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Send returns false without contacting the peer, and reports the delivery as failed, when no connection is identified as the user.</summary>
    [Fact]
    public async Task Send_UserWithNoConnection_ReturnsFalseAndReportsFailure()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        PeerService svc = BuildService(peer, BuildUserDirectory());
        Reach(peer, "SOMEONE-ELSE");
        TaskCompletionSource<DestinationStatus> failed = new();
        svc.DeliveryStatusChanged += (_, _, status) => { failed.TrySetResult(status); return Task.CompletedTask; };
        TestFrame msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("UNKNOWN", msg);

        Assert.False(ok);
        Assert.Equal(DestinationStatus.Failed, await failed.Task.WaitAsync(TimeSpan.FromSeconds(30)));
        peer.Verify(p => p.Request(It.IsAny<PeerConnection>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Send returns false when the underlying peer request throws (e.g. the peer is unreachable).</summary>
    [Fact]
    public async Task Send_PeerRequestThrows_ReturnsFalse()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        peer.Setup(p => p.Request(It.IsAny<PeerConnection>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        Reach(peer, "DEST");
        TestFrame msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.False(ok);
    }

    /// <summary>Send returns false when the remote peer negatively acknowledges the message.</summary>
    [Fact]
    public async Task Send_NegativelyAcknowledged_ReturnsFalse()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer, success: false);
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        Reach(peer, "DEST");
        TestFrame msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.False(ok);
    }

    /// <summary>
    /// A connection attempt that fails outright (e.g. the target refused the connection) causes the
    /// underlying Request to throw, which Send treats as a failed delivery rather than propagating.
    /// </summary>
    [Fact]
    public async Task Send_ConnectionRefused_ReturnsFalsePromptly()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        peer.Setup(p => p.Request(It.IsAny<PeerConnection>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("refused"));
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        Reach(peer, "DEST");
        TestFrame msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        Task<bool> sendTask = svc.Send("DEST", msg);
        bool ok = await sendTask.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(ok);
    }

    /// <summary>A successful Request's outcome re-raises DeliveryStatusChanged with the matching message and user.</summary>
    [Fact]
    public async Task Send_Acknowledged_ReturnsTrueWithoutReportingReceived()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        Reach(peer, "DEST");

        List<DestinationStatus> statuses = [];
        svc.DeliveryStatusChanged += (_, _, status) =>
        {
            lock (statuses) { statuses.Add(status); }
            return Task.CompletedTask;
        };

        Assert.True(await svc.Send("DEST", new TestFrame { MessageId = "M1", FromUser = "SOURCE" }));
        lock (statuses) { Assert.DoesNotContain(DestinationStatus.Received, statuses); }
    }

    /// <summary>Start, using the production DI constructor, creates a transport via IPeerTransportFactory and starts its IP listener on PeerPort.</summary>
    [Fact]
    public async Task Start_UsesFactoryToCreateTransportAndStartsListener()
    {
        Mock<IPeerTransport> transport = BuildPeerMock();
        Mock<IPeerTransportFactory> factory = new();
        factory.Setup(f => f.Create()).Returns(transport.Object);

        Mock<TestEngineController> engineController = BuildUserDirectory();
        engineController.Setup(e => e.PeerPort).Returns(50021);

        PeerService svc = new(factory.Object, engineController.Object, noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = svc.Start(cts.Token);
        await Task.Delay(20);

        factory.Verify(f => f.Create(), Times.Once);
        transport.Verify(t => t.StartListener(50021), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Reconfigure restarts only the listener when the port changed, closes points that are no longer defined, opens new ones, and leaves unchanged points, and everything when nothing changed, alone.</summary>
    [Fact]
    public async Task Reconfigure_AppliesOnlyWhatChanged()
    {
        ConnectionPoint kept = new() { IpAddress = "10.0.0.1", Port = 1 };
        ConnectionPoint removed = new() { IpAddress = "10.0.0.2", Port = 2 };
        ConnectionPoint added = new() { IpAddress = "10.0.0.3", Port = 3 };
        IReadOnlyList<ConnectionPoint> outgoing = [kept, removed];
        int port = 50021;
        Mock<IPeerTransport> transport = BuildPeerMock();
        Mock<IPeerTransportFactory> factory = new();
        factory.Setup(f => f.Create()).Returns(transport.Object);
        Mock<TestEngineController> engineController = BuildUserDirectory();
        engineController.Setup(e => e.PeerPort).Returns(() => port);
        engineController.Setup(e => e.OutgoingPoints).Returns(() => outgoing);
        PeerService svc = new(factory.Object, engineController.Object, noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = svc.Start(cts.Token);
        await Task.Delay(50);

        svc.Reconfigure();
        transport.Verify(t => t.StopListener(), Times.Never);
        transport.Verify(t => t.SetClosed(It.IsAny<ConnectionPoint>(), It.IsAny<bool>()), Times.Never);

        port = 50022;
        outgoing = [kept, added];
        svc.Reconfigure();
        await Task.Delay(50);

        transport.Verify(t => t.StopListener(), Times.Once);
        transport.Verify(t => t.StartListener(50022), Times.Once);
        transport.Verify(t => t.StartListener(50021), Times.Once);
        transport.Verify(t => t.SetClosed(removed, true), Times.Once);
        transport.Verify(t => t.SetClosed(kept, It.IsAny<bool>()), Times.Never);
        transport.Verify(t => t.Connect(added, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        transport.Verify(t => t.Connect(kept, It.IsAny<CancellationToken>()), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Reconfigure before Start does nothing.</summary>
    [Fact]
    public void Reconfigure_BeforeStart_DoesNothing()
    {
        Mock<IPeerTransportFactory> factory = new();
        PeerService svc = new(factory.Object, BuildUserDirectory().Object, noLogger);

        svc.Reconfigure();

        factory.Verify(f => f.Create(), Times.Never);
    }

    /// <summary>Start keeps a connection open to every outgoing point, IP or serial, without being told which users are behind them.</summary>
    [Fact]
    public async Task Start_MaintainsEveryOutgoingPoint()
    {
        Mock<IPeerTransport> transport = BuildPeerMock();
        Mock<IPeerTransportFactory> factory = new();
        factory.Setup(f => f.Create()).Returns(transport.Object);
        ConnectionPoint serial = new() { SerialPort = "SL0", SerialAddress = 7 };
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(e => e.OutgoingPoints).Returns([fakeConnectionPoint, serial]);

        PeerService svc = new(factory.Object, engineController.Object, noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = svc.Start(cts.Token);

        await WaitUntil(
            () => transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Connect) && Equals(i.Arguments[0], serial))
                && transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Connect) && Equals(i.Arguments[0], fakeConnectionPoint)),
            TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }

    /// <summary>The transport reporting the payload as handed off raises DeliveryStatusChanged as Sent before the send completes.</summary>
    [Fact]
    public async Task Send_TransmittedCallback_RaisesSentDeliveryStatus()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        TaskCompletionSource requestStarted = new();
        TaskCompletionSource<bool> requestCompletion = new();
        peer.Setup(p => p.Request(It.IsAny<PeerConnection>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .Callback<PeerConnection, ReadOnlyMemory<byte>, PeerSendOptions?, CancellationToken>((_, _, options, _) =>
            {
                requestStarted.TrySetResult();
                options!.Transmitted!();
            })
            .Returns(requestCompletion.Task);
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        Reach(peer, "DEST");
        List<DestinationStatus> statuses = [];
        svc.DeliveryStatusChanged += (_, _, status) => { statuses.Add(status); return Task.CompletedTask; };

        Task<bool> sendTask = svc.Send("DEST", new TestFrame { MessageId = "M1", FromUser = "SOURCE" });
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await WaitUntil(() => statuses.Contains(DestinationStatus.Sent), TimeSpan.FromSeconds(30));

        requestCompletion.TrySetResult(true);
        await sendTask;
    }

    /// <summary>A message received on the transport is deserialized and raised as FrameDelivered.</summary>
    [Fact]
    public async Task TransportReceived_RaisesMessageDelivered()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        TestObservable<PeerReceivedEventArgs> received = (TestObservable<PeerReceivedEventArgs>)peer.Object.Received;
        PeerService svc = BuildService(peer, BuildUserDirectory());
        TaskCompletionSource<object> delivered = new();
        svc.FrameDelivered += payload => { delivered.TrySetResult(payload); return Task.CompletedTask; };

        received.Publish(new PeerReceivedEventArgs
        {
            Connection = new PeerConnection(null, new IpConnectionInfo { IsInbound = true }, () => { }),
            Payload = Encode(new TestFrame { MessageId = "M1", FromUser = "REMOTE" })
        });

        TestFrame message = Assert.IsType<TestFrame>(await delivered.Task.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal("M1", message.MessageId);
    }

    /// <summary>DisposeAsync disposes the underlying transport once Start has created one.</summary>
    [Fact]
    public async Task DisposeAsync_DisposesUnderlyingTransport()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = BuildUserDirectory();
        PeerService svc = BuildService(peer, userDirectory);

        await svc.DisposeAsync();

        peer.Verify(p => p.DisposeAsync(), Times.Once);
    }

    /// <summary>The first connection identified as a user raises UserConnected once, and GetConnectedUsers lists them.</summary>
    [Fact]
    public async Task Reach_FirstConnectionForUser_RaisesUserConnectedAndListsThem()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        PeerService svc = BuildService(peer, BuildUserDirectory());
        List<string> connected = [];
        svc.UserConnected += name => { connected.Add(name); return Task.CompletedTask; };

        Reach(peer, "DEST");

        await WaitUntil(() => connected.Count > 0, TimeSpan.FromSeconds(30));
        Assert.Equal(["DEST"], connected);
        Assert.Equal(["DEST"], svc.GetConnectedUsers());
    }

    /// <summary>A second connection for the same already-online user does not raise UserConnected again.</summary>
    [Fact]
    public async Task Reach_SecondConnectionForSameUser_DoesNotRaiseUserConnectedAgain()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        PeerService svc = BuildService(peer, BuildUserDirectory());
        List<string> connected = [];
        svc.UserConnected += name => { connected.Add(name); return Task.CompletedTask; };

        Reach(peer, "DEST");
        await WaitUntil(() => connected.Count > 0, TimeSpan.FromSeconds(30));
        Reach(peer, "DEST", inbound: true);
        await Task.Delay(50);

        Assert.Single(connected);
    }

    /// <summary>Losing a user's only connection raises UserDisconnected and removes them from GetConnectedUsers; losing one of several does not.</summary>
    [Fact]
    public async Task Lose_LastConnectionForUser_RaisesUserDisconnected()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        PeerService svc = BuildService(peer, BuildUserDirectory());
        List<string> disconnected = [];
        svc.UserDisconnected += name => { disconnected.Add(name); return Task.CompletedTask; };
        PeerConnection first = Reach(peer, "DEST");
        PeerConnection second = Reach(peer, "DEST", inbound: true);

        Lose(peer, first);
        await Task.Delay(50);
        Assert.Empty(disconnected);
        Assert.Equal(["DEST"], svc.GetConnectedUsers());

        Lose(peer, second);
        await WaitUntil(() => disconnected.Count > 0, TimeSpan.FromSeconds(30));
        Assert.Equal(["DEST"], disconnected);
        Assert.Empty(svc.GetConnectedUsers());
    }

    /// <summary>GetConnectedUsers starts empty with no connections identified.</summary>
    [Fact]
    public void GetConnectedUsers_NoConnections_ReturnsEmpty()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        PeerService svc = BuildService(peer, BuildUserDirectory());

        Assert.Empty(svc.GetConnectedUsers());
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

    private sealed class FixedMemoryOwner(byte[] data) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = data;
        public void Dispose() { }
    }
}
