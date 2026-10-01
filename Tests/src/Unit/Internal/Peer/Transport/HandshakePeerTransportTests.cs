namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer.Transport;

/// <summary>Unit tests for <see cref="HandshakePeerTransport"/>, over an in-memory pair of transports.</summary>
public sealed class HandshakePeerTransportTests
{
    private static readonly ILogger logger = LoggerFactory.Create(_ => { }).CreateLogger("test");
    private static readonly ConnectionPoint point = new() { IpAddress = "10.0.0.5", Port = 4000 };
    private static readonly ConnectionPoint serialPoint = new() { SerialPort = "SL0" };
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    private static bool Matches(IConnectionInfo info) => info is ISerialConnectionInfo { SerialPort: "SL0", SerialAddress: 0xFF };

    private sealed class Scripted : IInitialProcessor
    {
        public Type ItemType { get; init; } = typeof(TestMessage);
        public Func<IInitialSession, Task> Connected { get; init; } = _ => Task.CompletedTask;
        public Func<IInitialSession, object, Task> Initial { get; init; } = (_, _) => Task.CompletedTask;
        public Func<IInitialSession, object, Task> Reply { get; init; } = (_, _) => Task.CompletedTask;

        public Task OnConnected(IInitialSession session) => Connected(session);

        public Task OnInitial(IInitialSession session, object item) => Initial(session, item);

        public Task OnReply(IInitialSession session, object item) => Reply(session, item);
    }

    private sealed class End(HandshakePeerTransport transport, LoopbackPeerTransport raw)
    {
        private readonly Lock gate = new();
        private readonly List<PeerConnection> connected = [];
        private readonly List<PeerConnection> disconnected = [];
        private readonly List<byte[]> received = [];

        public HandshakePeerTransport Transport { get; } = transport;
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

    private static Mock<TestEngineController> WithProcessor(IInitialProcessor processor)
    {
        Mock<TestEngineController> controller = Controller();
        controller.Setup(c => c.InitialMessageProcessor).Returns(processor);
        return controller;
    }

    private static Scripted Introduce(string me, List<bool>? openers = null)
        => new()
        {
            Connected = async session =>
            {
                openers?.Add(session.IsOpener);
                if (session.IsOpener) { await session.Send(Who(me)); }
            },
            Initial = async (session, item) =>
            {
                await session.Send(Who(me));
                session.Connected(((TestMessage)item).FromUser);
            },
            Reply = (session, item) =>
            {
                session.Connected(((TestMessage)item).FromUser);
                return Task.CompletedTask;
            }
        };

    private static TestMessage Who(string user) => new() { FromUser = user };

    private static (End A, End B) Pair(IEngineController a, IEngineController b, IReadOnlyList<string>? aNames = null, IReadOnlyList<string>? bNames = null, bool serial = false, TimeSpan? handshakeTimeout = null)
    {
        (LoopbackPeerTransport rawA, LoopbackPeerTransport rawB) = LoopbackPeerTransport.CreatePair(aNames, bNames, serial);
        End endA = new(new HandshakePeerTransport(rawA, a, logger, Handshake.ForMessages(a), identify: true, handshakeTimeout), rawA);
        End endB = new(new HandshakePeerTransport(rawB, b, logger, Handshake.ForMessages(b), identify: true, handshakeTimeout), rawB);
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
        a.Setup(c => c.GetUserData("CUSTOM")).Returns(new Dictionary<string, string> { ["k"] = "v" });
        IConnectionInfo? seen = null;
        a.Setup(c => c.IdentifyConnection(It.IsAny<IConnectionInfo>())).Returns((IConnectionInfo info) =>
        {
            seen = info;
            return "CUSTOM";
        });
        (End endA, _) = Pair(a.Object, Controller().Object, bNames: ["cert-BOB"]);

        PeerConnection connection = await endA.Transport.Connect(point);

        Assert.Equal("CUSTOM", connection.User!.Name);
        Assert.Equal("v", connection.User.Data["k"]);
        IIpConnectionInfo ip = Assert.IsAssignableFrom<IIpConnectionInfo>(seen);
        Assert.Equal("10.0.0.5", ip.Host);
        Assert.Equal(4000, ip.Port);
        Assert.False(ip.IsInbound);
        Assert.Equal(["cert-BOB"], ip.CertificateNames);
        Assert.Equal("CN=cert-BOB", ip.CertificateSubject);
    }

