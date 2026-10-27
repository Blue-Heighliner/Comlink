namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer.Transport;

/// <summary>Unit tests for <see cref="MsmtPeerTransport"/>, the IP half of the peer transport.</summary>
public sealed class MsmtPeerTransportTests
{
    private static readonly ConnectionPoint target = new() { IpAddress = "10.0.0.5", Port = 4000 };

    private sealed record Fixture(
        MsmtPeerTransport Transport,
        Mock<IMsmtSessionPeer> Peer,
        TestObservable<IMsmtConnection> Connected,
        TestObservable<MsmtDisconnection> Disconnected,
        TestObservable<MsmtPackageChange> PackageChanged);

    private static Fixture Build(MicrosoftLogger? logger = null, TimeSpan? renewalLead = null, TimeSpan? renewalRetry = null)
    {
        Mock<IMsmtSessionPeer> peer = new();
        TestObservable<IMsmtConnection> connected = new();
        TestObservable<MsmtDisconnection> disconnected = new();
        TestObservable<MsmtPackageChange> packageChanged = new();
        peer.SetupGet(p => p.Connected).Returns(connected);
        peer.SetupGet(p => p.Disconnected).Returns(disconnected);
        peer.SetupGet(p => p.PackageChanged).Returns(packageChanged);
        peer.SetupProperty(p => p.Receiver);
        return new Fixture(new MsmtPeerTransport(peer.Object, logger, renewalLead, renewalRetry), peer, connected, disconnected, packageChanged);
    }

    private static Mock<IMsmtConnection> OutboundConnection(ConnectionPoint point, bool connects = true, TimeSpan? endsIn = null)
    {
        Mock<IMsmtConnection> connection = new();
        if (endsIn is { } lifetime)
        {
            connection.SetupGet(c => c.Expiration).Returns(DateTime.UtcNow + lifetime);
        }

        connection.SetupGet(c => c.Direction).Returns(MsmtConnectionDirection.Outgoing);
        connection.SetupGet(c => c.Remote).Returns(new MsmtTarget { Host = point.IpAddress, Port = point.Port });
        connection.SetupGet(c => c.Status).Returns(connects ? MsmtConnectionStatus.Connected : MsmtConnectionStatus.Disconnected);
        connection.Setup(c => c.Wait(It.IsAny<CancellationToken>())).ReturnsAsync(connects);
        return connection;
    }

    private static Mock<IMsmtConnection> InboundConnection(string subject)
    {
        Mock<IMsmtConnection> connection = new();
        connection.SetupGet(c => c.Direction).Returns(MsmtConnectionDirection.Incoming);
        connection.SetupGet(c => c.Remote).Returns(new MsmtTarget { Host = "10.1.2.3", Port = 51234 });
        connection.SetupGet(c => c.Identity).Returns(new MsmtIdentity { Subject = subject, Issuer = string.Empty, SerialNumber = string.Empty, Thumbprint = string.Empty });
        return connection;
    }

