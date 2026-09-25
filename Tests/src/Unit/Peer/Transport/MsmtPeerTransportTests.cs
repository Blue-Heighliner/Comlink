namespace BlueHeighliner.Comlink.Tests.Unit.Peer.Transport;

/// <summary>Unit tests for <see cref="MsmtPeerTransport"/>, the IP half of the peer transport.</summary>
public sealed class MsmtPeerTransportTests
{
    private static readonly UserEndpoint target = new() { IpAddress = "10.0.0.5", Port = 4000 };

    private sealed record Fixture(
        MsmtPeerTransport Transport,
        Mock<IMsmtPeer> Peer,
        TestObservable<MsmtConnectedEventArgs> Connected,
        TestObservable<MsmtDisconnectedEventArgs> Disconnected,
        TestObservable<MsmtReceivedEventArgs> Received,
        TestObservable<MsmtPackageChangedEventArgs> PackageChanged);

    private static Fixture Build()
    {
        Mock<IMsmtPeer> peer = new();
        TestObservable<MsmtConnectedEventArgs> connected = new();
        TestObservable<MsmtDisconnectedEventArgs> disconnected = new();
        TestObservable<MsmtReceivedEventArgs> received = new();
        TestObservable<MsmtPackageChangedEventArgs> packageChanged = new();
        peer.SetupGet(p => p.Connected).Returns(connected);
        peer.SetupGet(p => p.Disconnected).Returns(disconnected);
        peer.SetupGet(p => p.Received).Returns(received);
        peer.SetupGet(p => p.PackageChanged).Returns(packageChanged);
        return new Fixture(new MsmtPeerTransport(peer.Object), peer, connected, disconnected, received, packageChanged);
    }

