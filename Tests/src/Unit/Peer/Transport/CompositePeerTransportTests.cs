namespace BlueHeighliner.Comlink.Tests.Unit.Peer.Transport;

/// <summary>Unit tests for <see cref="CompositePeerTransport"/> and <see cref="PeerTransportFactory"/>.</summary>
public sealed class CompositePeerTransportTests
{
    private static readonly UserEndpoint ip = new() { IpAddress = "10.0.0.1", Port = 5 };
    private static readonly UserEndpoint serial = new() { SerialPort = "SL0" };

    private sealed record Part(Mock<IPeerTransport> Transport, TestObservable<PeerReceivedEventArgs> Received, TestObservable<PeerConnectionEventArgs> Connected, TestObservable<PeerConnectionEventArgs> Disconnected);

    private static Part BuildPart()
    {
        Mock<IPeerTransport> transport = new();
        TestObservable<PeerReceivedEventArgs> received = new();
        TestObservable<PeerConnectionEventArgs> connected = new();
        TestObservable<PeerConnectionEventArgs> disconnected = new();
        transport.SetupGet(t => t.Received).Returns(received);
        transport.SetupGet(t => t.Connected).Returns(connected);
        transport.SetupGet(t => t.Disconnected).Returns(disconnected);
        transport.Setup(t => t.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new Part(transport, received, connected, disconnected);
    }

    /// <summary>A request to a serial endpoint goes to the serial transport and one to an IP endpoint goes to the IP transport.</summary>
    [Fact]
    public async Task Request_RoutesByEndpointKind()
    {
        Part ipPart = BuildPart();
        Part serialPart = BuildPart();
        CompositePeerTransport composite = new(ipPart.Transport.Object, serialPart.Transport.Object);

        await composite.Request(ip, new byte[] { 1 });
        await composite.Request(serial, new byte[] { 2 });

        ipPart.Transport.Verify(t => t.Request(ip, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        ipPart.Transport.Verify(t => t.Request(serial, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        serialPart.Transport.Verify(t => t.Request(serial, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        serialPart.Transport.Verify(t => t.Request(ip, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>With no IP transport, IP requests fail with IOException while serial keeps working.</summary>
    [Fact]
    public async Task Request_NoIpTransport_IpFailsSerialWorks()
    {
        Part serialPart = BuildPart();
        CompositePeerTransport composite = new(null, serialPart.Transport.Object);

        await Assert.ThrowsAsync<IOException>(() => composite.Request(ip, new byte[] { 1 }));
        Assert.True(await composite.Request(serial, new byte[] { 1 }));
    }

    /// <summary>Received, Connected, and Disconnected from both transports are merged.</summary>
    [Fact]
    public void Events_AreMergedFromBothTransports()
    {
        Part ipPart = BuildPart();
        Part serialPart = BuildPart();
        CompositePeerTransport composite = new(ipPart.Transport.Object, serialPart.Transport.Object);
        List<string> log = [];
        composite.Received.Listen(_ => log.Add("received"));
        composite.Connected.Listen(_ => log.Add("connected"));
        composite.Disconnected.Listen(_ => log.Add("disconnected"));
        PeerConnection connection = new(null, true, null, () => { });

        foreach (Part part in new[] { ipPart, serialPart })
        {
            part.Received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = new byte[] { 1 } });
            part.Connected.Publish(new PeerConnectionEventArgs { Connection = connection });
            part.Disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });
        }

        Assert.Equal(["received", "connected", "disconnected", "received", "connected", "disconnected"], log);
    }

    /// <summary>StartListener reaches only the IP transport, and is harmless when there is none.</summary>
    [Fact]
    public void StartListener_OnlyReachesIpTransport()
    {
        Part ipPart = BuildPart();
        Part serialPart = BuildPart();

        new CompositePeerTransport(ipPart.Transport.Object, serialPart.Transport.Object).StartListener(50021);
        new CompositePeerTransport(null, serialPart.Transport.Object).StartListener(50021);

        ipPart.Transport.Verify(t => t.StartListener(50021), Times.Once);
        serialPart.Transport.Verify(t => t.StartListener(It.IsAny<int>()), Times.Never);
    }

    /// <summary>Open goes to the transport the endpoint belongs to, and nowhere when that is the missing IP transport.</summary>
    [Fact]
    public void Open_RoutesByEndpointKind()
    {
        Part ipPart = BuildPart();
        Part serialPart = BuildPart();
        CompositePeerTransport composite = new(ipPart.Transport.Object, serialPart.Transport.Object);

        composite.Open(serial);
        composite.Open(ip);
        new CompositePeerTransport(null, serialPart.Transport.Object).Open(ip);

        serialPart.Transport.Verify(t => t.Open(serial), Times.Once);
        serialPart.Transport.Verify(t => t.Open(ip), Times.Never);
        ipPart.Transport.Verify(t => t.Open(ip), Times.Once);
    }

    /// <summary>Disposing the composite disposes both transports.</summary>
    [Fact]
    public async Task DisposeAsync_DisposesBoth()
    {
        Part ipPart = BuildPart();
        Part serialPart = BuildPart();

        await new CompositePeerTransport(ipPart.Transport.Object, serialPart.Transport.Object).DisposeAsync();

        ipPart.Transport.Verify(t => t.DisposeAsync(), Times.Once);
        serialPart.Transport.Verify(t => t.DisposeAsync(), Times.Once);
    }

    /// <summary>With an identity certificate available the factory builds an IP transport as well as the serial one.</summary>
    [Fact]
    public async Task Factory_WithConnectionOptions_BuildsIpTransport()
    {
        Mock<IMsmtPeer> peer = new();
        peer.SetupGet(p => p.Connected).Returns(new TestObservable<MsmtConnectedEventArgs>());
        peer.SetupGet(p => p.Disconnected).Returns(new TestObservable<MsmtDisconnectedEventArgs>());
        peer.SetupGet(p => p.Received).Returns(new TestObservable<MsmtReceivedEventArgs>());
        peer.SetupGet(p => p.PackageChanged).Returns(new TestObservable<MsmtPackageChangedEventArgs>());
        peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MsmtResponse { Success = true, Payload = Mock.Of<IMemoryOwner<byte>>() });
        Mock<IMsmtPeerFactory> msmtFactory = new();
        msmtFactory.Setup(f => f.Create(It.IsAny<MsmtOptions>())).Returns(peer.Object);
        (X509Certificate2 identity, _, X509Certificate2Collection authorities) = TestMsmtCertificates.Create();
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ConnectionOptions).Returns(new MsmtOptions { Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = authorities } });
        PeerTransportFactory factory = new(msmtFactory.Object, Mock.Of<IMicroGatePeerFactory>(), controller.Object, LoggerFactory.Create(_ => { }));

        await using IPeerTransport transport = factory.Create();
        bool ok = await transport.Request(ip, new byte[] { 1 });

        Assert.True(ok);
        msmtFactory.Verify(f => f.Create(It.IsAny<MsmtOptions>()), Times.Once);
    }

    /// <summary>With no identity certificate (a node that only uses serial) the factory still builds a transport, leaving IP unavailable.</summary>
    [Fact]
    public async Task Factory_WithoutConnectionOptions_StillBuildsSerialOnlyTransport()
    {
        Mock<IMsmtPeerFactory> msmtFactory = new();
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ConnectionOptions).Throws(new InvalidOperationException("no current user"));
        PeerTransportFactory factory = new(msmtFactory.Object, Mock.Of<IMicroGatePeerFactory>(), controller.Object, LoggerFactory.Create(_ => { }));

        await using IPeerTransport transport = factory.Create();

        msmtFactory.Verify(f => f.Create(It.IsAny<MsmtOptions>()), Times.Never);
        await Assert.ThrowsAsync<IOException>(() => transport.Request(ip, new byte[] { 1 }));
    }
}