    private static void Acknowledge(Mock<IMsmtConnection> connection, bool success = true)
        => connection.Setup(c => c.Request(It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MsmtResponse { Success = success, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });

    private static void Connects(Mock<IMsmtSessionPeer> peer, ConnectionPoint point, Mock<IMsmtConnection> connection)
        => peer.Setup(p => p.Connect(It.Is<MsmtNameTarget>(t => t.Host == point.IpAddress && t.Port == point.Port))).Returns(connection.Object);

    private static async Task<bool> RequestVia(Fixture fx, ConnectionPoint point, ReadOnlyMemory<byte> data, PeerSendOptions? options = null)
    {
        PeerConnection connection = await fx.Transport.Connect(point);
        return await fx.Transport.Request(connection, data, options);
    }

    private sealed class RecordingLogger : MicrosoftLogger
    {
        private readonly Lock gate = new();
        private readonly List<(EventId Id, string Message)> entries = [];

        public IReadOnlyList<(EventId Id, string Message)> Entries { get { lock (gate) { return [.. entries]; } } }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (gate) { entries.Add((eventId, formatter(state, exception))); }
        }

        public IReadOnlyList<string> Of(EventId id) => [.. Entries.Where(entry => entry.Id == id).Select(entry => entry.Message)];
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    /// <summary>A connection is replaced before its session ends: the replacement is offered as a new connection, becomes the one used from then on, and is logged, while the old one is left alone.</summary>
    [Fact]
    public async Task Connect_BeforeTheSessionEnds_ReplacesTheConnection()
    {
        RecordingLogger log = new();
        Fixture fx = Build(log, renewalLead: TimeSpan.FromMilliseconds(100));
        Mock<IMsmtConnection> first = OutboundConnection(target, endsIn: TimeSpan.FromMilliseconds(400));
        Mock<IMsmtConnection> second = OutboundConnection(target);
        fx.Peer.SetupSequence(p => p.Connect(It.IsAny<MsmtNameTarget>())).Returns(first.Object).Returns(second.Object);
        List<PeerConnection> offered = [];
        fx.Transport.Connected.Listen(args => { lock (offered) { offered.Add(args.Connection); } });

        PeerConnection original = await fx.Transport.Connect(target);
        await WaitUntil(() => { lock (offered) { return offered.Count == 2; } });

        Assert.Same(original, offered[0]);
        Assert.NotSame(original, offered[1]);
        Assert.Same(offered[1], await fx.Transport.Connect(target));
        Assert.Single(log.Of(LogEvents.IpRenewed));
        fx.Peer.Verify(p => p.Connect(It.IsAny<MsmtNameTarget>()), Times.Exactly(2));
        first.Verify(c => c.Dispose(), Times.Never);
    }

    /// <summary>A replacement that cannot be made is tried again until it can, while the old connection is still up.</summary>
    [Fact]
    public async Task Connect_ReplacementFails_IsRetried()
    {
        Fixture fx = Build(renewalLead: TimeSpan.FromMilliseconds(100), renewalRetry: TimeSpan.FromMilliseconds(20));
        Mock<IMsmtConnection> first = OutboundConnection(target, endsIn: TimeSpan.FromSeconds(5));
        fx.Peer.SetupSequence(p => p.Connect(It.IsAny<MsmtNameTarget>())).Returns(first.Object).Returns(OutboundConnection(target, connects: false).Object).Returns(OutboundConnection(target).Object);
        List<PeerConnection> offered = [];
        fx.Transport.Connected.Listen(args => { lock (offered) { offered.Add(args.Connection); } });

        await fx.Transport.Connect(target);
        await WaitUntil(() => { lock (offered) { return offered.Count == 2; } });

        fx.Peer.Verify(p => p.Connect(It.IsAny<MsmtNameTarget>()), Times.Exactly(3));
    }

    /// <summary>A point that is closed is not renewed.</summary>
    [Fact]
    public async Task Connect_PointClosed_IsNotRenewed()
    {
        Fixture fx = Build(renewalLead: TimeSpan.FromMilliseconds(100));
        Mock<IMsmtConnection> first = OutboundConnection(target, endsIn: TimeSpan.FromMilliseconds(400));
        fx.Peer.Setup(p => p.Connect(It.IsAny<MsmtNameTarget>())).Returns(first.Object);

        await fx.Transport.Connect(target);
        fx.Transport.SetClosed(target, true);
        await Task.Delay(600);

        fx.Peer.Verify(p => p.Connect(It.IsAny<MsmtNameTarget>()), Times.Once);
    }

    /// <summary>The listener starting and stopping is logged under IP.</summary>
    [Fact]
    public void Listener_LogsStartAndStop()
    {
        RecordingLogger log = new();
        Fixture fx = Build(log);
        fx.Peer.SetupGet(p => p.Listener).Returns(new MsmtTarget { Host = "0.0.0.0", Port = 50121 });

        fx.Transport.StartListener(50121);
        fx.Transport.StopListener();

        Assert.Equal("Listening for connections on 0.0.0.0:50121", Assert.Single(log.Of(LogEvents.IpListening)));
        Assert.Single(log.Of(LogEvents.IpListenerStopped));
    }

    /// <summary>An outgoing connection logs the attempt and the connection with its host and port.</summary>
    [Fact]
    public async Task Connect_LogsTheAttemptAndTheConnection()
    {
        RecordingLogger log = new();
        Fixture fx = Build(log);
        Connects(fx.Peer, target, OutboundConnection(target));

        await fx.Transport.Connect(target);

        Assert.Equal("Connecting to 10.0.0.5:4000", Assert.Single(log.Of(LogEvents.IpConnecting)));
        Assert.StartsWith("Connected to 10.0.0.5:4000", Assert.Single(log.Of(LogEvents.IpConnected)));
    }

    /// <summary>Repeated attempts that fail are logged once, with the reason, and the next attempt after a connection is logged again.</summary>
    [Fact]
    public async Task Connect_RepeatedFailures_AreLoggedOnce()
    {
        RecordingLogger log = new();
        Fixture fx = Build(log);
        Mock<IMsmtConnection> refused = OutboundConnection(target, connects: false);
        Connects(fx.Peer, target, refused);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            await Assert.ThrowsAsync<IOException>(() => fx.Transport.Connect(target));
            fx.Disconnected.Publish(new MsmtDisconnection { Connection = refused.Object, Exception = new IOException("connection refused") });
        }

        Assert.Single(log.Of(LogEvents.IpConnecting));
        Assert.Equal("Could not connect to 10.0.0.5:4000: connection refused", Assert.Single(log.Of(LogEvents.IpConnectFailed)));

        Mock<IMsmtConnection> first = OutboundConnection(target);
        Connects(fx.Peer, target, first);
        await fx.Transport.Connect(target);
        Assert.Single(log.Of(LogEvents.IpConnecting));

        first.SetupGet(c => c.Status).Returns(MsmtConnectionStatus.Disconnected);
        fx.Disconnected.Publish(new MsmtDisconnection { Connection = first.Object });
        Connects(fx.Peer, target, OutboundConnection(target));
        await fx.Transport.Connect(target);
        Assert.Equal(2, log.Of(LogEvents.IpConnecting).Count);
    }

