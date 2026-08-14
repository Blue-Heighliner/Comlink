namespace BlueHeighliner.Comlink.Tests.Interface;

/// <summary>Unit and real-OFT integration tests for <see cref="InterfaceService"/>.</summary>
public sealed class InterfaceServiceTests
{
    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? MessageDelivered;
#pragma warning disable CS0067
        public event Func<string, string, Task>? ConfirmationReceived;
        public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067
        public Task Start(CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default) => Task.FromResult(true);
        public Task DeliverLocal(object payload) => Task.CompletedTask;

        public async Task FireMessageDelivered(object payload)
        {
            if (MessageDelivered is not null) await MessageDelivered(payload);
        }
    }

    private static readonly IMessageFormat Format = new TestMessageFormat();

    private static UserInfo MakeUserInfo(string name) => new() { Name = name, Code = "C1", EnvironmentTitle = "T", EnvironmentColor = "#000" };

    /// <summary>Retries connecting to the listener while it finishes binding, tolerating scheduling delays under parallel test load.</summary>
    private static async Task<IOftConnection> ConnectWithRetry(int port)
    {
        OftConnectionOptions options = new() { Info = string.Empty, SecurityMode = OftSecurityMode.Trusted };
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await new OftConnector().Connect("127.0.0.1", port, options);
            }
            catch when (attempt < 50)
            {
                await Task.Delay(100);
            }
        }
    }

    // ── HandleInterfaceMessage ────────────────────────────────────────────────

    /// <summary>A message received from an interface is routed as if sent by the currently installed user.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_ValidMessage_RoutesAsCurrentUser()
    {
        Mock<IOftHoster> hoster = new();
        Mock<IMessageRoutingService> routing = new();
        routing.Setup(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("MSGID", (IReadOnlyList<UserDeliveryResult>)[]));
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));
        FakePeerService peer = new();

        InterfaceService svc = new(hoster.Object, new DefaultPortConfiguration(), routing.Object, user.Object, Format, peer);

        TestMessage incoming = new()
        {
            Subject = "Hi",
            Body = "Body",
            Addresses = [new TestAddressEntry { UserName = "DEST", Type = "To" }],
            IsAlert = true,
            Priority = 2
        };
        using OwnedBuffer buf = PeerSerializer.Serialize(incoming);

        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        routing.Verify(r => r.Route("LOCAL", It.Is<SendMessagePayload>(p =>
            p.Subject == "Hi" && p.Body == "Body" && p.Addresses.Count == 1 && p.Addresses[0].UserName == "DEST" && p.IsAlert && p.Priority == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A message received from an interface is dropped without routing when no user is installed.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_NoUserInstalled_DoesNotRoute()
    {
        Mock<IOftHoster> hoster = new();
        Mock<IMessageRoutingService> routing = new();
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns((UserInfo?)null);
        FakePeerService peer = new();

        InterfaceService svc = new(hoster.Object, new DefaultPortConfiguration(), routing.Object, user.Object, Format, peer);

        using OwnedBuffer buf = PeerSerializer.Serialize(new TestMessage { Subject = "Hi" });
        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        routing.Verify(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Corrupted (non-protobuf) bytes from an interface are dropped without throwing.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_CorruptData_DoesNotThrow()
    {
        Mock<IOftHoster> hoster = new();
        Mock<IMessageRoutingService> routing = new();
        Mock<IUserService> user = new();
        FakePeerService peer = new();

        InterfaceService svc = new(hoster.Object, new DefaultPortConfiguration(), routing.Object, user.Object, Format, peer);

        await svc.HandleInterfaceMessage(new byte[] { 0xFF, 0xFE, 0xFD });

        routing.Verify(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Mirroring to connected interfaces ────────────────────────────────────

    /// <summary>An inbound peer message mirrored to a connected interface is sent at the message's own OFT priority.</summary>
    [Fact]
    public async Task OnMessageDelivered_MirrorsToConnectedInterface_AtMessagePriority()
    {
        Mock<IOftListener> listener = new();
        listener.SetupProperty(l => l.ConnectedHandler);
        Mock<IOftHoster> hoster = new();
        hoster.Setup(h => h.Host(It.IsAny<IPEndPoint>(), It.IsAny<OftConnectionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listener.Object);
        Mock<IMessageRoutingService> routing = new();
        Mock<IUserService> user = new();
        FakePeerService peer = new();

        InterfaceService svc = new(hoster.Object, new DefaultPortConfiguration(), routing.Object, user.Object, Format, peer);

        using CancellationTokenSource cts = new();
        _ = svc.Start(cts.Token);

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (listener.Object.ConnectedHandler is null)
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10);
        }

        Mock<IOftConnection> connection = new();
        connection.Setup(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        listener.Object.ConnectedHandler!(connection.Object);

        await peer.FireMessageDelivered(new TestMessage { MessageId = "M1", FromUser = "REMOTE", Priority = 3 });

        connection.Verify(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), 3, It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);

        cts.Cancel();
    }

    // ── End-to-end over real OFT ──────────────────────────────────────────────

    /// <summary>A message the user receives from a peer is mirrored, unmodified, to a connected interface.</summary>
    [Fact]
    public async Task RealOft_MessageDeliveredFromPeer_IsMirroredToConnectedInterface()
    {
        int port = 43000 + Random.Shared.Next(1000);
        Mock<IMessageRoutingService> routing = new();
        Mock<IUserService> user = new();
        FakePeerService peer = new();

        await using InterfaceService svc = new(new OftHoster(), new ConfiguredPortConfiguration(new DefaultPortConfiguration(), new EngineConfig { InterfacePort = port }), routing.Object, user.Object, Format, peer);

        using CancellationTokenSource cts = new();
        _ = svc.Start(cts.Token);

        await using IOftConnection client = await ConnectWithRetry(port);

        TaskCompletionSource<TestMessage> tcs = new();
        client.ReceivedHandler = data =>
        {
            byte[] copy;
            using (data) { copy = data.Memory.ToArray(); }
            TestMessage? message = PeerSerializer.Deserialize(typeof(TestMessage), copy) as TestMessage;
            if (message is not null) tcs.TrySetResult(message);
        };

        // The client's Connect() completing does not happen-before the server's ConnectedHandler
        // registering the connection into InterfaceService's own connection set — OnMessageDelivered
        // is a one-shot fire-and-forget event that silently drops the message if that registration
        // hasn't finished yet, so a single FireMessageDelivered call can race and be lost. Re-fire
        // until the client observes it (harmless: TrySetResult only ever accepts the first delivery).
        _ = Task.Run(async () =>
        {
            try
            {
                while (!tcs.Task.IsCompleted)
                {
                    await peer.FireMessageDelivered(new TestMessage { MessageId = "M1", FromUser = "REMOTE", Subject = "Hello", Body = "World" });
                    await Task.Delay(100);
                }
            }
            catch { }
        });

        TestMessage received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("M1", received.MessageId);
        Assert.Equal("REMOTE", received.FromUser);

        cts.Cancel();
    }

    /// <summary>A message an interface sends is routed out to peers as if the app's own installed user had sent it.</summary>
    [Fact]
    public async Task RealOft_MessageFromInterface_IsRoutedAsCurrentUser()
    {
        int port = 44000 + Random.Shared.Next(1000);
        Mock<IMessageRoutingService> routing = new();
        TaskCompletionSource<(string FromUser, SendMessagePayload Payload)> routeCalled = new();
        routing.Setup(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendMessagePayload, CancellationToken>((fromUser, payload, _) => routeCalled.TrySetResult((fromUser, payload)))
            .ReturnsAsync(("MSGID", (IReadOnlyList<UserDeliveryResult>)[]));
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));
        FakePeerService peer = new();

        await using InterfaceService svc = new(new OftHoster(), new ConfiguredPortConfiguration(new DefaultPortConfiguration(), new EngineConfig { InterfacePort = port }), routing.Object, user.Object, Format, peer);

        using CancellationTokenSource cts = new();
        _ = svc.Start(cts.Token);

        await using IOftConnection client = await ConnectWithRetry(port);

        TestMessage outgoing = new()
        {
            Subject = "FromInterface",
            Body = "Body",
            Addresses = [new TestAddressEntry { UserName = "DEST", Type = "To" }]
        };

        // Same race as RealOft_MessageDeliveredFromPeer_IsMirroredToConnectedInterface, mirrored on the
        // send side: the client's Connect() completing does not happen-before the server's ConnectedHandler
        // setting connection.ReceivedHandler, so a single send can arrive before the server is listening.
        // Re-send until routing observes it (harmless: Route is a no-op to production state here).
        _ = Task.Run(async () =>
        {
            try
            {
                while (!routeCalled.Task.IsCompleted)
                {
                    using OwnedBuffer buf = PeerSerializer.Serialize(outgoing);
                    await client.Send(buf.Memory);
                    await Task.Delay(100);
                }
            }
            catch { }
        });

        (string fromUser, SendMessagePayload payload) = await routeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("LOCAL", fromUser);
        Assert.Equal("FromInterface", payload.Subject);
        Assert.Single(payload.Addresses);
        Assert.Equal("DEST", payload.Addresses[0].UserName);

        cts.Cancel();
    }
}
