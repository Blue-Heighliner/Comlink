namespace BlueHeighliner.Comlink.Tests.Unit.Peer;

/// <summary>Unit tests for <see cref="PeerService"/> message routing and delivery-status dispatch.</summary>
public sealed class PeerServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private static readonly UserEndpoint fakeUserEndpoint = new() { IpAddress = "127.0.0.1", Port = 12345 };

    private static Mock<IPeerTransport> BuildPeerMock()
    {
        Mock<IPeerTransport> peer = new();
        peer.SetupGet(p => p.Received).Returns(new TestObservable<PeerReceivedEventArgs>());
        return peer;
    }

    private static PeerService BuildService(Mock<IPeerTransport> peerMock, Mock<TestEngineController> engineControllerMock)
        => new(peerMock.Object, engineControllerMock.Object, noLogger);

    private static Mock<TestEngineController> BuildUserDirectory()
    {
        Mock<TestEngineController> locator = new() { CallBase = true };
        locator.Setup(l => l.GetEndpoint(It.IsAny<string>())).Returns(fakeUserEndpoint);
        return locator;
    }

    /// <summary>Configures <paramref name="peer"/> so every <see cref="IPeerTransport.Request"/> call immediately returns a successful acknowledgement.</summary>
    private static void AutoAcknowledge(Mock<IPeerTransport> peer, bool success = true)
        => peer.Setup(p => p.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);

    private static ReadOnlyMemory<byte> Encode(TestMessage message)
    {
        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    /// <summary>A valid message fires MessageDelivered with the correct fields.</summary>
    [Fact]
    public async Task HandleMessage_ValidMessage_RaisesMessageDeliveredEvent()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        object? received = null;
        svc.MessageDelivered += p => { received = p; return Task.CompletedTask; };

        TestMessage payload = new()
        {
            MessageId = "MSG1",
            FromUser = "REMOTE",
            Subject = "Hi",
            Body = "Hello"
        };
        bool ok = await svc.HandleMessage(Encode(payload));

        Assert.True(ok);
        Assert.NotNull(received);
        TestMessage receivedMessage = Assert.IsType<TestMessage>(received);
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

    /// <summary>A message carrying a non-empty ConfirmationMessageId raises ConfirmationReceived, not MessageDelivered.</summary>
    [Fact]
    public async Task HandleMessage_ConfirmationMessage_RaisesConfirmationReceivedNotMessageDelivered()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        object? delivered = null;
        svc.MessageDelivered += p => { delivered = p; return Task.CompletedTask; };
        (string MessageId, string ConfirmingUser)? confirmation = null;
        svc.ConfirmationReceived += (messageId, user) => { confirmation = (messageId, user); return Task.CompletedTask; };

        TestMessage payload = new() { FromUser = "REMOTE", ConfirmationMessageId = "ORIGINAL-MSG-1" };
        bool ok = await svc.HandleMessage(Encode(payload));

        Assert.True(ok);
        Assert.Null(delivered);
        Assert.NotNull(confirmation);
        Assert.Equal("ORIGINAL-MSG-1", confirmation!.Value.MessageId);
        Assert.Equal("REMOTE", confirmation.Value.ConfirmingUser);
    }

    /// <summary>An ordinary message (empty ConfirmationMessageId) raises MessageDelivered, not ConfirmationReceived.</summary>
    [Fact]
    public async Task HandleMessage_OrdinaryMessage_RaisesMessageDeliveredNotConfirmationReceived()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        bool confirmationFired = false;
        svc.ConfirmationReceived += (_, _) => { confirmationFired = true; return Task.CompletedTask; };

        TestMessage payload = new() { MessageId = "MSG1", FromUser = "REMOTE" };
        bool ok = await svc.HandleMessage(Encode(payload));

        Assert.True(ok);
        Assert.False(confirmationFired);
    }

    /// <summary>Send resolves the user's endpoint, serializes the message, forwards it to the peer, and returns once it is acknowledged.</summary>
    [Fact]
    public async Task Send_ForwardsSerializedMessageToPeer()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        Mock<TestEngineController> userDirectory = new() { CallBase = true };
        userDirectory.Setup(l => l.GetEndpoint("DEST")).Returns(fakeUserEndpoint);

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        peer.Verify(p => p.Request(
            It.Is<UserEndpoint>(t => t.IpAddress == "127.0.0.1" && t.Port == 12345),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.Is<PeerSendOptions>(o => o.Priority == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Send passes the message's IEngineController.GetPriority value through as the MSMT send priority.</summary>
    [Fact]
    public async Task Send_UsesMessagePriorityAsSendPriority()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        Mock<TestEngineController> userDirectory = new() { CallBase = true };
        userDirectory.Setup(l => l.GetEndpoint("DEST")).Returns(fakeUserEndpoint);

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE", Priority = 3 };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        peer.Verify(p => p.Request(
            It.IsAny<UserEndpoint>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.Is<PeerSendOptions>(o => o.Priority == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Send returns false without contacting the peer when the user cannot be resolved.</summary>
    [Fact]
    public async Task Send_UnknownUser_ReturnsFalse()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = new() { CallBase = true };
        userDirectory.Setup(l => l.GetEndpoint("UNKNOWN")).Returns((UserEndpoint?)null);

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("UNKNOWN", msg);

        Assert.False(ok);
        peer.Verify(p => p.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Send returns false when the underlying peer request throws (e.g. the peer is unreachable).</summary>
    [Fact]
    public async Task Send_PeerRequestThrows_ReturnsFalse()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        peer.Setup(p => p.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

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
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

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
        peer.Setup(p => p.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("refused"));
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        Task<bool> sendTask = svc.Send("DEST", msg);
        bool ok = await sendTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(ok);
    }

    /// <summary>A successful Request's outcome re-raises DeliveryStatusChanged with the matching message and user.</summary>
    [Fact]
    public async Task Send_Acknowledged_RaisesDeliveryStatusChanged()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);

        List<(string MessageId, string UserName, DestinationStatus Status)> events = [];
        TaskCompletionSource tcs = new();
        svc.DeliveryStatusChanged += (messageId, userName, status) =>
        {
            events.Add((messageId, userName, status));
            if (status == DestinationStatus.Confirmed) { tcs.TrySetResult(); }
            return Task.CompletedTask;
        };

        await svc.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains(events, e => e.MessageId == "M1" && e.UserName == "DEST" && e.Status == DestinationStatus.Confirmed);
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

    /// <summary>Start opens the link to every configured user reached over serial, so their messages are received before anything is sent to them.</summary>
    [Fact]
    public async Task Start_OpensSerialEndpointsOfConfiguredUsers()
    {
        Mock<IPeerTransport> transport = BuildPeerMock();
        Mock<IPeerTransportFactory> factory = new();
        factory.Setup(f => f.Create()).Returns(transport.Object);

        UserEndpoint serial = new() { SerialPort = "SL0", SerialAddress = 7 };
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(e => e.Users).Returns(["IPUSER", "SERIALUSER", "NOBODY"]);
        engineController.Setup(e => e.GetEndpoint("IPUSER")).Returns(fakeUserEndpoint);
        engineController.Setup(e => e.GetEndpoint("SERIALUSER")).Returns(serial);
        engineController.Setup(e => e.GetEndpoint("NOBODY")).Returns((UserEndpoint?)null);

        PeerService svc = new(factory.Object, engineController.Object, noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = svc.Start(cts.Token);
        await Task.Delay(20);

        transport.Verify(t => t.Open(serial), Times.Once);
        transport.Verify(t => t.Open(fakeUserEndpoint), Times.Never);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Send reaches a serial user through the same transport call as an IP user; the transport, not the service, chooses the medium.</summary>
    [Fact]
    public async Task Send_SerialEndpoint_PassesEndpointToTransport()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        UserEndpoint serial = new() { SerialPort = "SL0" };
        Mock<TestEngineController> userDirectory = new() { CallBase = true };
        userDirectory.Setup(l => l.GetEndpoint("DEST")).Returns(serial);

        PeerService svc = BuildService(peer, userDirectory);

        bool ok = await svc.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.True(ok);
        peer.Verify(p => p.Request(serial, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>The transport reporting the payload as handed off raises DeliveryStatusChanged as Sent before the send completes.</summary>
    [Fact]
    public async Task Send_TransmittedCallback_RaisesSentDeliveryStatus()
    {
        Mock<IPeerTransport> peer = BuildPeerMock();
        TaskCompletionSource requestStarted = new();
        TaskCompletionSource<bool> requestCompletion = new();
        peer.Setup(p => p.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .Callback<UserEndpoint, ReadOnlyMemory<byte>, PeerSendOptions?, CancellationToken>((_, _, options, _) =>
            {
                requestStarted.TrySetResult();
                options!.Transmitted!();
            })
            .Returns(requestCompletion.Task);
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        List<DestinationStatus> statuses = [];
        svc.DeliveryStatusChanged += (_, _, status) => { statuses.Add(status); return Task.CompletedTask; };

        Task<bool> sendTask = svc.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntil(() => statuses.Contains(DestinationStatus.Sent), TimeSpan.FromSeconds(2));

        requestCompletion.TrySetResult(true);
        await sendTask;
    }

    /// <summary>A message received on the transport is deserialized and raised as MessageDelivered.</summary>
    [Fact]
    public async Task TransportReceived_RaisesMessageDelivered()
    {
        Mock<IPeerTransport> peer = new();
        TestObservable<PeerReceivedEventArgs> received = new();
        peer.SetupGet(p => p.Received).Returns(received);
        PeerService svc = BuildService(peer, BuildUserDirectory());
        TaskCompletionSource<object> delivered = new();
        svc.MessageDelivered += payload => { delivered.TrySetResult(payload); return Task.CompletedTask; };

        received.Publish(new PeerReceivedEventArgs
        {
            Connection = new PeerConnection(null, true, null, () => { }),
            Payload = Encode(new TestMessage { MessageId = "M1", FromUser = "REMOTE" })
        });

        TestMessage message = Assert.IsType<TestMessage>(await delivered.Task.WaitAsync(TimeSpan.FromSeconds(2)));
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

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(10);
        }
    }
}
