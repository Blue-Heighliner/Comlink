namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer.Transport;

/// <summary>Unit tests for <see cref="IdentifyingPeerTransport"/>, over an in-memory pair of transports.</summary>
public sealed class IdentifyingPeerTransportTests
{
    private static readonly ILogger logger = LoggerFactory.Create(_ => { }).CreateLogger("test");
    private static readonly ConnectionPoint point = new() { IpAddress = "10.0.0.5", Port = 4000 };
    private static readonly ConnectionPoint serialPoint = new() { SerialPort = "SL0" };
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    private sealed class End(IdentifyingPeerTransport transport, LoopbackPeerTransport raw)
    {
        private readonly Lock gate = new();
        private readonly List<PeerConnection> connected = [];
        private readonly List<PeerConnection> disconnected = [];
        private readonly List<byte[]> received = [];

        public IdentifyingPeerTransport Transport { get; } = transport;
        public LoopbackPeerTransport Raw { get; } = raw;
        public IReadOnlyList<PeerConnection> Connected { get { lock (gate) { return [.. connected]; } } }
        public IReadOnlyList<PeerConnection> Disconnected { get { lock (gate) { return [.. disconnected]; } } }
        public IReadOnlyList<byte[]> Received { get { lock (gate) { return [.. received]; } } }

        public void Watch()
        {
            Transport.Connected.Listen(args => { lock (gate) { connected.Add(args.Connection); } });
            Transport.Disconnected.Listen(args => { lock (gate) { disconnected.Add(args.Connection); } });
            Transport.Received.Listen(args => { lock (gate) { received.Add(args.Payload.ToArray()); } });
        }
    }

