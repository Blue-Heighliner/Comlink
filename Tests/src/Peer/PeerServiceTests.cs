namespace BlueHeighliner.Comlink.Tests.Peer;

/// <summary>Unit tests for <see cref="PeerService"/> message routing and delivery-status dispatch.</summary>
public sealed class PeerServiceTests
{
    private static readonly ILoggerFactory NoLogger = LoggerFactory.Create(_ => { });
    private static readonly UserEndpoint FakeUserEndpoint = new() { IpAddress = "127.0.0.1", Port = 12345 };
    private static readonly IMessageFormat Format = new TestMessageFormat();

    private static PeerService BuildService(Mock<IOftPeer> peerMock, Mock<IUserDirectory> userDirectoryMock)
        => new(peerMock.Object, userDirectoryMock.Object, new DefaultPortConfiguration(), Format, NoLogger);

    private static Mock<IUserDirectory> BuildUserDirectory()
    {
        Mock<IUserDirectory> locator = new();
        locator.Setup(l => l.GetEndpoint(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(FakeUserEndpoint);
        return locator;
    }

    private static ReadOnlyMemory<byte> Encode(TestMessage message)
    {
        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    // ── HandleMessage ─────────────────────────────────────────────────────────

    /// <summary>A valid message fires MessageDelivered with the correct fields.</summary>
    [Fact]
    public async Task HandleMessage_ValidMessage_RaisesMessageDeliveredEvent()
    {
        Mock<IOftPeer> peer = new();
        Mock<IUserDirectory> userDirectory = BuildUserDirectory();

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
        Mock<IOftPeer> peer = new();
        Mock<IUserDirectory> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        bool ok = await svc.HandleMessage(new byte[] { 0xFF, 0xFE, 0xFD });

        Assert.False(ok);
    }

    /// <summary>A message carrying a non-empty ConfirmationMessageId raises ConfirmationReceived, not MessageDelivered.</summary>
    [Fact]
    public async Task HandleMessage_ConfirmationMessage_RaisesConfirmationReceivedNotMessageDelivered()
    {
        Mock<IOftPeer> peer = new();
        Mock<IUserDirectory> userDirectory = BuildUserDirectory();

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
        Mock<IOftPeer> peer = new();
        Mock<IUserDirectory> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        bool confirmationFired = false;
        svc.ConfirmationReceived += (_, _) => { confirmationFired = true; return Task.CompletedTask; };

        TestMessage payload = new() { MessageId = "MSG1", FromUser = "REMOTE" };
        bool ok = await svc.HandleMessage(Encode(payload));

        Assert.True(ok);
        Assert.False(confirmationFired);
    }

    // ── Send ──────────────────────────────────────────────────────────────────

    /// <summary>Send resolves the user's endpoint, serializes the message, and forwards it to the peer.</summary>
    [Fact]
    public async Task Send_ForwardsSerializedMessageToPeer()
    {
        Mock<IOftPeer> peer = new();
        peer.Setup(p => p.Send("127.0.0.1", 12345, It.IsAny<ReadOnlyMemory<byte>>(), 0, It.IsAny<object?>(), default))
            .Returns(Task.CompletedTask);
        Mock<IUserDirectory> userDirectory = new();
        userDirectory.Setup(l => l.GetEndpoint("DEST", default)).ReturnsAsync(FakeUserEndpoint);

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        peer.Verify(p => p.Send("127.0.0.1", 12345, It.IsAny<ReadOnlyMemory<byte>>(), 0, It.IsAny<object?>(), default), Times.Once);
    }

    /// <summary>Send passes the message's IMessageFormat.GetPriority value through as the OFT send priority.</summary>
    [Fact]
    public async Task Send_UsesMessagePriorityAsOftPriority()
    {
        Mock<IOftPeer> peer = new();
        peer.Setup(p => p.Send("127.0.0.1", 12345, It.IsAny<ReadOnlyMemory<byte>>(), 3, It.IsAny<object?>(), default))
            .Returns(Task.CompletedTask);
        Mock<IUserDirectory> userDirectory = new();
        userDirectory.Setup(l => l.GetEndpoint("DEST", default)).ReturnsAsync(FakeUserEndpoint);

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE", Priority = 3 };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        peer.Verify(p => p.Send("127.0.0.1", 12345, It.IsAny<ReadOnlyMemory<byte>>(), 3, It.IsAny<object?>(), default), Times.Once);
    }

    /// <summary>Send returns false without contacting the peer when the user cannot be resolved.</summary>
    [Fact]
    public async Task Send_UnknownUser_ReturnsFalse()
    {
        Mock<IOftPeer> peer = new();
        Mock<IUserDirectory> userDirectory = new();
        userDirectory.Setup(l => l.GetEndpoint("UNKNOWN", default)).ReturnsAsync((UserEndpoint?)null);

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("UNKNOWN", msg);

        Assert.False(ok);
        peer.Verify(p => p.Send(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Send returns false when the underlying OFT send throws (e.g. the peer is unreachable).</summary>
    [Fact]
    public async Task Send_PeerSendThrows_ReturnsFalse()
    {
        Mock<IOftPeer> peer = new();
        peer.Setup(p => p.Send(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OftDisconnectedException());
        Mock<IUserDirectory> userDirectory = BuildUserDirectory();

        PeerService svc = BuildService(peer, userDirectory);
        TestMessage msg = new() { MessageId = "M1", FromUser = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.False(ok);
    }

    // ── DeliveryStatusChanged ────────────────────────────────────────────────

    /// <summary>Raising the peer's DeliveryStatusHandler for a tag from a Send call re-raises DeliveryStatusChanged with the matching message and user.</summary>
    [Fact]
    public async Task DeliveryStatusHandler_ForKnownTag_RaisesDeliveryStatusChanged()
    {
        Mock<IOftPeer> peer = new();
        Action<object, OftDeliveryStatus>? handler = null;
        peer.SetupSet(p => p.DeliveryStatusHandler = It.IsAny<Action<object, OftDeliveryStatus>>())
            .Callback<Action<object, OftDeliveryStatus>>(h => handler = h);
        object? capturedTag = null;
        peer.Setup(p => p.Send(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, ReadOnlyMemory<byte>, int, object?, CancellationToken>((_, _, _, _, tag, _) => capturedTag = tag)
            .Returns(Task.CompletedTask);
        Mock<IUserDirectory> userDirectory = BuildUserDirectory();

        // The DeliveryStatusHandler setter runs during construction, so Setup above must be in place first.
        PeerService svc = BuildService(peer, userDirectory);

        TaskCompletionSource<(string MessageId, string UserName, OftDeliveryStatus Status)> tcs = new();
        svc.DeliveryStatusChanged += (messageId, userName, status) =>
        {
            tcs.TrySetResult((messageId, userName, status));
            return Task.CompletedTask;
        };

        await svc.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.NotNull(handler);
        Assert.NotNull(capturedTag);
        handler(capturedTag, OftDeliveryStatus.Acknowledged);

        (string MessageId, string UserName, OftDeliveryStatus Status) result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("M1", result.MessageId);
        Assert.Equal("DEST", result.UserName);
        Assert.Equal(OftDeliveryStatus.Acknowledged, result.Status);
    }
}
