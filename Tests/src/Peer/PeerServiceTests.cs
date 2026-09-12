namespace BlueHeighliner.Comlink.Tests.Peer;

/// <summary>Unit tests for <see cref="PeerService"/> message routing and delivery-status dispatch.</summary>
public sealed class PeerServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private static readonly UserEndpoint fakeUserEndpoint = new() { IpAddress = "127.0.0.1", Port = 12345 };

    private static Mock<IMsmtPeer> BuildPeerMock()
    {
        Mock<IMsmtPeer> peer = new();
        peer.SetupGet(p => p.Received).Returns(new TestObservable<MsmtReceivedEventArgs>());
        peer.SetupGet(p => p.PackageChanged).Returns(new TestObservable<MsmtPackageChangedEventArgs>());
        return peer;
    }

    private static PeerService BuildService(Mock<IMsmtPeer> peerMock, Mock<TestEngineController> engineControllerMock)
        => new(peerMock.Object, engineControllerMock.Object, noLogger);

    private static Mock<TestEngineController> BuildUserDirectory()
    {
        Mock<TestEngineController> locator = new() { CallBase = true };
        locator.Setup(l => l.GetEndpoint(It.IsAny<string>())).Returns(fakeUserEndpoint);
        return locator;
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

    /// <summary>A valid message fires MessageDelivered with the correct fields.</summary>
    [Fact]
    public async Task HandleMessage_ValidMessage_RaisesMessageDeliveredEvent()
    {
        Mock<IMsmtPeer> peer = BuildPeerMock();
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
        Mock<IMsmtPeer> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        bool ok = await svc.HandleMessage(new byte[] { 0xFF, 0xFE, 0xFD });

        Assert.False(ok);
    }

    /// <summary>A message carrying a non-empty ConfirmationMessageId raises ConfirmationReceived, not MessageDelivered.</summary>
    [Fact]
    public async Task HandleMessage_ConfirmationMessage_RaisesConfirmationReceivedNotMessageDelivered()
    {
        Mock<IMsmtPeer> peer = BuildPeerMock();
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
        Mock<IMsmtPeer> peer = BuildPeerMock();
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
        Mock<IMsmtPeer> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        Mock<TestEngineController> userDirectory = new() { CallBase = true };
        userDirectory.Setup(l => l.GetEndpoint("DEST")).Returns(fakeUserEndpoint);

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        peer.Verify(p => p.Request(
            It.Is<MsmtNameTarget>(t => t.Host == "127.0.0.1" && t.Port == 12345),
            It.IsAny<IMemoryOwner<byte>>(),
            It.Is<MsmtSendOptions>(o => o.Priority == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Send passes the message's IEngineController.GetPriority value through as the MSMT send priority.</summary>
    [Fact]
    public async Task Send_UsesMessagePriorityAsSendPriority()
    {
        Mock<IMsmtPeer> peer = BuildPeerMock();
        AutoAcknowledge(peer);
        Mock<TestEngineController> userDirectory = new() { CallBase = true };
        userDirectory.Setup(l => l.GetEndpoint("DEST")).Returns(fakeUserEndpoint);

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE", Priority = 3 };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        peer.Verify(p => p.Request(
            It.IsAny<MsmtNameTarget>(),
            It.IsAny<IMemoryOwner<byte>>(),
            It.Is<MsmtSendOptions>(o => o.Priority == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Send returns false without contacting the peer when the user cannot be resolved.</summary>
    [Fact]
    public async Task Send_UnknownUser_ReturnsFalse()
    {
        Mock<IMsmtPeer> peer = BuildPeerMock();
        Mock<TestEngineController> userDirectory = new() { CallBase = true };
        userDirectory.Setup(l => l.GetEndpoint("UNKNOWN")).Returns((UserEndpoint?)null);

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("UNKNOWN", msg);

        Assert.False(ok);
        peer.Verify(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Send returns false when the underlying peer request throws (e.g. the peer is unreachable).</summary>
    [Fact]
    public async Task Send_PeerRequestThrows_ReturnsFalse()
    {
        Mock<IMsmtPeer> peer = BuildPeerMock();
        peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
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
        Mock<IMsmtPeer> peer = BuildPeerMock();
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
        Mock<IMsmtPeer> peer = BuildPeerMock();
        peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SocketException((int)SocketError.ConnectionRefused));
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
        Mock<IMsmtPeer> peer = BuildPeerMock();
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

    /// <summary>Start, using the production DI constructor, creates a peer via IMsmtPeerFactory and starts its listener on PeerPort.</summary>
    [Fact]
    public async Task Start_UsesFactoryToCreatePeerAndStartsListener()
    {
        Mock<IMsmtPeer> peer = BuildPeerMock();
        Mock<IMsmtPeerFactory> peerFactory = new();
        peerFactory.Setup(f => f.Create(It.IsAny<MsmtOptions>())).Returns(peer.Object);

        Mock<TestEngineController> engineController = BuildUserDirectory();
        engineController.Setup(e => e.PeerPort).Returns(50021);
        (X509Certificate2 identity, _, X509Certificate2Collection trustedAuthorities) = TestMsmtCertificates.Create();
        engineController.Setup(e => e.ConnectionOptions).Returns(new MsmtOptions
        {
            Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = trustedAuthorities }
        });

        PeerService svc = new(peerFactory.Object, engineController.Object, noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = svc.Start(cts.Token);
        await Task.Delay(20);

        peerFactory.Verify(f => f.Create(It.IsAny<MsmtOptions>()), Times.Once);
        peer.Verify(p => p.StartListener(50021, "0.0.0.0"), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Start logs and returns without creating a peer when no current user is registered to resolve an identity certificate for.</summary>
    [Fact]
    public async Task Start_NoCurrentUser_DoesNotCreatePeer()
    {
        Mock<IMsmtPeerFactory> peerFactory = new();
        Mock<TestEngineController> engineController = BuildUserDirectory();
        engineController.Setup(e => e.ConnectionOptions).Throws(new InvalidOperationException("no current user"));

        PeerService svc = new(peerFactory.Object, engineController.Object, noLogger);
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await svc.Start(cts.Token);

        peerFactory.Verify(f => f.Create(It.IsAny<MsmtOptions>()), Times.Never);
    }

    /// <summary>A PackageChanged event reporting PendingAcknowledgement raises DeliveryStatusChanged as Sent.</summary>
    [Fact]
    public async Task Send_PendingAcknowledgementPackageStatus_RaisesSentDeliveryStatus()
    {
        Mock<IMsmtPeer> peer = BuildPeerMock();
        TestObservable<MsmtPackageChangedEventArgs> packageChanged = new();
        peer.SetupGet(p => p.PackageChanged).Returns(packageChanged);
        TaskCompletionSource requestStarted = new();
        TaskCompletionSource<MsmtResponse> requestCompletion = new();
        peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .Callback<MsmtNameTarget, IMemoryOwner<byte>, MsmtSendOptions?, CancellationToken>((_, _, options, _) =>
            {
                requestStarted.TrySetResult();
                packageChanged.Publish(new MsmtPackageChangedEventArgs
                {
                    Link = Mock.Of<IMsmtLink>(),
                    Package = Mock.Of<IMsmtPackage>(pk => pk.Tag == options!.Tag),
                    Status = MsmtSendStatus.PendingAcknowledgement
                });
            })
            .Returns(requestCompletion.Task);
        Mock<TestEngineController> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        List<DestinationStatus> statuses = [];
        svc.DeliveryStatusChanged += (_, _, status) => { statuses.Add(status); return Task.CompletedTask; };

        Task<bool> sendTask = svc.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntil(() => statuses.Contains(DestinationStatus.Sent), TimeSpan.FromSeconds(2));

        requestCompletion.TrySetResult(new MsmtResponse { Success = true, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });
        await sendTask;
    }

    /// <summary>DisposeAsync disposes the underlying peer once Start has created one.</summary>
    [Fact]
    public async Task DisposeAsync_DisposesUnderlyingPeer()
    {
        Mock<IMsmtPeer> peer = BuildPeerMock();
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

    private sealed class UnownedMemory(ReadOnlyMemory<byte> data) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = data.ToArray();
        public void Dispose() { }
    }
}
