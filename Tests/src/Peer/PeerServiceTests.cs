namespace BlueHeighliner.Comlink.Tests.Peer;

/// <summary>Unit tests for <see cref="PeerService"/> message routing and delivery-status dispatch.</summary>
public sealed class PeerServiceTests
{
    private static readonly ILoggerFactory NoLogger = LoggerFactory.Create(_ => { });
    private static readonly SiteEndpoint FakeSiteEndpoint = new() { IpAddress = "127.0.0.1", Port = 12345 };
    private static readonly IMessageFormat Format = new TestMessageFormat();

    private static PeerService BuildService(Mock<IOftPeer> peerMock, Mock<ISiteLocator> siteLocatorMock)
        => new(peerMock.Object, siteLocatorMock.Object, new PortConfiguration(new EngineConfig()), Format, NoLogger);

    private static Mock<ISiteLocator> BuildSiteLocator()
    {
        Mock<ISiteLocator> locator = new();
        locator.Setup(l => l.GetEndpoint(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(FakeSiteEndpoint);
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
        Mock<ISiteLocator> siteLocator = BuildSiteLocator();

        PeerService svc = BuildService(peer, siteLocator);
        object? received = null;
        svc.MessageDelivered += p => { received = p; return Task.CompletedTask; };

        TestMessage payload = new()
        {
            MessageId = "MSG1",
            FromSite = "REMOTE",
            Subject = "Hi",
            Body = "Hello"
        };
        bool ok = await svc.HandleMessage(Encode(payload));

        Assert.True(ok);
        Assert.NotNull(received);
        TestMessage receivedMessage = Assert.IsType<TestMessage>(received);
        Assert.Equal("MSG1", receivedMessage.MessageId);
        Assert.Equal("REMOTE", receivedMessage.FromSite);
    }

    /// <summary>Corrupted (non-protobuf) bytes return false without throwing.</summary>
    [Fact]
    public async Task HandleMessage_CorruptData_ReturnsFalse()
    {
        Mock<IOftPeer> peer = new();
        Mock<ISiteLocator> siteLocator = BuildSiteLocator();

        PeerService svc = BuildService(peer, siteLocator);
        bool ok = await svc.HandleMessage(new byte[] { 0xFF, 0xFE, 0xFD });

        Assert.False(ok);
    }

    // ── Send ──────────────────────────────────────────────────────────────────

    /// <summary>Send resolves the site's endpoint, serializes the message, and forwards it to the peer.</summary>
    [Fact]
    public async Task Send_ForwardsSerializedMessageToPeer()
    {
        Mock<IOftPeer> peer = new();
        peer.Setup(p => p.Send("127.0.0.1", 12345, It.IsAny<ReadOnlyMemory<byte>>(), 0, It.IsAny<object?>(), default))
            .Returns(Task.CompletedTask);
        Mock<ISiteLocator> siteLocator = new();
        siteLocator.Setup(l => l.GetEndpoint("DEST", default)).ReturnsAsync(FakeSiteEndpoint);

        PeerService svc = BuildService(peer, siteLocator);
        TestMessage msg = new() { MessageId = "M1", FromSite = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.True(ok);
        peer.Verify(p => p.Send("127.0.0.1", 12345, It.IsAny<ReadOnlyMemory<byte>>(), 0, It.IsAny<object?>(), default), Times.Once);
    }

    /// <summary>Send returns false without contacting the peer when the site cannot be resolved.</summary>
    [Fact]
    public async Task Send_UnknownSite_ReturnsFalse()
    {
        Mock<IOftPeer> peer = new();
        Mock<ISiteLocator> siteLocator = new();
        siteLocator.Setup(l => l.GetEndpoint("UNKNOWN", default)).ReturnsAsync((SiteEndpoint?)null);

        PeerService svc = BuildService(peer, siteLocator);
        TestMessage msg = new() { MessageId = "M1", FromSite = "SOURCE" };

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
        Mock<ISiteLocator> siteLocator = BuildSiteLocator();

        PeerService svc = BuildService(peer, siteLocator);
        TestMessage msg = new() { MessageId = "M1", FromSite = "SOURCE" };

        bool ok = await svc.Send("DEST", msg);

        Assert.False(ok);
    }

    // ── DeliveryStatusChanged ────────────────────────────────────────────────

    /// <summary>Raising the peer's DeliveryStatusHandler for a tag from a Send call re-raises DeliveryStatusChanged with the matching message and site.</summary>
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
        Mock<ISiteLocator> siteLocator = BuildSiteLocator();

        // The DeliveryStatusHandler setter runs during construction, so Setup above must be in place first.
        PeerService svc = BuildService(peer, siteLocator);

        TaskCompletionSource<(string MessageId, string SiteName, OftDeliveryStatus Status)> tcs = new();
        svc.DeliveryStatusChanged += (messageId, siteName, status) =>
        {
            tcs.TrySetResult((messageId, siteName, status));
            return Task.CompletedTask;
        };

        await svc.Send("DEST", new TestMessage { MessageId = "M1", FromSite = "SOURCE" });

        Assert.NotNull(handler);
        Assert.NotNull(capturedTag);
        handler(capturedTag, OftDeliveryStatus.Acknowledged);

        (string MessageId, string SiteName, OftDeliveryStatus Status) result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("M1", result.MessageId);
        Assert.Equal("DEST", result.SiteName);
        Assert.Equal(OftDeliveryStatus.Acknowledged, result.Status);
    }
}