    /// <summary>A controller whose identification throws does not take the transport down: the connection is dropped.</summary>
    [Fact]
    public async Task Connect_ControllerIdentificationThrows_DropsConnection()
    {
        Mock<TestEngineController> a = Controller("BOB");
        a.Setup(c => c.IdentifyConnection(It.IsAny<IConnectionInfo>())).Throws(new InvalidOperationException("boom"));
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
        mapped.Setup(c => c.IdentifyConnection(It.Is<IConnectionInfo>(i => Matches(i)))).Returns("CONSOLE-3");
        (End mappedA, _) = Pair(mapped.Object, Controller().Object, serial: true);
        PeerConnection custom = await mappedA.Transport.Connect(serialPoint);
        Assert.Equal("CONSOLE-3", custom.User!.Name);
    }

    /// <summary>A serial point that names its user identifies the connection as that user, matching on port and address; a point that does not, or one with another address, falls back to the port name.</summary>
    [Fact]
    public async Task Connect_SerialPointNamingItsUser_IdentifiesAsThatUser()
    {
        Mock<TestEngineController> named = Controller();
        named.SetupGet(c => c.OutgoingPoints).Returns([new ConnectionPoint { SerialPort = "sl0", User = "SERVER" }, new ConnectionPoint { SerialPort = "SL0", SerialAddress = 5, User = "OTHER" }]);
        (End namedA, _) = Pair(named.Object, Controller().Object, serial: true);

        PeerConnection connection = await namedA.Transport.Connect(serialPoint);

        Assert.Equal("SERVER", connection.User!.Name);

        Mock<TestEngineController> unnamed = Controller();
        unnamed.SetupGet(c => c.OutgoingPoints).Returns([new ConnectionPoint { SerialPort = "SL0", SerialAddress = 5, User = "OTHER" }]);
        (End unnamedA, _) = Pair(unnamed.Object, Controller().Object, serial: true);
        Assert.Equal("SL0", (await unnamedA.Transport.Connect(serialPoint)).User!.Name);
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
        controller.Setup(c => c.IdentifyConnection(It.IsAny<IConnectionInfo>())).Returns(() => $"LINK-{++identifications}");
        HandshakePeerTransport transport = new(inner.Object, controller.Object, logger, null, identify: true);
        List<string> users = [];
        transport.Connected.Listen(args => users.Add(args.Connection.User!.Name));
        PeerConnection link = new(serialPoint, new SerialConnectionInfo { SerialPort = "SL0" }, () => { });

        connected.Publish(new PeerConnectionEventArgs { Connection = link });
        disconnected.Publish(new PeerConnectionEventArgs { Connection = link });
        connected.Publish(new PeerConnectionEventArgs { Connection = link });

        Assert.Equal(["LINK-1", "LINK-2"], users);
        await Task.CompletedTask;
    }

