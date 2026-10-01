namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit and real-MSMT integration tests for <see cref="InterfaceService"/>.</summary>
public sealed class InterfaceServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private readonly IEngineController format = new TestEngineController();
    private static readonly INetworkSerializer serializer = new ProtobufNetworkSerializer();

    private static UserInfo MakeUserInfo(string name) => new() { Name = name };

    /// <summary>A message received from an interface is routed as if sent by the currently installed user.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_ValidMessage_RoutesAsCurrentUser()
    {
        Mock<IMsmtSessionPeer.IFactory> peerFactory = new();
        Mock<IMessageRoutingService> routing = new();
        routing.Setup(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("MSGID", (IReadOnlyList<UserDeliveryResult>)[]));
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));

        InterfaceService svc = new(peerFactory.Object, format, routing.Object, user.Object, noLogger);

        TestFrame incoming = new()
        {
            Subject = "Hi",
            Body = "Body",
            Addresses = [new TestAddressEntry { UserName = "DEST", Type = "To" }, new TestAddressEntry { UserName = "OMAHA", Type = "External", Information = "Deliver to Eastside Office" }],
            IsAlert = true,
            Priority = 2
        };
        using IMemoryOwner<byte> buf = serializer.Serialize(incoming);

        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        routing.Verify(r => r.Route("LOCAL", It.Is<SendMessagePayload>(p =>
            p.Subject == "Hi" && p.Body == "Body" && p.Addresses.Count == 2 && p.Addresses[0].UserName == "DEST" && p.Addresses[1].Type == "External" && p.Addresses[1].Information == "Deliver to Eastside Office" && p.IsAlert && p.Priority == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A failure while routing a message from an interface is logged rather than escaping into a task nobody observes.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_RoutingFails_DoesNotThrow()
    {
        Mock<IMsmtSessionPeer.IFactory> peerFactory = new();
        Mock<IMessageRoutingService> routing = new();
        routing.Setup(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));
        InterfaceService svc = new(peerFactory.Object, format, routing.Object, user.Object, noLogger);
        using IMemoryOwner<byte> buf = serializer.Serialize(new TestFrame { Subject = "Hi" });

        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        routing.Verify(r => r.Route("LOCAL", It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A message received from an interface is dropped without routing when no user is installed.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_NoUserInstalled_DoesNotRoute()
    {
        Mock<IMsmtSessionPeer.IFactory> peerFactory = new();
        Mock<IMessageRoutingService> routing = new();
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns((UserInfo?)null);

        InterfaceService svc = new(peerFactory.Object, format, routing.Object, user.Object, noLogger);

        using IMemoryOwner<byte> buf = serializer.Serialize(new TestFrame { Subject = "Hi" });
        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        routing.Verify(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A payload that describes a type other than the engine's FrameType is dropped without routing or throwing, since the serializer determines the type from the data itself.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_ForeignType_IsDroppedWithoutRouting()
    {
        Mock<IMsmtSessionPeer.IFactory> peerFactory = new();
        Mock<IMessageRoutingService> routing = new();
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));
        InterfaceService svc = new(peerFactory.Object, format, routing.Object, user.Object, noLogger);
        using IMemoryOwner<byte> buf = serializer.Serialize(new ForeignDto { Name = "not a message" });

        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        routing.Verify(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [ProtoContract]
    private sealed class ForeignDto
    {
        [ProtoMember(1)] public string Name { get; set; } = string.Empty;
    }

    /// <summary>Corrupted (non-protobuf) bytes from an interface are dropped without throwing.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_CorruptData_DoesNotThrow()
    {
        Mock<IMsmtSessionPeer.IFactory> peerFactory = new();
        Mock<IMessageRoutingService> routing = new();
        Mock<IUserService> user = new();

        InterfaceService svc = new(peerFactory.Object, format, routing.Object, user.Object, noLogger);

        await svc.HandleInterfaceMessage(new byte[] { 0xFF, 0xFE, 0xFD });

        routing.Verify(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A message an interface sends over a real MSMT connection is routed out to peers as if the app's own installed user had sent it.</summary>
    [Fact]
    public async Task RealMsmt_MessageFromInterface_IsRoutedAsCurrentUser()
    {
        int port = 44000 + Random.Shared.Next(1000);
        (X509Certificate2 serverCertificate, X509Certificate2 clientCertificate, X509Certificate2Collection trustedAuthorities) = TestMsmtCertificates.Create();

        Mock<IMessageRoutingService> routing = new();
        TaskCompletionSource<(string FromUser, SendMessagePayload Payload)> routeCalled = new();
        routing.Setup(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendMessagePayload, CancellationToken>((fromUser, payload, _) => routeCalled.TrySetResult((fromUser, payload)))
            .ReturnsAsync(("MSGID", (IReadOnlyList<UserDeliveryResult>)[]));
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));

        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(e => e.InterfacePort).Returns(port);
        engineController.Setup(e => e.ConnectionOptions).Returns(new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = serverCertificate, TrustedAuthorities = trustedAuthorities },
            RequireFullyQualifiedHostname = false
        });

        await using InterfaceService svc = new(new IMsmtSessionPeer.Factory(), engineController.Object, routing.Object, user.Object, noLogger);

        using CancellationTokenSource cts = new();
        _ = svc.Start(cts.Token);

        await using IMsmtSessionPeer client = new IMsmtSessionPeer.Factory().Create(new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = clientCertificate, TrustedAuthorities = trustedAuthorities },
            RequireFullyQualifiedHostname = false
        });

        TestFrame outgoing = new()
        {
            Subject = "FromInterface",
            Body = "Body",
            Addresses = [new TestAddressEntry { UserName = "DEST", Type = "To" }]
        };

        // The interface listener may still be finishing binding immediately after Start() returns control;
        // reconnect and re-send until routing observes it (harmless: Route is a no-op to production state here).
        _ = Task.Run(async () =>
        {
            try
            {
                while (!routeCalled.Task.IsCompleted)
                {
                    IMsmtConnection connection = client.Connect(new MsmtNameTarget { Host = "127.0.0.1", Port = port, ServerName = "127.0.0.1" });
                    if (await connection.Wait())
                    {
                        using IMemoryOwner<byte> buf = serializer.Serialize(outgoing);
                        await connection.Request(buf.Memory);
                    }
                    await connection.DisposeAsync();
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
