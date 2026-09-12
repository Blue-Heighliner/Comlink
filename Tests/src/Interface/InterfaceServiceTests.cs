namespace BlueHeighliner.Comlink.Tests.Interface;

/// <summary>Unit and real-MSMT integration tests for <see cref="InterfaceService"/>.</summary>
public sealed class InterfaceServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private readonly IEngineController format = new TestEngineController();

    private static UserInfo MakeUserInfo(string name) => new() { Name = name, Code = "C1", EnvironmentTitle = "T", EnvironmentColor = "#000" };

    /// <summary>A message received from an interface is routed as if sent by the currently installed user.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_ValidMessage_RoutesAsCurrentUser()
    {
        Mock<IMsmtPeerFactory> peerFactory = new();
        Mock<IMessageRoutingService> routing = new();
        routing.Setup(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("MSGID", (IReadOnlyList<UserDeliveryResult>)[]));
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));

        InterfaceService svc = new(peerFactory.Object, format, routing.Object, user.Object, noLogger);

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
        Mock<IMsmtPeerFactory> peerFactory = new();
        Mock<IMessageRoutingService> routing = new();
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns((UserInfo?)null);

        InterfaceService svc = new(peerFactory.Object, format, routing.Object, user.Object, noLogger);

        using OwnedBuffer buf = PeerSerializer.Serialize(new TestMessage { Subject = "Hi" });
        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        routing.Verify(r => r.Route(It.IsAny<string>(), It.IsAny<SendMessagePayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Corrupted (non-protobuf) bytes from an interface are dropped without throwing.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_CorruptData_DoesNotThrow()
    {
        Mock<IMsmtPeerFactory> peerFactory = new();
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
        engineController.Setup(e => e.ConnectionOptions).Returns(new MsmtOptions
        {
            Credentials = new MsmtCredentials { Identity = serverCertificate, TrustedAuthorities = trustedAuthorities },
            RequireFullyQualifiedHostname = false
        });

        await using InterfaceService svc = new(new MsmtPeerFactory(), engineController.Object, routing.Object, user.Object, noLogger);

        using CancellationTokenSource cts = new();
        _ = svc.Start(cts.Token);

        await using IMsmtPeer client = new MsmtPeerFactory().Create(new MsmtOptions
        {
            Credentials = new MsmtCredentials { Identity = clientCertificate, TrustedAuthorities = trustedAuthorities },
            RequireFullyQualifiedHostname = false
        });

        TestMessage outgoing = new()
        {
            Subject = "FromInterface",
            Body = "Body",
            Addresses = [new TestAddressEntry { UserName = "DEST", Type = "To" }]
        };

        // The interface listener may still be finishing binding immediately after Start() returns
        // control; re-send until routing observes it (harmless: Route is a no-op to production state here).
        _ = Task.Run(async () =>
        {
            try
            {
                while (!routeCalled.Task.IsCompleted)
                {
                    using OwnedBuffer buf = PeerSerializer.Serialize(outgoing);
                    client.Send(new MsmtTarget { Host = "127.0.0.1", Port = port }, buf.Memory);
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