    /// <summary>Without an initial exchange payloads travel exactly as sent, in both directions, over the connection either end opened.</summary>
    [Fact]
    public async Task Request_WithoutInitialExchange_PassesPayloadsThroughUnchanged()
    {
        (End endA, End endB) = Pair(Controller().Object, Controller().Object, aNames: ["Alice"], bNames: ["Bob"]);
        PeerConnection outbound = await endA.Transport.Connect(point);
        await WaitUntil(() => endB.Connected.Count == 1);

        Assert.True(await endA.Transport.Request(outbound, new byte[] { 1, 2, 3 }));
        Assert.True(await endB.Transport.Request(endB.Connected[0], new byte[] { 9 }));

        await WaitUntil(() => endB.Received.Count == 1 && endA.Received.Count == 1);
        Assert.Equal(new byte[] { 1, 2, 3 }, endB.Received[0]);
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

    /// <summary>The processors decide the identity on both ends, ahead of the controller's own identification: the opener sends a message, the receiver answers and names the opener from it, and the opener names the receiver from the reply, data included.</summary>
    [Fact]
    public async Task Handshake_IdentitiesComeFromTheProcessors()
    {
        Mock<TestEngineController> a = WithProcessor(Introduce("ALICE"));
        a.Setup(c => c.GetUserData("BOB")).Returns(new Dictionary<string, string> { ["station"] = "4" });
        a.Setup(c => c.IdentifyConnection(It.IsAny<IConnectionInfo>())).Returns("WRONG");
        Mock<TestEngineController> b = WithProcessor(Introduce("BOB"));
        (End endA, End endB) = Pair(a.Object, b.Object);

        PeerConnection outbound = await endA.Transport.Connect(point);

        Assert.Equal("BOB", outbound.User!.Name);
        Assert.Equal("4", outbound.User.Data["station"]);
        await WaitUntil(() => endB.Connected.Count == 1);
        Assert.Equal("ALICE", endB.Connected[0].User!.Name);
    }

    /// <summary>The exchange adds nothing to what crosses the connection: the initial message is the serialized message itself, and later payloads arrive exactly as sent, in both directions.</summary>
    [Fact]
    public async Task Handshake_AddsNothingToPayloads_AndDataFlowsBothWaysAfterwards()
    {
        Mock<TestEngineController> a = WithProcessor(Introduce("ALICE"));
        (End endA, End endB) = Pair(a.Object, WithProcessor(Introduce("BOB")).Object);
        PeerConnection outbound = await endA.Transport.Connect(point);
        await WaitUntil(() => endB.Connected.Count == 1);

        Assert.True(await endA.Transport.Request(outbound, new byte[] { 1, 2, 3 }));
        Assert.True(await endB.Transport.Request(endB.Connected[0], new byte[] { 7 }));

        await WaitUntil(() => endB.Received.Count == 1 && endA.Received.Count == 1);
        Assert.Equal(new byte[] { 1, 2, 3 }, endB.Received[0]);
        Assert.Equal(new byte[] { 7 }, endA.Received[0]);
        using IMemoryOwner<byte> expected = a.Object.NetworkSerializer.Serialize(Who("ALICE"));
        Assert.Equal(expected.Memory.ToArray(), endB.Raw.Delivered.First());
        Assert.Equal(new byte[] { 1, 2, 3 }, endB.Raw.Delivered.ElementAt(1));
    }

    /// <summary>A payload sent the moment the opener's connection is usable reaches the receiver even if the receiver has not finished establishing yet: it is held, then delivered in order.</summary>
    [Fact]
    public async Task Handshake_DataSentImmediately_IsHeldUntilTheReceiverIsEstablished()
    {
        Mock<TestEngineController> b = WithProcessor(Introduce("BOB"));
        b.Setup(c => c.GetUserData("ALICE")).Returns(() =>
        {
            Thread.Sleep(100);
            return new Dictionary<string, string>();
        });
        (End endA, End endB) = Pair(WithProcessor(Introduce("ALICE")).Object, b.Object);

        PeerConnection outbound = await endA.Transport.Connect(point);
        await endA.Transport.Request(outbound, new byte[] { 1 });
        await endA.Transport.Request(outbound, new byte[] { 2 });

        await WaitUntil(() => endB.Received.Count == 2);
        Assert.Equal(new byte[] { 1 }, endB.Received[0]);
        Assert.Equal(new byte[] { 2 }, endB.Received[1]);
        Assert.Single(endB.Connected);
    }

    /// <summary>A processor may take several rounds: every item that arrives before the connection is marked connected is handed to it, in order.</summary>
    [Fact]
    public async Task Handshake_SeveralItems_AreHandedToTheProcessorInOrder()
    {
        List<string> seen = [];
        Scripted opener = new()
        {
            Connected = async session =>
            {
                if (!session.IsOpener) { return; }
                await session.Send(Who("ONE"));
                await session.Send(Who("TWO"));
            },
            Reply = (session, item) =>
            {
                session.Connected("BOB");
                return Task.CompletedTask;
            }
        };
        Scripted acceptor = new()
        {
            Initial = async (session, item) =>
            {
                seen.Add(((TestMessage)item).FromUser);
                if (seen.Count < 2) { return; }

                await session.Send(Who("DONE"));
                session.Connected("ALICE");
            }
        };
        (End endA, _) = Pair(WithProcessor(opener).Object, WithProcessor(acceptor).Object);

        PeerConnection outbound = await endA.Transport.Connect(point);

        Assert.Equal("BOB", outbound.User!.Name);
        Assert.Equal(["ONE", "TWO"], seen);
    }

    /// <summary>A serial link has no opener, so the node at the higher station address is the opener and the other accepts: one message and one reply cross it, and each end names the other.</summary>
    [Fact]
    public async Task Handshake_Serial_HigherAddressOpens()
    {
        List<bool> openersA = [];
        List<bool> openersB = [];
        (End endA, End endB) = Pair(WithProcessor(Introduce("ALICE", openersA)).Object, WithProcessor(Introduce("BOB", openersB)).Object, serial: true);

        PeerConnection link = await endA.Transport.Connect(serialPoint);

        Assert.Equal("BOB", link.User!.Name);
        await WaitUntil(() => endB.Connected.Count == 1);
        Assert.Equal("ALICE", endB.Connected[0].User!.Name);
        Assert.Equal([true], openersA);
        Assert.Equal([false], openersB);
    }

    /// <summary>The processor disconnecting drops the connection, which fails the connect.</summary>
    [Fact]
    public async Task Handshake_ProcessorDisconnects_DropsConnection()
    {
        Scripted refusing = new() { Connected = session => { session.Disconnect(); return Task.CompletedTask; } };
        (End endA, _) = Pair(WithProcessor(refusing).Object, WithProcessor(Introduce("BOB")).Object);

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Connect(point));

        Assert.Empty(endA.Connected);
    }

