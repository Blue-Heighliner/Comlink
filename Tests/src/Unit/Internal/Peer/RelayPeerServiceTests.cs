namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="RelayPeerService"/>: forwarding between its child clients and its server, connection classification and status.</summary>
public sealed class RelayPeerServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private static readonly ConnectionPoint serverPoint = new() { IpAddress = "10.0.0.1", Port = 9000 };
    private static readonly ProtobufSerializer serializer = new();

    private sealed class Fixture(RelayPeerService service, Mock<IPeerTransport> transport, TestObservable<PeerConnectionEventArgs> connected, TestObservable<PeerConnectionEventArgs> disconnected, TestObservable<PeerReceivedEventArgs> received, PeerConnection server, Task startTask, CancellationTokenSource cts)
    {
        public RelayPeerService Service { get; } = service;
        public Mock<IPeerTransport> Transport { get; } = transport;
        public TestObservable<PeerConnectionEventArgs> Connected { get; } = connected;
        public TestObservable<PeerConnectionEventArgs> Disconnected { get; } = disconnected;
        public TestObservable<PeerReceivedEventArgs> Received { get; } = received;
        public PeerConnection Server { get; } = server;
        public Task StartTask { get; } = startTask;
        public CancellationTokenSource Cts { get; } = cts;

        public void Come(PeerConnection connection) => Connected.Publish(new PeerConnectionEventArgs { Connection = connection });

        public void Lose(PeerConnection connection) => Disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });

        public void Receive(PeerConnection connection, ReadOnlyMemory<byte> payload) => Received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = payload });

        public async Task Stop()
        {
            Cts.Cancel();
            await StartTask;
        }
    }

    private static PeerConnection Inbound(string user, Action? drop = null)
        => new(null, new IpConnectionInfo { IsInbound = true }, drop ?? (() => { })) { User = new UserIdentity { Name = user } };

    private static ReadOnlyMemory<byte> Encode(TestFrame message)
    {
        using IMemoryOwner<byte> buf = serializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    private static TestFrame MessageTo(params string[] users) => new()
    {
        MessageId = "M1",
        FromUser = "SOURCE",
        Addresses = [.. users.Select(u => new TestAddressEntry { UserName = u, Type = "To" })]
    };

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(10);
        }
    }

    private static async Task<Fixture> BuildStarted(string[]? children = null, bool serverReachable = true)
    {
        Mock<IPeerTransport> transport = new();
        TestObservable<PeerConnectionEventArgs> connected = new();
        TestObservable<PeerConnectionEventArgs> disconnected = new();
        TestObservable<PeerReceivedEventArgs> received = new();
        transport.SetupGet(p => p.Connected).Returns(connected);
        transport.SetupGet(p => p.Disconnected).Returns(disconnected);
        transport.SetupGet(p => p.Received).Returns(received);
        transport.Setup(p => p.Request(It.IsAny<PeerConnection>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        PeerConnection server = new(serverPoint, new IpConnectionInfo(), () => { }) { User = new UserIdentity { Name = "Server1" } };
        if (serverReachable)
        {
            int isUp = 0;
            transport.Setup(p => p.Connect(serverPoint, It.IsAny<CancellationToken>())).Returns(() =>
            {
                if (Interlocked.Exchange(ref isUp, 1) == 0) { connected.Publish(new PeerConnectionEventArgs { Connection = server }); }
                return Task.FromResult(server);
            });
        }
        else
        {
            transport.Setup(p => p.Connect(serverPoint, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("refused"));
        }

        Mock<IPeerTransportFactory> transportFactory = new();
        transportFactory.Setup(f => f.Create()).Returns(transport.Object);

        string[] childNames = children ?? ["ClientR1", "ClientR2"];
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(p => p.OutgoingPoints).Returns([serverPoint]);
        engineController.Setup(p => p.ParentPoints).Returns([serverPoint]);
        engineController.Setup(p => p.ParentUser).Returns("Server1");
        engineController.Setup(p => p.PeerPort).Returns(9100);
        engineController.Setup(p => p.GetUserInfo("Relay1")).Returns(new UserInfo { Name = "Relay1", Role = UserRole.Relay, Children = [.. childNames.Select(name => (UserLink)name)] });
        Mock<ICurrentUserProvider> currentUser = new();
        currentUser.SetupGet(p => p.UserName).Returns("Relay1");

        RelayPeerService service = new(transportFactory.Object, engineController.Object, currentUser.Object, noLogger);
        CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        Fixture fixture = new(service, transport, connected, disconnected, received, server, startTask, cts);
        if (serverReachable) { await WaitUntil(() => service.GetStatuses().Single(s => s.Kind == PeerConnectionKind.Server).IsConnected); }
        return fixture;
    }

    private static int Requests(Mock<IPeerTransport> transport, PeerConnection connection, ReadOnlyMemory<byte> payload)
        => transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request) && ReferenceEquals(i.Arguments[0], connection) && ((ReadOnlyMemory<byte>)i.Arguments[1]).Span.SequenceEqual(payload.Span));

    private static int RealRequests(Fixture fx, PeerConnection connection)
        => fx.Transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request) && ReferenceEquals(i.Arguments[0], connection) && !TestHeartbeat.Is((ReadOnlyMemory<byte>)i.Arguments[1]));

    /// <summary>A frame from a child goes to the server as exactly the bytes received, whoever it is addressed to.</summary>
    [Fact]
    public async Task FromChild_IsForwardedToTheServerUnchanged()
    {
        Fixture fx = await BuildStarted();
        PeerConnection child = Inbound("ClientR1");
        fx.Come(child);
        ReadOnlyMemory<byte> payload = Encode(MessageTo("SomeoneElsewhere"));

        fx.Receive(child, payload);

        await WaitUntil(() => RealRequests(fx, fx.Server) == 1);
        Assert.Equal(1, Requests(fx.Transport, fx.Server, payload));
        await fx.Stop();
    }

    /// <summary>A frame from the server goes, unchanged, to each of the relay's children it addresses and to no one else.</summary>
    [Fact]
    public async Task FromServer_IsForwardedOnlyToAddressedChildren()
    {
        Fixture fx = await BuildStarted(["ClientR1", "ClientR2", "ClientR3"]);
        PeerConnection first = Inbound("ClientR1");
        PeerConnection second = Inbound("ClientR2");
        PeerConnection third = Inbound("ClientR3");
        fx.Come(first);
        fx.Come(second);
        fx.Come(third);
        ReadOnlyMemory<byte> payload = Encode(MessageTo("ClientR1", "ClientR3", "NotAChild"));

        fx.Receive(fx.Server, payload);

        await WaitUntil(() => RealRequests(fx, first) == 1 && RealRequests(fx, third) == 1);
        await Task.Delay(100);
        Assert.Equal(0, RealRequests(fx, second));
        Assert.Equal(1, Requests(fx.Transport, first, payload));
        await fx.Stop();
    }

    /// <summary>The relay never turns traffic around: a frame from a child addressed to a sibling goes up to the server, not across.</summary>
    [Fact]
    public async Task FromChild_AddressedToSibling_StillGoesOnlyToTheServer()
    {
        Fixture fx = await BuildStarted();
        PeerConnection sender = Inbound("ClientR1");
        PeerConnection sibling = Inbound("ClientR2");
        fx.Come(sender);
        fx.Come(sibling);

        fx.Receive(sender, Encode(MessageTo("ClientR2")));

        await WaitUntil(() => RealRequests(fx, fx.Server) == 1);
        await Task.Delay(100);
        Assert.Equal(0, RealRequests(fx, sibling));
        await fx.Stop();
    }

    /// <summary>A frame that addresses no one, like those of the point-to-point initial exchange, is not traffic and is not forwarded in either direction.</summary>
    [Fact]
    public async Task FramesAddressingNoOne_AreNotForwarded()
    {
        Fixture fx = await BuildStarted();
        PeerConnection child = Inbound("ClientR1");
        fx.Come(child);
        ReadOnlyMemory<byte> unaddressed = Encode(new TestFrame { MessageId = "INITIAL", FromUser = "ClientR1", Body = "who am I" });

        fx.Receive(child, unaddressed);
        fx.Receive(fx.Server, unaddressed);
        await Task.Delay(150);

        Assert.Equal(0, RealRequests(fx, fx.Server));
        Assert.Equal(0, RealRequests(fx, child));
        await fx.Stop();
    }

    /// <summary>A message with no identifier is invalid and is not forwarded either way.</summary>
    [Fact]
    public async Task MessagesWithoutAnId_AreNotForwarded()
    {
        Fixture fx = await BuildStarted();
        PeerConnection child = Inbound("ClientR1");
        fx.Come(child);
        TestFrame message = MessageTo("ClientR1");
        message.MessageId = string.Empty;

        fx.Receive(child, Encode(message));
        fx.Receive(fx.Server, Encode(message));
        await Task.Delay(150);

        Assert.Equal(0, RealRequests(fx, fx.Server));
        Assert.Equal(0, RealRequests(fx, child));
        await fx.Stop();
    }

    /// <summary>Heartbeats are connection upkeep, not traffic, and are not forwarded.</summary>
    [Fact]
    public async Task Heartbeats_AreNotForwarded()
    {
        Fixture fx = await BuildStarted();
        PeerConnection child = Inbound("ClientR1");
        fx.Come(child);

        fx.Receive(child, TestHeartbeat.Bytes());
        fx.Receive(fx.Server, TestHeartbeat.Bytes());
        await Task.Delay(150);

        Assert.Equal(0, RealRequests(fx, fx.Server));
        Assert.Equal(0, RealRequests(fx, child));
        await fx.Stop();
    }

    /// <summary>The relay raises no delivery events and keeps nothing: forwarded traffic is never delivered locally or receipted.</summary>
    [Fact]
    public async Task ForwardedTraffic_IsNeverDeliveredLocally()
    {
        Fixture fx = await BuildStarted();
        PeerConnection child = Inbound("ClientR1");
        fx.Come(child);
        bool delivered = false;
        fx.Service.FrameDelivered += _ => { delivered = true; return Task.CompletedTask; };

        fx.Receive(fx.Server, Encode(MessageTo("ClientR1")));
        fx.Receive(child, Encode(MessageTo("SOMEONE")));
        await WaitUntil(() => RealRequests(fx, child) == 1 && RealRequests(fx, fx.Server) == 1);

        Assert.False(delivered);
        Assert.False(await fx.Service.Send("ClientR1", new TestFrame()));
        await fx.Stop();
    }

    /// <summary>A connection from anyone who is not one of the relay's children is dropped.</summary>
    [Fact]
    public async Task OnConnected_UnknownUser_IsDropped()
    {
        Fixture fx = await BuildStarted();
        int drops = 0;

        fx.Come(Inbound("Stranger", () => drops++));

        Assert.Equal(1, drops);
        Assert.DoesNotContain(fx.Service.GetStatuses(), status => status.UserName == "Stranger");
        await fx.Stop();
    }

    /// <summary>Frames from a connection that was never accepted are ignored.</summary>
    [Fact]
    public async Task Received_FromUnacceptedConnection_IsIgnored()
    {
        Fixture fx = await BuildStarted();

        fx.Receive(Inbound("Stranger"), Encode(MessageTo("X")));
        await Task.Delay(150);

        Assert.Equal(0, RealRequests(fx, fx.Server));
        await fx.Stop();
    }

    /// <summary>Statuses list every configured child and the server, with their live connection state.</summary>
    [Fact]
    public async Task GetStatuses_ListsChildrenAndServer()
    {
        Fixture fx = await BuildStarted();
        fx.Come(Inbound("ClientR1"));

        IReadOnlyList<PeerConnectionStatus> statuses = fx.Service.GetStatuses();

        Assert.True(statuses.Single(s => s.UserName == "ClientR1").IsConnected);
        Assert.False(statuses.Single(s => s.UserName == "ClientR2").IsConnected);
        Assert.Equal(PeerConnectionKind.Client, statuses.Single(s => s.UserName == "ClientR1").Kind);
        PeerConnectionStatus server = statuses.Single(s => s.Kind == PeerConnectionKind.Server);
        Assert.Equal("Server1", server.UserName);
        Assert.True(server.IsConnected);
        await fx.Stop();
    }

    /// <summary>Closing a child drops its connection and refuses it until it is reopened.</summary>
    [Fact]
    public async Task SetClosed_Child_DropsItAndRefusesItUntilReopened()
    {
        Fixture fx = await BuildStarted();
        int drops = 0;
        fx.Come(Inbound("ClientR1", () => drops++));

        fx.Service.SetClosed(PeerConnectionKind.Client, "ClientR1", true);

        Assert.Equal(1, drops);
        Assert.True(fx.Service.GetStatuses().Single(s => s.UserName == "ClientR1").IsClosed);
        fx.Come(Inbound("ClientR1", () => drops++));
        Assert.Equal(2, drops);

        fx.Service.SetClosed(PeerConnectionKind.Client, "ClientR1", false);
        fx.Come(Inbound("ClientR1", () => drops++));
        Assert.Equal(2, drops);
        await fx.Stop();
    }

    /// <summary>With no reachable server, a child's frame is not forwarded and nothing throws.</summary>
    [Fact]
    public async Task FromChild_ServerUnreachable_IsDroppedQuietly()
    {
        Fixture fx = await BuildStarted(serverReachable: false);
        PeerConnection child = Inbound("ClientR1");
        fx.Come(child);

        fx.Receive(child, Encode(MessageTo("SOMEONE")));
        await Task.Delay(150);

        Assert.Equal(0, RealRequests(fx, fx.Server));
        await fx.Stop();
    }
}