    private static void Acknowledge(Mock<IMsmtPeer> peer, bool success = true)
        => peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MsmtResponse { Success = success, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });

    private static Mock<IMsmtConnection> OutboundConnection(UserEndpoint endpoint)
    {
        Mock<IMsmtConnection> connection = new();
        connection.SetupGet(c => c.Target).Returns(new MsmtTarget { Host = endpoint.IpAddress, Port = endpoint.Port });
        connection.SetupGet(c => c.Sender).Returns(Mock.Of<IMsmtLink>());
        return connection;
    }

    private static Mock<IMsmtConnection> InboundConnection(string subject)
    {
        Mock<IMsmtConnection> connection = new();
        connection.SetupGet(c => c.Target).Returns(new MsmtTarget { Host = "0.0.0.0", Port = 0 });
        connection.SetupGet(c => c.Sender).Returns((IMsmtLink?)null);
        connection.SetupGet(c => c.Identity).Returns(new MsmtIdentity { Subject = subject, Issuer = string.Empty, SerialNumber = string.Empty, Thumbprint = string.Empty });
        return connection;
    }

    /// <summary>A request is sent to the endpoint's host and port with the payload, and returns the remote acknowledgement.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Request_SendsToTargetAndReturnsAcknowledgement(bool success)
    {
        Fixture fx = Build();
        Acknowledge(fx.Peer, success);

        bool result = await fx.Transport.Request(target, new byte[] { 1, 2 }, new PeerSendOptions { Priority = 4 });

        Assert.Equal(success, result);
        fx.Peer.Verify(p => p.Request(
            It.Is<MsmtNameTarget>(t => t.Host == "10.0.0.5" && t.Port == 4000),
            It.Is<IMemoryOwner<byte>>(payload => payload.Memory.ToArray().SequenceEqual(new byte[] { 1, 2 })),
            It.Is<MsmtSendOptions>(o => o.Priority == 4),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A failed send propagates the exception so the caller can treat it as undelivered.</summary>
    [Fact]
    public async Task Request_PeerThrows_Propagates()
    {
        Fixture fx = Build();
        fx.Peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SocketException((int)SocketError.ConnectionRefused));

        await Assert.ThrowsAsync<SocketException>(() => fx.Transport.Request(target, new byte[] { 1 }));
    }

    /// <summary>The Transmitted callback fires when MSMT reports the package as awaiting acknowledgement, and not for other statuses.</summary>
    [Fact]
    public async Task Request_TransmittedCallback_FiresOnPendingAcknowledgementOnly()
    {
        Fixture fx = Build();
        int transmitted = 0;
        TaskCompletionSource<MsmtResponse> completion = new();
        fx.Peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .Callback<MsmtNameTarget, IMemoryOwner<byte>, MsmtSendOptions?, CancellationToken>((_, _, options, _) =>
            {
                foreach (MsmtSendStatus status in new[] { MsmtSendStatus.Queued, MsmtSendStatus.Transmitting, MsmtSendStatus.PendingAcknowledgement, MsmtSendStatus.Completed })
                {
                    fx.PackageChanged.Publish(new MsmtPackageChangedEventArgs
                    {
                        Link = Mock.Of<IMsmtLink>(),
                        Package = Mock.Of<IMsmtPackage>(pk => pk.Tag == options!.Tag),
                        Status = status
                    });
                }
            })
            .Returns(completion.Task);

        Task<bool> request = fx.Transport.Request(target, new byte[] { 1 }, new PeerSendOptions { Transmitted = () => transmitted++ });
        completion.SetResult(new MsmtResponse { Success = true, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });
        await request;

        Assert.Equal(1, transmitted);
    }

    /// <summary>A send with no Transmitted callback carries no tag, so MSMT does no per-package progress tracking for it.</summary>
    [Fact]
    public async Task Request_WithoutTransmittedCallback_HasNoTag()
    {
        Fixture fx = Build();
        Acknowledge(fx.Peer);

        await fx.Transport.Request(target, new byte[] { 1 });

        fx.Peer.Verify(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.Is<MsmtSendOptions>(o => o.Tag == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A connection this node dialed is published as outbound with the endpoint it dialed.</summary>
    [Fact]
    public void Connected_Outbound_CarriesDialedEndpoint()
    {
        Fixture fx = Build();
        PeerConnection? connection = null;
        fx.Transport.Connected.Listen(args => connection = args.Connection);

        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = OutboundConnection(target).Object });

        Assert.NotNull(connection);
        Assert.False(connection.IsInbound);
        Assert.Equal(target, connection.Endpoint);
    }

    /// <summary>A connection a remote node opened to this node is published as inbound, with its certificate subject and no endpoint.</summary>
    [Fact]
    public void Connected_Inbound_CarriesCertificateSubject()
    {
        Fixture fx = Build();
        PeerConnection? connection = null;
        fx.Transport.Connected.Listen(args => connection = args.Connection);

        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = InboundConnection("CN=Alice").Object });

        Assert.NotNull(connection);
        Assert.True(connection.IsInbound);
        Assert.Null(connection.Endpoint);
        Assert.Equal("CN=Alice", connection.IdentitySubject);
    }

    /// <summary>Dropping the published connection drops the underlying MSMT connection.</summary>
    [Fact]
    public void Connection_Drop_DropsUnderlyingConnection()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> msmt = InboundConnection("CN=Alice");
        PeerConnection? connection = null;
        fx.Transport.Connected.Listen(args => connection = args.Connection);
        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = msmt.Object });

        connection!.Drop();

        msmt.Verify(c => c.Drop(), Times.Once);
    }

    /// <summary>A received message is published with a copy of its payload, on the same connection object that was published as connected.</summary>
    [Fact]
    public void Received_PublishesPayloadCopyOnSameConnection()
    {
        Fixture fx = Build();
        Mock<IMsmtConnection> msmt = InboundConnection("CN=Alice");
        PeerConnection? connected = null;
        PeerReceivedEventArgs? received = null;
        fx.Transport.Connected.Listen(args => connected = args.Connection);
        fx.Transport.Received.Listen(args => received = args);
        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = msmt.Object });

        fx.Received.Publish(new MsmtReceivedEventArgs
        {
            Link = Mock.Of<IMsmtLink>(l => l.Connection == msmt.Object),
            Payload = new UnownedMemory(new byte[] { 7, 8, 9 }),
            Responder = Mock.Of<IMsmtResponder>(),
            IsResponseRequested = false
        });

        Assert.NotNull(received);
        Assert.Same(connected, received.Connection);
        Assert.Equal(new byte[] { 7, 8, 9 }, received.Payload.ToArray());
    }

    /// <summary>A message on a connection never seen connected is still published, as inbound.</summary>
    [Fact]
    public void Received_UnknownConnection_PublishedAsInbound()
    {
        Fixture fx = Build();
        PeerReceivedEventArgs? received = null;
        fx.Transport.Received.Listen(args => received = args);

        fx.Received.Publish(new MsmtReceivedEventArgs
        {
            Link = Mock.Of<IMsmtLink>(l => l.Connection == InboundConnection("CN=Bob").Object),
            Payload = new UnownedMemory(new byte[] { 1 }),
            Responder = Mock.Of<IMsmtResponder>(),
            IsResponseRequested = false
        });

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
        Mock<IMsmtConnection> known = OutboundConnection(target);
        fx.Connected.Publish(new MsmtConnectedEventArgs { Connection = known.Object });

        fx.Disconnected.Publish(new MsmtDisconnectedEventArgs { Connection = OutboundConnection(new UserEndpoint { IpAddress = "1.1.1.1", Port = 1 }).Object });
        fx.Disconnected.Publish(new MsmtDisconnectedEventArgs { Connection = known.Object });

        PeerConnection connection = Assert.Single(disconnected);
        Assert.Equal(target, connection.Endpoint);
    }

    /// <summary>StartListener forwards the port, and Open is a no-op since IP connections are dialed on demand.</summary>
    [Fact]
    public void StartListener_ForwardsPort_AndOpenDoesNothing()
    {
        Fixture fx = Build();

        fx.Transport.StartListener(50021);
        fx.Transport.Open(target);

        fx.Peer.Verify(p => p.StartListener(50021, "0.0.0.0"), Times.Once);
        fx.Peer.Verify(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Disposing the transport disposes the MSMT peer.</summary>
    [Fact]
    public async Task DisposeAsync_DisposesPeer()
    {
        Fixture fx = Build();

        await fx.Transport.DisposeAsync();

        fx.Peer.Verify(p => p.DisposeAsync(), Times.Once);
    }

    private sealed class UnownedMemory(ReadOnlyMemory<byte> data) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = data.ToArray();
        public void Dispose() { }
    }
}