    /// <summary>If the processor never marks the connection connected, the connection is dropped once the timeout passes and connecting fails.</summary>
    [Fact]
    public async Task Handshake_NeverConnected_TimesOutAndDrops()
    {
        Scripted silent = new();
        (End endA, End endB) = Pair(WithProcessor(Introduce("ALICE")).Object, WithProcessor(silent).Object, handshakeTimeout: TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Connect(point));

        Assert.Empty(endA.Connected);
        Assert.Empty(endB.Connected);
    }

    /// <summary>An item of a type other than the processor's is refused and the connection dropped.</summary>
    [Fact]
    public async Task Handshake_WrongItemType_DropsConnection()
    {
        Scripted wrong = new() { ItemType = typeof(TestHello) };
        (End endA, End endB) = Pair(WithProcessor(Introduce("ALICE")).Object, WithProcessor(wrong).Object, handshakeTimeout: TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Connect(point));

        Assert.Empty(endB.Connected);
    }

    /// <summary>A processor that throws drops the connection.</summary>
    [Fact]
    public async Task Handshake_ProcessorThrows_DropsConnection()
    {
        Scripted throwing = new() { Connected = _ => throw new InvalidOperationException("boom") };
        (End endA, _) = Pair(WithProcessor(throwing).Object, WithProcessor(Introduce("BOB")).Object);

        await Assert.ThrowsAsync<IOException>(() => endA.Transport.Connect(point));

        Assert.Empty(endA.Connected);
    }

    /// <summary>The initial packet exchange travels as instances of the packet type through the packet serializer, names the user on the connection for identification above it, and identifies no one itself.</summary>
    [Fact]
    public async Task InitialPacket_ExchangesPackets_AndNamesTheUserOnTheConnection()
    {
        Scripted a = new()
        {
            ItemType = typeof(TestPacket),
            Connected = async session =>
            {
                if (session.IsOpener) { await session.Send(new TestPacket { PayloadId = 11 }); }
            },
            Reply = (session, item) =>
            {
                Assert.Equal(22, ((TestPacket)item).PayloadId);
                session.Connected("BOB");
                return Task.CompletedTask;
            }
        };
        Scripted b = new()
        {
            ItemType = typeof(TestPacket),
            Initial = async (session, item) =>
            {
                Assert.Equal(11, ((TestPacket)item).PayloadId);
                await session.Send(new TestPacket { PayloadId = 22 });
                session.Connected("ALICE");
            }
        };
        Mock<TestPacketEngineController> controllerA = new() { CallBase = true };
        controllerA.Setup(c => c.InitialPacketProcessor).Returns(a);
        Mock<TestPacketEngineController> controllerB = new() { CallBase = true };
        controllerB.Setup(c => c.InitialPacketProcessor).Returns(b);
        (LoopbackPeerTransport rawA, LoopbackPeerTransport rawB) = LoopbackPeerTransport.CreatePair();
        HandshakePeerTransport endA = new(rawA, controllerA.Object, logger, Handshake.ForPackets(controllerA.Object), identify: false);
        HandshakePeerTransport endB = new(rawB, controllerB.Object, logger, Handshake.ForPackets(controllerB.Object), identify: false);
        List<PeerConnection> accepted = [];
        endB.Connected.Listen(args => accepted.Add(args.Connection));

        PeerConnection outbound = await endA.Connect(point);

        Assert.Null(outbound.User);
        Assert.Equal("BOB", outbound.InitialUser);
        await WaitUntil(() => accepted.Count == 1);
        Assert.Equal("ALICE", accepted[0].InitialUser);
        Assert.Null(accepted[0].User);
    }

    /// <summary>An initial packet processor with no packet serializer is a configuration error, reported when the exchange is built.</summary>
    [Fact]
    public void ForPackets_WithoutSerializer_Throws()
    {
        Mock<TestEngineController> controller = Controller();
        controller.Setup(c => c.InitialPacketProcessor).Returns(new Scripted());

        Assert.Throws<InvalidOperationException>(() => Handshake.ForPackets(controller.Object));
        Assert.Null(Handshake.ForPackets(Controller().Object));
        Assert.Null(Handshake.ForMessages(Controller().Object));
    }

    /// <summary>The management calls that are not about identity go straight to the wrapped transport.</summary>
    [Fact]
    public async Task ManagementCalls_GoToWrappedTransport()
    {
        Mock<IPeerTransport> inner = new();
        inner.SetupGet(t => t.Received).Returns(new TestObservable<PeerReceivedEventArgs>());
        inner.SetupGet(t => t.Connected).Returns(new TestObservable<PeerConnectionEventArgs>());
        inner.SetupGet(t => t.Disconnected).Returns(new TestObservable<PeerConnectionEventArgs>());
        HandshakePeerTransport transport = new(inner.Object, Controller().Object, logger, null, identify: true);

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