    /// <summary>An accepted connection and its closing are logged with the other end's host, port, certificate and the reason, and one that never completes is logged as refused.</summary>
    [Fact]
    public void Accepted_LogsTheConnectionAndItsClosing()
    {
        RecordingLogger log = new();
        Fixture fx = Build(log);
        Mock<IMsmtConnection> accepted = InboundConnection("CN=Bob");
        Mock<IMsmtConnection> stalled = InboundConnection("CN=Eve");

        fx.Connected.Publish(accepted.Object);
        fx.Disconnected.Publish(new MsmtDisconnection { Connection = accepted.Object, Exception = new TimeoutException("idle") });
        fx.Disconnected.Publish(new MsmtDisconnection { Connection = stalled.Object, Exception = new IOException("handshake failed") });

        Assert.Equal("Accepted a connection from 10.1.2.3:51234 as CN=Bob", Assert.Single(log.Of(LogEvents.IpAccepted)));
        Assert.Equal("Connection from 10.1.2.3:51234 closed: idle", Assert.Single(log.Of(LogEvents.IpDisconnected)));
        Assert.Equal("A connection from 10.1.2.3:51234 never completed: handshake failed", Assert.Single(log.Of(LogEvents.IpRefused)));
    }

    /// <summary>A request opens a connection to the point's host and port, sends the payload over it, and returns the remote acknowledgement.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Request_SendsOverNewConnectionAndReturnsAcknowledgement(bool success)
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        Acknowledge(connection, success);

        bool result = await RequestVia(fx, target, new byte[] { 1, 2 }, new PeerSendOptions { Priority = 4 });