    private static Mock<TestEngineController> Controller(params string[] users)
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.Users).Returns(users);
        controller.Setup(c => c.GetCertificateName(It.IsAny<string>())).Returns((string name) => $"cert-{name}");
        return controller;
    }

    private static Mock<TestEngineController> HandshakeController(bool respond = true)
    {
        Mock<TestEngineController> controller = Controller();
        controller.Setup(c => c.ConnectionMessageType).Returns(typeof(TestHello));
        if (respond) { controller.Setup(c => c.ConnectionResponseType).Returns(typeof(TestWelcome)); }
        return controller;
    }

    private static (End A, End B) Pair(IEngineController a, IEngineController b, IReadOnlyList<string>? aNames = null, IReadOnlyList<string>? bNames = null, bool serial = false, TimeSpan? handshakeTimeout = null)
    {
        (LoopbackPeerTransport rawA, LoopbackPeerTransport rawB) = LoopbackPeerTransport.CreatePair(aNames, bNames, serial);
        End endA = new(new IdentifyingPeerTransport(rawA, a, logger, handshakeTimeout), rawA);
        End endB = new(new IdentifyingPeerTransport(rawB, b, logger, handshakeTimeout), rawB);
        endA.Watch();
        endB.Watch();
        return (endA, endB);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(5);
        }
    }

    /// <summary>Without a connection message, an IP connection is the user whose certificate name appears in the remote certificate, on both ends, with that user's data.</summary>
    [Fact]
    public async Task Connect_IdentifiesByCertificateName()
    {
        Mock<TestEngineController> a = Controller("ALICE", "BOB");
        Mock<TestEngineController> b = Controller("ALICE", "BOB");
        a.Setup(c => c.GetUserData("BOB")).Returns(new Dictionary<string, string> { ["desk"] = "4" });
        (End endA, End endB) = Pair(a.Object, b.Object, aNames: ["cert-ALICE"], bNames: ["cert-BOB"]);

        PeerConnection connection = await endA.Transport.Connect(point);

        Assert.Equal("BOB", connection.User!.Name);
        Assert.Equal("4", connection.User.Data["desk"]);
        await WaitUntil(() => endB.Connected.Count == 1);
        Assert.Equal("ALICE", endB.Connected[0].User!.Name);
        Assert.Same(connection, Assert.Single(endA.Connected));
    }

    /// <summary>A certificate name no user matches names a user after itself, since the trusted authority already vouched for the certificate.</summary>
    [Fact]
    public async Task Connect_UnknownCertificateName_UsesTheFirstCertificateName()
    {
        (End endA, _) = Pair(Controller("ALICE").Object, Controller("ALICE").Object, bNames: ["Stranger", "Other"]);

        PeerConnection connection = await endA.Transport.Connect(point);

        Assert.Equal("Stranger", connection.User!.Name);
    }

    /// <summary>Servers and their child clients count as known users, so a certificate name is matched to one of them even when it is not in the user list.</summary>
    [Fact]
    public async Task Connect_MatchesTopologyUsers()
    {
        Mock<TestEngineController> a = Controller();
        a.Setup(c => c.Servers).Returns(new Dictionary<string, ServerUserConfig> { ["Server1"] = new ServerUserConfig { ChildClients = ["Client1"] } });
        (End endA, _) = Pair(a.Object, Controller().Object, bNames: ["cert-Client1"]);

        PeerConnection connection = await endA.Transport.Connect(point);

        Assert.Equal("Client1", connection.User!.Name);
    }

    /// <summary>A connection with no certificate names cannot be identified by default and is dropped, which fails the connect.</summary>
    [Fact]
    public async Task Connect_NoCertificateNames_IsDroppedAndFails()
    {
        (End endA, End endB) = Pair(Controller().Object, Controller().Object);

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Connect(point));

        Assert.Empty(endA.Connected);
        Assert.Empty(endA.Disconnected);
        await WaitUntil(() => endB.Raw.Delivered.IsEmpty);
    }

    /// <summary>The controller's own identification wins over the engine's, and it is told what is known about the connection.</summary>
    [Fact]
    public async Task Connect_ControllerIdentification_WinsAndSeesConnectionInfo()
    {
        Mock<TestEngineController> a = Controller("BOB");
        ConnectionInfo? seen = null;
        a.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns((ConnectionInfo info) =>
        {
            seen = info;
            return new UserIdentity { Name = "CUSTOM", Data = new Dictionary<string, string> { ["k"] = "v" } };
        });
        (End endA, _) = Pair(a.Object, Controller().Object, bNames: ["cert-BOB"]);

        PeerConnection connection = await endA.Transport.Connect(point);

        Assert.Equal("CUSTOM", connection.User!.Name);
        Assert.Equal("v", connection.User.Data["k"]);
        Assert.NotNull(seen);
        Assert.Equal("10.0.0.5", seen.Host);
        Assert.Equal(4000, seen.Port);
        Assert.False(seen.IsInbound);
        Assert.False(seen.IsSerial);
        Assert.Equal(["cert-BOB"], seen.CertificateNames);
        Assert.Equal("CN=cert-BOB", seen.CertificateSubject);
    }

    /// <summary>A controller whose identification throws does not take the transport down: the connection is dropped.</summary>
    [Fact]
    public async Task Connect_ControllerIdentificationThrows_DropsConnection()
    {
        Mock<TestEngineController> a = Controller("BOB");
        a.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Throws(new InvalidOperationException("boom"));
        (End endA, _) = Pair(a.Object, Controller().Object, bNames: ["cert-BOB"]);

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Connect(point));

        Assert.Empty(endA.Connected);
    }

    /// <summary>A serial connection is named after its port by default, and a controller can map the port and address to a user instead.</summary>
    [Fact]
    public async Task Connect_Serial_DefaultsToPortName_ControllerCanOverride()
    {
        (End plainA, _) = Pair(Controller().Object, Controller().Object, serial: true);
        PeerConnection plain = await plainA.Transport.Connect(serialPoint);
        Assert.Equal("SL0", plain.User!.Name);

        Mock<TestEngineController> mapped = Controller();
        mapped.Setup(c => c.IdentifyConnection(It.Is<ConnectionInfo>(i => i.IsSerial && i.SerialPort == "SL0" && i.SerialAddress == 0xFF))).Returns(new UserIdentity { Name = "CONSOLE-3" });
        (End mappedA, _) = Pair(mapped.Object, Controller().Object, serial: true);
        PeerConnection custom = await mappedA.Transport.Connect(serialPoint);
        Assert.Equal("CONSOLE-3", custom.User!.Name);
    }

    /// <summary>Connected is published only once identification has finished, with the user already set, and Disconnected only for a connection that was published.</summary>
    [Fact]
    public async Task Events_ConnectedCarriesUser_DisconnectedOnlyAfterConnected()
    {
        (End endA, End endB) = Pair(Controller().Object, Controller().Object, aNames: ["Alice"], bNames: ["Bob"]);
        List<UserIdentity?> usersAtConnect = [];
        endA.Transport.Connected.Listen(args => usersAtConnect.Add(args.Connection.User));

        PeerConnection connection = await endA.Transport.Connect(point);
        connection.Drop();

        Assert.Equal("Bob", Assert.Single(usersAtConnect)!.Name);
        await WaitUntil(() => endA.Disconnected.Count == 1 && endB.Disconnected.Count == 1);
        Assert.Same(connection, endA.Disconnected[0]);
    }

    /// <summary>A connection that is lost before its connect returns is not resurrected: it is published once, and requests over it fail.</summary>
    [Fact]
    public async Task Connect_ConnectionLostBeforeItReturns_IsNotPublishedAgain()
    {
        (End endA, _) = Pair(Controller().Object, Controller().Object, bNames: ["Bob"]);

        PeerConnection connection = await endA.Transport.Connect(point);

        await WaitUntil(() => endA.Disconnected.Count == 1);
        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Request(connection, new byte[] { 1 }));
        Assert.Single(endA.Connected);
    }

    /// <summary>A serial link presents the same connection object every time it comes back, and each time is identified afresh.</summary>
    [Fact]
    public async Task Connected_SameConnectionObjectAgain_IsIdentifiedAgain()
    {
        Mock<IPeerTransport> inner = new();
        TestObservable<PeerReceivedEventArgs> received = new();
        TestObservable<PeerConnectionEventArgs> connected = new();
        TestObservable<PeerConnectionEventArgs> disconnected = new();
        inner.SetupGet(t => t.Received).Returns(received);
        inner.SetupGet(t => t.Connected).Returns(connected);
        inner.SetupGet(t => t.Disconnected).Returns(disconnected);
        int identifications = 0;
        Mock<TestEngineController> controller = Controller();
        controller.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns(() => new UserIdentity { Name = $"LINK-{++identifications}" });
        IdentifyingPeerTransport transport = new(inner.Object, controller.Object, logger);
        List<string> users = [];
        transport.Connected.Listen(args => users.Add(args.Connection.User!.Name));
        PeerConnection link = new(serialPoint, new ConnectionInfo { IsSerial = true, SerialPort = "SL0" }, () => { });

        connected.Publish(new PeerConnectionEventArgs { Connection = link });
        disconnected.Publish(new PeerConnectionEventArgs { Connection = link });
        connected.Publish(new PeerConnectionEventArgs { Connection = link });

        Assert.Equal(["LINK-1", "LINK-2"], users);
        await Task.CompletedTask;
    }

    /// <summary>Without a connection message payloads travel unframed, in both directions, over the connection either end opened.</summary>
    [Fact]
    public async Task Request_WithoutConnectionMessage_PassesPayloadsThroughUnchanged()
    {
        (End endA, End endB) = Pair(Controller().Object, Controller().Object, aNames: ["Alice"], bNames: ["Bob"]);
        PeerConnection outbound = await endA.Transport.Connect(point);
        await WaitUntil(() => endB.Connected.Count == 1);

        Assert.True(await endA.Transport.Request(outbound, new byte[] { 1, 2, 3 }));
        Assert.True(await endB.Transport.Request(endB.Connected[0], new byte[] { 9 }));
        Assert.True(await endA.Transport.Request(outbound, ReadOnlyMemory<byte>.Empty));

        await WaitUntil(() => endB.Received.Count == 2 && endA.Received.Count == 1);
        Assert.Equal(new byte[] { 1, 2, 3 }, endB.Received[0]);
        Assert.Empty(endB.Received[1]);
        Assert.Equal(new byte[] { 9 }, endA.Received[0]);
        Assert.Equal(new byte[] { 1, 2, 3 }, endB.Raw.Delivered.First());
    }

    /// <summary>A request over a connection that has been lost fails with an IOException.</summary>
    [Fact]
    public async Task Request_OverLostConnection_ThrowsIOException()
    {
        (End endA, _) = Pair(Controller().Object, Controller().Object, bNames: ["Bob"]);
        PeerConnection connection = await endA.Transport.Connect(point);
        connection.Drop();

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Request(connection, new byte[] { 1 }));
    }

    /// <summary>The message exchange decides the identity on both ends: the opener sends its message, the receiver identifies it from that and answers, and the opener identifies the receiver from the response, data included.</summary>
    [Fact]
    public async Task Handshake_IdentitiesComeFromTheMessageAndTheResponse()
    {
        Mock<TestEngineController> a = HandshakeController();
        a.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "ALICE" });
        a.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns((ConnectionInfo info) =>
        {
            TestWelcome welcome = (TestWelcome)info.ConnectionResponse!;
            return new UserIdentity { Name = welcome.Name, Data = new Dictionary<string, string> { ["station"] = welcome.Station.ToString() } };
        });
        Mock<TestEngineController> b = HandshakeController();
        ConnectionInfo? receiverSaw = null;
        b.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns((ConnectionInfo info) =>
        {
            receiverSaw = info;
            return new UserIdentity { Name = ((TestHello)info.ConnectionMessage!).Name };
        });
        b.Setup(c => c.CreateConnectionResponse(It.IsAny<ConnectionInfo>())).Returns((ConnectionInfo info) => new TestWelcome { Name = "BOB", Station = 4 });
        (End endA, End endB) = Pair(a.Object, b.Object);

        PeerConnection outbound = await endA.Transport.Connect(point);

        Assert.Equal("BOB", outbound.User!.Name);
        Assert.Equal("4", outbound.User.Data["station"]);
        await WaitUntil(() => endB.Connected.Count == 1);
        Assert.Equal("ALICE", endB.Connected[0].User!.Name);
        Assert.NotNull(receiverSaw);
        Assert.True(receiverSaw.IsInbound);
        Assert.Null(receiverSaw.ConnectionResponse);
        Assert.Equal("ALICE", ((TestHello)receiverSaw.ConnectionMessage!).Name);
    }

    /// <summary>Once the exchange is done, payloads flow both ways and arrive as sent, even though they travel framed.</summary>
    [Fact]
    public async Task Handshake_DataFlowsBothWaysAfterwards()
    {
        Mock<TestEngineController> a = HandshakeController();
        a.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "ALICE" });
        a.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns(new UserIdentity { Name = "BOB" });
        Mock<TestEngineController> b = HandshakeController();
        b.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns(new UserIdentity { Name = "ALICE" });
        (End endA, End endB) = Pair(a.Object, b.Object);
        PeerConnection outbound = await endA.Transport.Connect(point);
        await WaitUntil(() => endB.Connected.Count == 1);

        Assert.True(await endA.Transport.Request(outbound, new byte[] { 1, 2, 3 }));
        Assert.True(await endB.Transport.Request(endB.Connected[0], new byte[] { 7 }));
        Assert.True(await endA.Transport.Request(outbound, ReadOnlyMemory<byte>.Empty));

        await WaitUntil(() => endB.Received.Count == 2 && endA.Received.Count == 1);
        Assert.Equal(new byte[] { 1, 2, 3 }, endB.Received[0]);
        Assert.Empty(endB.Received[1]);
        Assert.Equal(new byte[] { 7 }, endA.Received[0]);
        Assert.NotEqual(new byte[] { 1, 2, 3 }, endB.Raw.Delivered.ElementAt(1));
    }

    /// <summary>A payload sent the moment the opener's connection is usable reaches the receiver even if the receiver has not finished establishing yet: it is held, then delivered in order.</summary>
    [Fact]
    public async Task Handshake_DataSentImmediately_IsHeldUntilTheReceiverIsEstablished()
    {
        Mock<TestEngineController> a = HandshakeController();
        a.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "ALICE" });
        a.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns(new UserIdentity { Name = "BOB" });
        Mock<TestEngineController> b = HandshakeController();
        b.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns(() =>
        {
            Thread.Sleep(100);
            return new UserIdentity { Name = "ALICE" };
        });
        (End endA, End endB) = Pair(a.Object, b.Object);

        PeerConnection outbound = await endA.Transport.Connect(point);
        await endA.Transport.Request(outbound, new byte[] { 1 });
        await endA.Transport.Request(outbound, new byte[] { 2 });

        await WaitUntil(() => endB.Received.Count == 2);
        Assert.Equal(new byte[] { 1 }, endB.Received[0]);
        Assert.Equal(new byte[] { 2 }, endB.Received[1]);
        Assert.Single(endB.Connected);
    }

    /// <summary>With no response type the opener is usable as soon as its message has been accepted, and the receiver identifies it from the message alone.</summary>
    [Fact]
    public async Task Handshake_WithoutResponseType_OpenerNeedsNoResponse()
    {
        Mock<TestEngineController> a = HandshakeController(respond: false);
        a.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "ALICE" });
        Mock<TestEngineController> b = HandshakeController(respond: false);
        b.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns((ConnectionInfo info) => new UserIdentity { Name = ((TestHello)info.ConnectionMessage!).Name });
        (End endA, End endB) = Pair(a.Object, b.Object, bNames: ["Bob"]);

        PeerConnection outbound = await endA.Transport.Connect(point);

        Assert.Equal("Bob", outbound.User!.Name);
        await WaitUntil(() => endB.Connected.Count == 1);
        Assert.Equal("ALICE", endB.Connected[0].User!.Name);
        b.Verify(c => c.CreateConnectionResponse(It.IsAny<ConnectionInfo>()), Times.Never);
    }

    /// <summary>A controller that builds no message sends an empty one, and the receiver then sees no message and identifies the connection by the engine's own rule.</summary>
    [Fact]
    public async Task Handshake_NullMessage_IsSentEmpty()
    {
        ConnectionInfo? receiverSaw = null;
        Mock<TestEngineController> a = HandshakeController(respond: false);
        Mock<TestEngineController> b = HandshakeController(respond: false);
        b.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Callback<ConnectionInfo>(info => receiverSaw = info).Returns((UserIdentity?)null);
        (End endA, End endB) = Pair(a.Object, b.Object, aNames: ["Alice"], bNames: ["Bob"]);

        await endA.Transport.Connect(point);

        await WaitUntil(() => endB.Connected.Count == 1);
        Assert.Null(receiverSaw!.ConnectionMessage);
        Assert.Equal("Alice", endB.Connected[0].User!.Name);
    }

    /// <summary>Both ends of a serial link send a message and answer the other's, and each identifies the other from what it received.</summary>
    [Fact]
    public async Task Handshake_Serial_BothEndsExchange()
    {
        Mock<TestEngineController> a = HandshakeController();
        a.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "ALICE" });
        a.Setup(c => c.CreateConnectionResponse(It.IsAny<ConnectionInfo>())).Returns(new TestWelcome { Name = "ALICE", Station = 1 });
        a.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns((ConnectionInfo info) => new UserIdentity { Name = ((TestHello)info.ConnectionMessage!).Name + "/" + ((TestWelcome)info.ConnectionResponse!).Station });
        Mock<TestEngineController> b = HandshakeController();
        b.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "BOB" });
        b.Setup(c => c.CreateConnectionResponse(It.IsAny<ConnectionInfo>())).Returns(new TestWelcome { Name = "BOB", Station = 2 });
        b.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns((ConnectionInfo info) => new UserIdentity { Name = ((TestHello)info.ConnectionMessage!).Name + "/" + ((TestWelcome)info.ConnectionResponse!).Station });
        (End endA, End endB) = Pair(a.Object, b.Object, serial: true);

        PeerConnection link = await endA.Transport.Connect(serialPoint);

        Assert.Equal("BOB/2", link.User!.Name);
        await WaitUntil(() => endB.Connected.Count == 1);
        Assert.Equal("ALICE/1", endB.Connected[0].User!.Name);
    }

    /// <summary>A node that is fully identified before its own response has finished sending keeps the response going and stays connected: establishing must not cancel what the exchange still has in flight.</summary>
    [Fact]
    public async Task Handshake_EstablishedWhileResponseStillSending_StaysConnected()
    {
        Mock<TestEngineController> a = HandshakeController();
        a.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "ALICE" });
        a.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns(new UserIdentity { Name = "BOB" });
        Mock<TestEngineController> b = HandshakeController();
        b.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "BOB" });
        b.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns(new UserIdentity { Name = "ALICE" });
        (End endA, End endB) = Pair(a.Object, b.Object, serial: true);
        endB.Raw.DelayFor = data => data[0] == 3 ? TimeSpan.FromMilliseconds(300) : TimeSpan.Zero;

        PeerConnection link = await endA.Transport.Connect(serialPoint);

        Assert.Equal("BOB", link.User!.Name);
        await WaitUntil(() => endB.Connected.Count == 1);
        Assert.Empty(endB.Disconnected);
        Assert.Empty(endA.Disconnected);
    }

    /// <summary>If the other end never answers the connection message, the connection is dropped once the timeout passes and connecting fails.</summary>
    [Fact]
    public async Task Handshake_NoAnswer_TimesOutAndDrops()
    {
        Mock<TestEngineController> a = HandshakeController();
        a.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "ALICE" });
        (End endA, End endB) = Pair(a.Object, HandshakeController().Object, handshakeTimeout: TimeSpan.FromMilliseconds(150));
        endA.Raw.IsSilent = true;

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Connect(point));

        Assert.Empty(endA.Connected);
        await WaitUntil(() => endB.Raw.Delivered.IsEmpty);
    }

    /// <summary>A connection message of a type other than the configured one is refused and the connection dropped.</summary>
    [Fact]
    public async Task Handshake_WrongMessageType_DropsConnection()
    {
        Mock<TestEngineController> a = HandshakeController();
        a.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Returns(new TestHello { Name = "ALICE" });
        Mock<TestEngineController> b = Controller();
        b.Setup(c => c.ConnectionMessageType).Returns(typeof(TestWelcome));
        (End endA, End endB) = Pair(a.Object, b.Object, handshakeTimeout: TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Connect(point));

        Assert.Empty(endB.Connected);
    }

    /// <summary>A controller whose connection message hook throws has its connection dropped rather than left half open.</summary>
    [Fact]
    public async Task Handshake_MessageHookThrows_DropsConnection()
    {
        Mock<TestEngineController> a = HandshakeController();
        a.Setup(c => c.CreateConnectionMessage(It.IsAny<ConnectionInfo>())).Throws(new InvalidOperationException("boom"));
        (End endA, _) = Pair(a.Object, HandshakeController().Object);

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Connect(point));

        Assert.Empty(endA.Connected);
    }

    /// <summary>A received payload the transport can make nothing of, such as an unframed one from a node without a connection message, is ignored.</summary>
    [Fact]
    public async Task Handshake_EmptyRawPayload_IsIgnored()
    {
        Mock<TestEngineController> a = HandshakeController();
        a.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns(new UserIdentity { Name = "BOB" });
        Mock<TestEngineController> b = HandshakeController();
        b.Setup(c => c.IdentifyConnection(It.IsAny<ConnectionInfo>())).Returns(new UserIdentity { Name = "ALICE" });
        (End endA, End endB) = Pair(a.Object, b.Object);
        PeerConnection outbound = await endA.Transport.Connect(point);
        await WaitUntil(() => endB.Connected.Count == 1);

        await endA.Raw.Request(outbound, ReadOnlyMemory<byte>.Empty);
        await endA.Raw.Request(outbound, new byte[] { 0x7F, 1, 2 });
        await Task.Delay(50);

        Assert.Empty(endB.Received);
    }

    /// <summary>A connection message type with no serializer is a configuration error, reported when the transport is built.</summary>
    [Fact]
    public void Constructor_MessageTypeWithoutSerializer_Throws()
    {
        Mock<TestEngineController> controller = Controller();
        controller.Setup(c => c.ConnectionMessageType).Returns(typeof(TestHello));
        controller.Setup(c => c.ConnectionSerializer).Returns((INetworkSerializer?)null);
        (LoopbackPeerTransport raw, _) = LoopbackPeerTransport.CreatePair();

        Assert.Throws<InvalidOperationException>(() => new IdentifyingPeerTransport(raw, controller.Object, logger));
    }

    /// <summary>The management calls that are not about identity go straight to the wrapped transport.</summary>
    [Fact]
    public async Task ManagementCalls_GoToWrappedTransport()
    {
        Mock<IPeerTransport> inner = new();
        inner.SetupGet(t => t.Received).Returns(new TestObservable<PeerReceivedEventArgs>());
        inner.SetupGet(t => t.Connected).Returns(new TestObservable<PeerConnectionEventArgs>());
        inner.SetupGet(t => t.Disconnected).Returns(new TestObservable<PeerConnectionEventArgs>());
        IdentifyingPeerTransport transport = new(inner.Object, Controller().Object, logger);

        transport.StartListener(50021);
        transport.SetClosed(point, true);
        transport.Reset(point);
        await transport.DisposeAsync();

        inner.Verify(t => t.StartListener(50021), Times.Once);
        inner.Verify(t => t.SetClosed(point, true), Times.Once);
        inner.Verify(t => t.Reset(point), Times.Once);
        inner.Verify(t => t.DisposeAsync(), Times.Once);
    }
}
