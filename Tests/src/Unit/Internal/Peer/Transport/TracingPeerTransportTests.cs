namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer.Transport;

/// <summary>Unit tests for <see cref="TracingPeerTransport"/>.</summary>
public sealed class TracingPeerTransportTests
{
    private sealed class RecordingLogger : ILogger
    {
        public List<(EventId Id, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Entries.Add((eventId, formatter(state, exception)));
    }

    private static ILogSettings Settings(bool enabled)
    {
        Mock<ILogSettings> settings = new();
        settings.Setup(s => s.IsEnabled(It.IsAny<string>())).Returns(enabled);
        return settings.Object;
    }

    private static PeerConnection Connection(string? user = null) => new(new ConnectionPoint { IpAddress = "10.0.0.5", Port = 4000 }, new IpConnectionInfo { Host = "10.0.0.5", Port = 4000 }, () => { }) { User = user is null ? null : new UserIdentity { Name = user } };

    /// <summary>A request is logged as the hexadecimal bytes with the user it goes to, then passed on unchanged.</summary>
    [Fact]
    public async Task Request_LogsTheBytesAndTheUser_AndPassesThemOn()
    {
        Mock<IPeerTransport> inner = new();
        inner.SetupGet(i => i.Received).Returns(new TestObservable<PeerReceivedEventArgs>());
        inner.SetupGet(i => i.Connected).Returns(new TestObservable<PeerConnectionEventArgs>());
        inner.SetupGet(i => i.Disconnected).Returns(new TestObservable<PeerConnectionEventArgs>());
        PeerConnection connection = Connection("ALICE");
        inner.Setup(i => i.Request(connection, It.IsAny<ReadOnlyMemory<byte>>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        RecordingLogger logger = new();
        TracingPeerTransport transport = new(inner.Object, logger, Settings(true), "FRAMES", LogEvents.FrameSent, LogEvents.FrameReceived);

        bool accepted = await transport.Request(connection, new byte[] { 0x01, 0xAB }, null, CancellationToken.None);

        Assert.True(accepted);
        Assert.Equal((LogEvents.FrameSent, "Sent 2 bytes to ALICE: 01AB"), Assert.Single(logger.Entries));
    }

    /// <summary>A received payload is logged with the user it came from, an unidentified connection is said to be, and the payload is republished.</summary>
    [Fact]
    public void Received_LogsTheBytesAndTheUser_AndRepublishes()
    {
        Mock<IPeerTransport> inner = new();
        TestObservable<PeerReceivedEventArgs> received = new();
        inner.SetupGet(i => i.Received).Returns(received);
        inner.SetupGet(i => i.Connected).Returns(new TestObservable<PeerConnectionEventArgs>());
        inner.SetupGet(i => i.Disconnected).Returns(new TestObservable<PeerConnectionEventArgs>());
        RecordingLogger logger = new();
        TracingPeerTransport transport = new(inner.Object, logger, Settings(true), "PACKETS", LogEvents.PacketSent, LogEvents.PacketReceived);
        List<PeerReceivedEventArgs> republished = [];
        transport.Received.Listen(republished.Add);

        received.Publish(new PeerReceivedEventArgs { Connection = Connection(), Payload = new byte[] { 0xFF } });

        Assert.Equal((LogEvents.PacketReceived, "Received 1 bytes from unidentified: FF"), Assert.Single(logger.Entries));
        Assert.Single(republished);
    }

    /// <summary>While the trace category is off nothing is logged, and what is sent and received is passed on all the same.</summary>
    [Fact]
    public async Task WhileTheCategoryIsOff_NothingIsLogged()
    {
        Mock<IPeerTransport> inner = new();
        TestObservable<PeerReceivedEventArgs> received = new();
        inner.SetupGet(i => i.Received).Returns(received);
        inner.SetupGet(i => i.Connected).Returns(new TestObservable<PeerConnectionEventArgs>());
        inner.SetupGet(i => i.Disconnected).Returns(new TestObservable<PeerConnectionEventArgs>());
        PeerConnection connection = Connection("ALICE");
        inner.Setup(i => i.Request(connection, It.IsAny<ReadOnlyMemory<byte>>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        RecordingLogger logger = new();
        TracingPeerTransport transport = new(inner.Object, logger, Settings(false), "FRAMES", LogEvents.FrameSent, LogEvents.FrameReceived);
        List<PeerReceivedEventArgs> republished = [];
        transport.Received.Listen(republished.Add);

        Assert.True(await transport.Request(connection, new byte[] { 1 }, null, CancellationToken.None));
        received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = new byte[] { 2 } });

        Assert.Empty(logger.Entries);
        Assert.Single(republished);
    }
}