        Assert.Equal(success, result);
        fx.Peer.Verify(p => p.Connect(It.Is<MsmtNameTarget>(t => t.Host == "10.0.0.5" && t.Port == 4000)), Times.Once);
        connection.Verify(c => c.Request(
            It.Is<IMemoryOwner<byte>>(payload => payload.Memory.ToArray().SequenceEqual(new byte[] { 1, 2 })),
            It.Is<MsmtSendOptions>(o => o.Priority == 4),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A second request to the same point reuses the connection the first one opened instead of dialing again.</summary>
    [Fact]
    public async Task Request_SamePointTwice_ReusesConnection()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        Acknowledge(connection);

        await RequestVia(fx, target, new byte[] { 1 });
        await RequestVia(fx, target, new byte[] { 2 });

        fx.Peer.Verify(p => p.Connect(It.IsAny<MsmtNameTarget>()), Times.Once);
        connection.Verify(c => c.Request(It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>Once the cached connection reports itself disconnected, the next request to the same point opens a fresh one.</summary>
    [Fact]
    public async Task Request_CachedConnectionDisconnected_OpensFreshConnection()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> first = OutboundConnection(target);
        Acknowledge(first);
        Mock<IMsmtConnection> second = OutboundConnection(target);
        Acknowledge(second);
        fx.Peer.SetupSequence(p => p.Connect(It.IsAny<MsmtNameTarget>())).Returns(first.Object).Returns(second.Object);

        await RequestVia(fx, target, new byte[] { 1 });
        first.SetupGet(c => c.Status).Returns(MsmtConnectionStatus.Disconnected);
        await RequestVia(fx, target, new byte[] { 2 });

        fx.Peer.Verify(p => p.Connect(It.IsAny<MsmtNameTarget>()), Times.Exactly(2));
        second.Verify(c => c.Request(It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A connection that never finishes connecting fails the request with an IOException instead of an unhandled connection-layer exception.</summary>
    [Fact]
    public async Task Request_ConnectionNeverConnects_ThrowsIOException()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target, connects: false);
        Connects(fx.Peer, target, connection);

        await Assert.ThrowsAsync<IOException>(() => RequestVia(fx, target, new byte[] { 1 }));
    }

    /// <summary>The Transmitted callback fires when MSMT reports the package as awaiting acknowledgement, and not for other statuses.</summary>
    [Fact]
    public async Task Request_TransmittedCallback_FiresOnPendingAcknowledgementOnly()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        int transmitted = 0;
        TaskCompletionSource<MsmtResponse> completion = new();
        connection.Setup(c => c.Request(It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IMemoryOwner<byte>, MsmtSendOptions?, CancellationToken>((_, options, _) =>
            {
                foreach (MsmtSendStatus status in new[] { MsmtSendStatus.Queued, MsmtSendStatus.Transmitting, MsmtSendStatus.PendingAcknowledgement, MsmtSendStatus.Completed })
                {
                    fx.PackageChanged.Publish(new MsmtPackageChange
                    {
                        Package = Mock.Of<IMsmtPackage>(pk => pk.Tag == options!.Tag),
                        Status = status
                    });
                }
            })
            .Returns(completion.Task);

        Task<bool> request = RequestVia(fx, target, new byte[] { 1 }, new PeerSendOptions { Transmitted = () => transmitted++ });
        completion.SetResult(new MsmtResponse { Success = true, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });
        await request;

        Assert.Equal(1, transmitted);
    }

    /// <summary>Sends that share one Transmitted callback, as the packets of a payload do, get tags that are not equal to each other, since MSMT replaces an earlier send with an equal tag.</summary>
    [Fact]
    public async Task Request_SharedTransmittedCallback_GetsDistinctTags()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        List<object?> tags = [];
        connection.Setup(c => c.Request(It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IMemoryOwner<byte>, MsmtSendOptions?, CancellationToken>((_, options, _) => tags.Add(options!.Tag))
            .ReturnsAsync(new MsmtResponse { Success = true, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });
        Action shared = () => { };

        await RequestVia(fx, target, new byte[] { 1 }, new PeerSendOptions { Transmitted = shared });
        await RequestVia(fx, target, new byte[] { 2 }, new PeerSendOptions { Transmitted = shared });

        Assert.Equal(2, tags.Count);
        Assert.NotEqual(tags[0], tags[1]);
    }

    /// <summary>The lowest possible priority, which MSMT would mis-order because it negates it, is sent as the lowest priority MSMT can order correctly.</summary>
    [Fact]
    public async Task Request_LowestPriority_IsClampedSoMsmtOrdersItLast()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        Acknowledge(connection);

        await RequestVia(fx, target, new byte[] { 1 }, new PeerSendOptions { Priority = int.MinValue });

        connection.Verify(c => c.Request(It.IsAny<IMemoryOwner<byte>>(), It.Is<MsmtSendOptions>(o => o.Priority == int.MinValue + 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A send with no Transmitted callback carries no tag, so MSMT does no per-package progress tracking for it.</summary>
    [Fact]
    public async Task Request_WithoutTransmittedCallback_HasNoTag()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        Acknowledge(connection);

        await RequestVia(fx, target, new byte[] { 1 });

        connection.Verify(c => c.Request(It.IsAny<IMemoryOwner<byte>>(), It.Is<MsmtSendOptions>(o => o.Tag == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An outbound connection is published as connected, with the point it dialed, the first time something is sent to it.</summary>
    [Fact]
    public async Task Connected_Outbound_PublishedOnFirstRequest()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        Acknowledge(connection);
        PeerConnection? published = null;
        fx.Transport.Connected.Listen(args => published = args.Connection);

        await RequestVia(fx, target, new byte[] { 1 });

        Assert.NotNull(published);
        Assert.False(published.IsInbound);
        Assert.Equal(target, published.Point);
        Assert.Equal("10.0.0.5", ((IIpConnectionInfo)published.Info).Host);
        Assert.Equal(4000, ((IIpConnectionInfo)published.Info).Port);
    }

    /// <summary>Reusing a cached connection for a second request does not publish a second Connected event.</summary>
    [Fact]
    public async Task Connected_Outbound_PublishedOnlyOnceForReusedConnection()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        Acknowledge(connection);
        int publishCount = 0;
        fx.Transport.Connected.Listen(_ => publishCount++);

        await RequestVia(fx, target, new byte[] { 1 });
        await RequestVia(fx, target, new byte[] { 2 });

        Assert.Equal(1, publishCount);
    }

    /// <summary>A connection a remote node opened to this node's listener is published as inbound, with its certificate subject and no point.</summary>
    [Fact]
    public void Connected_Inbound_CarriesCertificateSubject()
    {
        Fixture fx = Build();
        PeerConnection? connection = null;
        fx.Transport.Connected.Listen(args => connection = args.Connection);

        fx.Connected.Publish(InboundConnection("CN=Alice").Object);

        Assert.NotNull(connection);
        Assert.True(connection.IsInbound);
        Assert.Null(connection.Point);
        Assert.Equal("CN=Alice", ((IIpConnectionInfo)connection.Info).CertificateSubject);
        Assert.Equal(["Alice"], ((IIpConnectionInfo)connection.Info).CertificateNames);
    }

    /// <summary>Dropping the published connection disposes the underlying MSMT connection.</summary>
    [Fact]
    public void Connection_Drop_DisposesUnderlyingConnection()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> msmt = InboundConnection("CN=Alice");
        PeerConnection? connection = null;
        fx.Transport.Connected.Listen(args => connection = args.Connection);
        fx.Connected.Publish(msmt.Object);

        connection!.Drop();

        msmt.Verify(c => c.Dispose(), Times.Once);
    }

    /// <summary>A received message is published with a copy of its payload, on the same connection object that was published as connected, and is accepted when acknowledgement was requested.</summary>
    [Fact]
    public async Task Received_PublishesPayloadCopyOnSameConnection_AndAcknowledgesWhenRequested()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> msmt = InboundConnection("CN=Alice");
        PeerConnection? connected = null;
        PeerReceivedEventArgs? received = null;
        fx.Transport.Connected.Listen(args => connected = args.Connection);
        fx.Transport.Received.Listen(args => received = args);
        fx.Connected.Publish(msmt.Object);

        Mock<IMsmtResponder> responder = new();

        fx.Peer.Object.Receiver!(msmt.Object, new TestOwner([7, 8, 9]), responder.Object);

        Assert.NotNull(received);
        Assert.Same(connected, received.Connection);
        Assert.Equal(new byte[] { 7, 8, 9 }, received.Payload.ToArray());
        responder.Verify(r => r.Accept(It.IsAny<IMemoryOwner<byte>>()), Times.Once);
        responder.Verify(r => r.Reject(It.IsAny<IMemoryOwner<byte>>()), Times.Never);
    }

    /// <summary>A received message that requested no acknowledgement is still published and its pooled payload is released.</summary>
    [Fact]
    public void Received_NoAcknowledgementRequested_PublishesAndReleasesPayload()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> msmt = InboundConnection("CN=Bob");
        PeerReceivedEventArgs? received = null;
        fx.Transport.Received.Listen(args => received = args);
        TestOwner owner = new([1]);

        fx.Peer.Object.Receiver!(msmt.Object, owner, null);

        Assert.NotNull(received);
        Assert.True(owner.IsDisposed);
    }

    /// <summary>A message on a connection never seen connected is still published, as inbound.</summary>
    [Fact]
    public void Received_UnknownConnection_PublishedAsInbound()
    {
        Fixture fx = Build();
        PeerReceivedEventArgs? received = null;
        fx.Transport.Received.Listen(args => received = args);

        fx.Peer.Object.Receiver!(InboundConnection("CN=Bob").Object, new TestOwner([1]), null);

        Assert.NotNull(received);
        Assert.True(received.Connection.IsInbound);
    }

    /// <summary>A disconnect is published for a connection that was published as connected, and ignored for one that was not.</summary>
    [Fact]
    public void Disconnected_PublishedOnlyForKnownConnections()
    {
        Fixture fx = Build();
        List<PeerConnection> disconnected = [];
        fx.Transport.Disconnected.Listen(args => disconnected.Add(args.Connection));
        Mock<IMsmtConnection> known = InboundConnection("CN=Alice");
        fx.Connected.Publish(known.Object);

        fx.Disconnected.Publish(new MsmtDisconnection { Connection = InboundConnection("CN=Bob").Object });
        fx.Disconnected.Publish(new MsmtDisconnection { Connection = known.Object });

        PeerConnection connection = Assert.Single(disconnected);
        Assert.Equal("CN=Alice", ((IIpConnectionInfo)connection.Info).CertificateSubject);
    }

    /// <summary>Closing an point drops its live outbound connection and makes requests to it fail without dialing MSMT; other points are unaffected.</summary>
    [Fact]
    public async Task SetClosed_DropsConnectionAndBlocksRequests()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        Acknowledge(connection);
        await RequestVia(fx, target, new byte[] { 1 });

        fx.Transport.SetClosed(target, true);

        connection.Verify(c => c.Dispose(), Times.Once);
        await Assert.ThrowsAsync<IOException>(() => RequestVia(fx, target, new byte[] { 1 }));
        fx.Peer.Verify(p => p.Connect(It.IsAny<MsmtNameTarget>()), Times.Once);

        ConnectionPoint other = new() { IpAddress = "10.9.9.9", Port = 1 };
        Mock<IMsmtConnection> otherConnection = OutboundConnection(other);
        Connects(fx.Peer, other, otherConnection);
        Acknowledge(otherConnection);
        Assert.True(await RequestVia(fx, other, new byte[] { 1 }));
    }

    /// <summary>Reopening an point lets requests to it through again.</summary>
    [Fact]
    public async Task SetClosed_ThenReopened_AllowsRequests()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);
        Acknowledge(connection);
        fx.Transport.SetClosed(target, true);

        fx.Transport.SetClosed(target, false);

        Assert.True(await RequestVia(fx, target, new byte[] { 1 }));
    }

    /// <summary>Reset drops the live outbound connection to the point but leaves it open for new requests.</summary>
    [Fact]
    public async Task Reset_DropsConnectionButStaysOpen()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> first = OutboundConnection(target);
        Acknowledge(first);
        Mock<IMsmtConnection> second = OutboundConnection(target);
        Acknowledge(second);
        fx.Peer.SetupSequence(p => p.Connect(It.IsAny<MsmtNameTarget>())).Returns(first.Object).Returns(second.Object);
        await RequestVia(fx, target, new byte[] { 1 });

        fx.Transport.Reset(target);

        first.Verify(c => c.Dispose(), Times.Once);
        Assert.True(await RequestVia(fx, target, new byte[] { 1 }));
        fx.Peer.Verify(p => p.Connect(It.IsAny<MsmtNameTarget>()), Times.Exactly(2));
        second.Verify(c => c.Request(It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Reset does nothing to a closed point, and does nothing when there is no connection to drop.</summary>
    [Fact]
    public void Reset_WhenClosedOrNotConnected_DoesNothing()
    {
        Fixture fx = Build();
        fx.Transport.Reset(target);
        fx.Transport.SetClosed(target, true);

        fx.Transport.Reset(target);

        fx.Peer.Verify(p => p.Connect(It.IsAny<MsmtNameTarget>()), Times.Never);
    }

    /// <summary>Connect returns the same connection object for a point until that connection is lost, so callers can tell connections apart by reference.</summary>
    [Fact]
    public async Task Connect_SamePointTwice_ReturnsSameConnection()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> connection = OutboundConnection(target);
        Connects(fx.Peer, target, connection);

        PeerConnection first = await fx.Transport.Connect(target);
        PeerConnection second = await fx.Transport.Connect(target);

        Assert.Same(first, second);
    }

    /// <summary>Connect to a closed point fails without dialing.</summary>
    [Fact]
    public async Task Connect_ClosedPoint_ThrowsIOException()
    {
        Fixture fx = Build();
        fx.Transport.SetClosed(target, true);

        await Assert.ThrowsAsync<IOException>(() => fx.Transport.Connect(target));

        fx.Peer.Verify(p => p.Connect(It.IsAny<MsmtNameTarget>()), Times.Never);
    }

    /// <summary>A connection the remote node opened carries this node's requests too, since session connections are bidirectional.</summary>
    [Fact]
    public async Task Request_OverInboundConnection_SendsOnThatConnection()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> msmt = InboundConnection("CN=Alice");
        Acknowledge(msmt);
        PeerConnection? connection = null;
        fx.Transport.Connected.Listen(args => connection = args.Connection);
        fx.Connected.Publish(msmt.Object);

        bool result = await fx.Transport.Request(connection!, new byte[] { 5 }, new PeerSendOptions { Priority = 2 });

        Assert.True(result);
        msmt.Verify(c => c.Request(
            It.Is<IMemoryOwner<byte>>(payload => payload.Memory.ToArray().SequenceEqual(new byte[] { 5 })),
            It.Is<MsmtSendOptions>(o => o.Priority == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A request over a connection that has been lost fails with an IOException.</summary>
    [Fact]
    public async Task Request_OverLostConnection_ThrowsIOException()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> msmt = InboundConnection("CN=Alice");
        Acknowledge(msmt);
        PeerConnection? connection = null;
        fx.Transport.Connected.Listen(args => connection = args.Connection);
        fx.Connected.Publish(msmt.Object);
        fx.Disconnected.Publish(new MsmtDisconnection { Connection = msmt.Object });

        await Assert.ThrowsAsync<IOException>(() => fx.Transport.Request(connection!, new byte[] { 1 }));
    }

    /// <summary>Every common name in the remote certificate's subject is reported, in order.</summary>
    [Fact]
    public void Connected_Inbound_ReportsEveryCommonName()
    {
        Fixture fx = Build();
        PeerConnection? connection = null;
        fx.Transport.Connected.Listen(args => connection = args.Connection);

        fx.Connected.Publish(InboundConnection("CN=Alice, O=Org, CN=Alias").Object);

        Assert.Equal(["Alice", "Alias"], ((IIpConnectionInfo)connection!.Info).CertificateNames);
    }

    private sealed class UnownedMemory(ReadOnlyMemory<byte> data) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = data.ToArray();
        public void Dispose() { }
    }
}
