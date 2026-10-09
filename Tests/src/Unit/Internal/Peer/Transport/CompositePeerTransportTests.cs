namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer.Transport;

/// <summary>Unit tests for <see cref="CompositePeerTransport"/> and <see cref="PeerTransportFactory"/>.</summary>
public sealed class CompositePeerTransportTests
{
    private static readonly ConnectionPoint ip = new() { IpAddress = "10.0.0.1", Port = 5 };
    private static readonly ConnectionPoint serial = new() { SerialPort = "SL0" };

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
        transport.Setup(t => t.Request(It.IsAny<PeerConnection>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        transport.Setup(t => t.Connect(It.IsAny<ConnectionPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConnectionPoint point, CancellationToken _) => Connection(point));
        return new Part(transport, received, connected, disconnected);
    }

    private static PeerConnection Connection(ConnectionPoint point)
        => new(point, point.IsSerial ? new SerialConnectionInfo() : new IpConnectionInfo(), () => { });

    /// <summary>A connect to a serial point goes to the serial transport and one to an IP point goes to the IP transport.</summary>
    [Fact]
    public async Task Connect_RoutesByPointKind()
    {
        Part ipPart = BuildPart();
        Part serialPart = BuildPart();
        CompositePeerTransport composite = new(ipPart.Transport.Object, serialPart.Transport.Object);

        await composite.Connect(ip);
        await composite.Connect(serial);

        ipPart.Transport.Verify(t => t.Connect(ip, It.IsAny<CancellationToken>()), Times.Once);
        ipPart.Transport.Verify(t => t.Connect(serial, It.IsAny<CancellationToken>()), Times.Never);
        serialPart.Transport.Verify(t => t.Connect(serial, It.IsAny<CancellationToken>()), Times.Once);
        serialPart.Transport.Verify(t => t.Connect(ip, It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A request over a serial connection goes to the serial transport and one over an IP connection goes to the IP transport.</summary>
    [Fact]
    public async Task Request_RoutesByConnectionKind()
    {
        Part ipPart = BuildPart();
        Part serialPart = BuildPart();
        CompositePeerTransport composite = new(ipPart.Transport.Object, serialPart.Transport.Object);
        PeerConnection ipConnection = Connection(ip);
        PeerConnection serialConnection = Connection(serial);
        PeerConnection inbound = new(null, new IpConnectionInfo { IsInbound = true }, () => { });

        await composite.Request(ipConnection, new byte[] { 1 });
        await composite.Request(serialConnection, new byte[] { 2 });
        await composite.Request(inbound, new byte[] { 3 });

        ipPart.Transport.Verify(t => t.Request(ipConnection, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        ipPart.Transport.Verify(t => t.Request(inbound, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        ipPart.Transport.Verify(t => t.Request(serialConnection, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        serialPart.Transport.Verify(t => t.Request(serialConnection, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        serialPart.Transport.Verify(t => t.Request(ipConnection, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>With no IP transport, IP connects and requests fail with IOException while serial keeps working.</summary>
    [Fact]
    public async Task NoIpTransport_IpFailsSerialWorks()
    {
        Part serialPart = BuildPart();
        CompositePeerTransport composite = new(null, serialPart.Transport.Object);

        await Assert.ThrowsAsync<IOException>(() => composite.Connect(ip));
        await Assert.ThrowsAsync<IOException>(() => composite.Request(Connection(ip), new byte[] { 1 }));
        Assert.True(await composite.Request(await composite.Connect(serial), new byte[] { 1 }));
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
        PeerConnection connection = new(null, new IpConnectionInfo { IsInbound = true }, () => { });

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

    /// <summary>SetClosed and Reset reach the transport the point belongs to, and are harmless with no IP transport.</summary>
    [Fact]
    public void SetClosedAndReset_RouteByPointKind()
    {
        Part ipPart = BuildPart();
        Part serialPart = BuildPart();
        CompositePeerTransport composite = new(ipPart.Transport.Object, serialPart.Transport.Object);

        composite.SetClosed(ip, true);
        composite.Reset(ip);
        composite.SetClosed(serial, false);
        composite.Reset(serial);
        new CompositePeerTransport(null, serialPart.Transport.Object).SetClosed(ip, true);
        new CompositePeerTransport(null, serialPart.Transport.Object).Reset(ip);

        ipPart.Transport.Verify(t => t.SetClosed(ip, true), Times.Once);
        ipPart.Transport.Verify(t => t.Reset(ip), Times.Once);
        serialPart.Transport.Verify(t => t.SetClosed(serial, false), Times.Once);
        serialPart.Transport.Verify(t => t.Reset(serial), Times.Once);
        serialPart.Transport.Verify(t => t.SetClosed(ip, It.IsAny<bool>()), Times.Never);
        serialPart.Transport.Verify(t => t.Reset(ip), Times.Never);
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

    private static MsmtSessionPeerOptions IpOptions()
    {
        (X509Certificate2 identity, _, X509Certificate2Collection authorities) = TestMsmtCertificates.Create();
        return new MsmtSessionPeerOptions { Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = authorities } };
    }

    private static (PeerTransportFactory Factory, Mock<IMsmtSessionPeer.IFactory> MsmtFactory, List<byte[]> Sent) BuildIpFactory(IEngineController controller)
    {
        Mock<IMsmtSessionPeer> peer = new();
        peer.SetupGet(p => p.Connected).Returns(new TestObservable<IMsmtConnection>());
        peer.SetupGet(p => p.Disconnected).Returns(new TestObservable<MsmtDisconnection>());
        peer.SetupGet(p => p.PackageChanged).Returns(new TestObservable<MsmtPackageChange>());
        peer.SetupProperty(p => p.Receiver);
        List<byte[]> sent = [];
        Mock<IMsmtConnection> connection = new();
        connection.SetupGet(c => c.Direction).Returns(MsmtConnectionDirection.Outgoing);
        connection.SetupGet(c => c.Remote).Returns(new MsmtTarget { Host = ip.IpAddress, Port = ip.Port });
        connection.SetupGet(c => c.Status).Returns(MsmtConnectionStatus.Connected);
        connection.SetupGet(c => c.Identity).Returns(new MsmtIdentity { Subject = "CN=Remote", Issuer = string.Empty, SerialNumber = string.Empty, Thumbprint = string.Empty });
        connection.Setup(c => c.Wait(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        connection.Setup(c => c.Request(It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IMemoryOwner<byte>, MsmtSendOptions?, CancellationToken>((payload, _, _) => sent.Add(payload.Memory.ToArray()))
            .ReturnsAsync(new MsmtResponse { Success = true, Payload = Mock.Of<IMemoryOwner<byte>>() });
        peer.Setup(p => p.Connect(It.IsAny<MsmtNameTarget>())).Returns(connection.Object);
        Mock<IMsmtSessionPeer.IFactory> msmtFactory = new();
        msmtFactory.Setup(f => f.Create(It.IsAny<MsmtSessionPeerOptions>())).Returns(peer.Object);
        return (new PeerTransportFactory(msmtFactory.Object, Mock.Of<IHdlcPeerFactory>(), controller, LoggerFactory.Create(_ => { }), Mock.Of<ILogSettings>()), msmtFactory, sent);
    }

    /// <summary>With an identity certificate available the factory builds an IP transport as well as the serial one.</summary>
    [Fact]
    public async Task Factory_WithConnectionOptions_BuildsIpTransport()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ConnectionOptions).Returns(IpOptions());
        (PeerTransportFactory factory, Mock<IMsmtSessionPeer.IFactory> msmtFactory, _) = BuildIpFactory(controller.Object);

        await using IPeerTransport transport = factory.Create();
        bool ok = await transport.Request(await transport.Connect(ip), new byte[] { 1 });

        Assert.True(ok);
        msmtFactory.Verify(f => f.Create(It.IsAny<MsmtSessionPeerOptions>()), Times.Once);
    }

    /// <summary>With no packet type (the default) full payloads are sent as they are.</summary>
    [Fact]
    public async Task Factory_WithoutPacketType_SendsFullPayloads()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ConnectionOptions).Returns(IpOptions());
        (PeerTransportFactory factory, _, List<byte[]> sent) = BuildIpFactory(controller.Object);
        await using IPeerTransport transport = factory.Create();

        await transport.Request(await transport.Connect(ip), new byte[] { 1, 2, 3 });

        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(sent));
    }

    /// <summary>With a packet type payloads are sent as packets, so what goes on the wire is not the payload itself.</summary>
    [Fact]
    public async Task Factory_WithPacketType_SendsPackets()
    {
        Mock<TestPacketEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ConnectionOptions).Returns(IpOptions());
        (PeerTransportFactory factory, _, List<byte[]> sent) = BuildIpFactory(controller.Object);
        await using IPeerTransport transport = factory.Create();

        await transport.Request(await transport.Connect(ip), new byte[] { 1, 2, 3 }, new PeerSendOptions { Frame = new TestFrame() });

        byte[] packet = Assert.Single(sent);
        Assert.NotEqual(new byte[] { 1, 2, 3 }, packet);
        Assert.True(packet.Length > 3);
    }

    /// <summary>An initial packet with no packets configured stops the transport being created rather than failing every connection later.</summary>
    [Fact]
    public void Factory_InitialPacketWithoutPackets_Throws()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.PacketHandshakeProcessor).Returns(Mock.Of<IHandshakeHandler>());
        controller.Setup(c => c.ConnectionOptions).Throws(new InvalidOperationException("no current user"));
        PeerTransportFactory factory = new(Mock.Of<IMsmtSessionPeer.IFactory>(), Mock.Of<IHdlcPeerFactory>(), controller.Object, LoggerFactory.Create(_ => { }), Mock.Of<ILogSettings>());

        Assert.Throws<InvalidEngineConfigurationException>(() => factory.Create());
    }

    /// <summary>The factory always produces an outermost transport that carries out the handshake and identifies its connections.</summary>
    [Fact]
    public async Task Factory_AlwaysBuildsHandshakeTransport()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ConnectionOptions).Throws(new InvalidOperationException("no current user"));
        PeerTransportFactory factory = new(Mock.Of<IMsmtSessionPeer.IFactory>(), Mock.Of<IHdlcPeerFactory>(), controller.Object, LoggerFactory.Create(_ => { }), Mock.Of<ILogSettings>());

        await using IPeerTransport transport = factory.Create();

        Assert.IsType<HandshakePeerTransport>(transport);
    }

    /// <summary>A payload size below 1, or a window below 1, stops the transport being created rather than failing every send later.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(16384, 0)]
    public void Factory_InvalidPacketization_Throws(int payloadSize, int window)
    {
        Mock<TestPacketEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.MaxPayloadSize).Returns(payloadSize);
        controller.Setup(c => c.PacketWindow).Returns(window);
        PeerTransportFactory factory = new(Mock.Of<IMsmtSessionPeer.IFactory>(), Mock.Of<IHdlcPeerFactory>(), controller.Object, LoggerFactory.Create(_ => { }), Mock.Of<ILogSettings>());

        Assert.Throws<InvalidEngineConfigurationException>(() => factory.Create());
    }

    /// <summary>The payload size and window are not looked at while there is no packet type, so a leftover invalid value does no harm.</summary>
    [Fact]
    public async Task Factory_InvalidPacketizationWithoutPacketType_IsIgnored()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.MaxPayloadSize).Returns(1);
        controller.Setup(c => c.PacketWindow).Returns(0);
        controller.Setup(c => c.ConnectionOptions).Throws(new InvalidOperationException("no current user"));
        PeerTransportFactory factory = new(Mock.Of<IMsmtSessionPeer.IFactory>(), Mock.Of<IHdlcPeerFactory>(), controller.Object, LoggerFactory.Create(_ => { }), Mock.Of<ILogSettings>());

        await using IPeerTransport transport = factory.Create();

        Assert.IsType<HandshakePeerTransport>(transport);
    }

    /// <summary>With no identity certificate (a node that only uses serial) the factory still builds a transport, leaving IP unavailable.</summary>
    [Fact]
    public async Task Factory_WithoutConnectionOptions_StillBuildsSerialOnlyTransport()
    {
        Mock<IMsmtSessionPeer.IFactory> msmtFactory = new();
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ConnectionOptions).Throws(new InvalidOperationException("no current user"));
        PeerTransportFactory factory = new(msmtFactory.Object, Mock.Of<IHdlcPeerFactory>(), controller.Object, LoggerFactory.Create(_ => { }), Mock.Of<ILogSettings>());

        await using IPeerTransport transport = factory.Create();

        msmtFactory.Verify(f => f.Create(It.IsAny<MsmtSessionPeerOptions>()), Times.Never);
        await Assert.ThrowsAsync<IOException>(() => transport.Connect(ip));
    }
}
