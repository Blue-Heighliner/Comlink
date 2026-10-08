namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit and real-MSMT integration tests for <see cref="InterfaceService"/>.</summary>
public sealed class InterfaceServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private readonly IEngineController format = new TestEngineController();
    private static readonly ProtobufSerializer serializer = new();

    private static UserInfo MakeUserInfo(string name) => new() { Name = name };

    /// <summary>A frame received from an interface is handed to the network processor as coming from the interface, under the installed user's name.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_ValidFrame_IsHandedToTheProcessor()
    {
        Mock<IMsmtSessionPeer.IFactory> peerFactory = new();
        Mock<INetworkProcessing> processing = new();
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));

        InterfaceService svc = new(peerFactory.Object, format, processing.Object, user.Object, noLogger);

        TestFrame incoming = new()
        {
            Body = "Body",
            Addresses = [new TestAddressEntry { UserName = "DEST", Type = "To" }, new TestAddressEntry { UserName = "OMAHA", Type = "External", Information = "Deliver to Eastside Office" }],
            Tag = "ALERT",
            Priority = "LEVEL2"
        };
        using IMemoryOwner<byte> buf = serializer.Serialize(incoming);

        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        processing.Verify(p => p.Received(It.Is<object>(frame => frame is TestFrame && ((TestFrame)frame).Body == "Body" && ((TestFrame)frame).Addresses.Count == 2), FrameOrigin.Interface, "LOCAL"), Times.Once);
    }

    /// <summary>A frame received from an interface is dropped when no user is installed.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_NoUserInstalled_IsNotHandedOn()
    {
        Mock<INetworkProcessing> processing = new();
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns((UserInfo?)null);
        InterfaceService svc = new(Mock.Of<IMsmtSessionPeer.IFactory>(), format, processing.Object, user.Object, noLogger);

        using IMemoryOwner<byte> buf = serializer.Serialize(new TestFrame { Body = "Hi" });
        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        processing.Verify(p => p.Received(It.IsAny<object>(), It.IsAny<FrameOrigin>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>A payload that describes a type other than the engine's FrameType is dropped without being handed on or throwing, since the serializer determines the type from the data itself.</summary>
    [Fact]
    public async Task HandleInterfaceMessage_ForeignType_IsDropped()
    {
        Mock<INetworkProcessing> processing = new();
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));
        InterfaceService svc = new(Mock.Of<IMsmtSessionPeer.IFactory>(), format, processing.Object, user.Object, noLogger);
        using IMemoryOwner<byte> buf = serializer.Serialize(new ForeignDto { Name = "not a message" });

        await svc.HandleInterfaceMessage(buf.Memory.ToArray());

        processing.Verify(p => p.Received(It.IsAny<object>(), It.IsAny<FrameOrigin>(), It.IsAny<string>()), Times.Never);
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
        Mock<INetworkProcessing> processing = new();
        Mock<IUserService> user = new();

        InterfaceService svc = new(Mock.Of<IMsmtSessionPeer.IFactory>(), format, processing.Object, user.Object, noLogger);

        await svc.HandleInterfaceMessage(new byte[] { 0xFF, 0xFE, 0xFD });

        processing.Verify(p => p.Received(It.IsAny<object>(), It.IsAny<FrameOrigin>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>A frame an interface sends over a real MSMT connection is handed to the network processor as received from the interface, under the app's own installed user.</summary>
    [Fact]
    public async Task RealMsmt_FrameFromInterface_IsHandedToTheProcessor()
    {
        int port = 44000 + Random.Shared.Next(1000);
        (X509Certificate2 serverCertificate, X509Certificate2 clientCertificate, X509Certificate2Collection trustedAuthorities) = TestMsmtCertificates.Create();

        Mock<INetworkProcessing> processing = new();
        TaskCompletionSource<(object Frame, FrameOrigin Origin, string Source)> routeCalled = new();
        processing.Setup(p => p.Received(It.IsAny<object>(), It.IsAny<FrameOrigin>(), It.IsAny<string>()))
            .Callback<object, FrameOrigin, string>((frame, origin, source) => routeCalled.TrySetResult((frame, origin, source)));
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));

        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(e => e.InterfacePort).Returns(port);
        engineController.Setup(e => e.ConnectionOptions).Returns(new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = serverCertificate, TrustedAuthorities = trustedAuthorities },
            RequireFullyQualifiedHostname = false
        });

        await using InterfaceService svc = new(new IMsmtSessionPeer.Factory(), engineController.Object, processing.Object, user.Object, noLogger);

        using CancellationTokenSource cts = new();
        _ = svc.Start(cts.Token);

        await using IMsmtSessionPeer client = new IMsmtSessionPeer.Factory().Create(new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = clientCertificate, TrustedAuthorities = trustedAuthorities },
            RequireFullyQualifiedHostname = false
        });

        TestFrame outgoing = new()
        {
            Body = "Body",
            Addresses = [new TestAddressEntry { UserName = "DEST", Type = "To" }]
        };

        // The interface listener may still be finishing binding immediately after Start() returns control;
        // reconnect and re-send until the processing observes it (harmless: it is a mock here).
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

        (object frame, FrameOrigin origin, string source) = await routeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(("LOCAL", FrameOrigin.Interface), (source, origin));
        TestFrame received = Assert.IsType<TestFrame>(frame);
        Assert.Equal("Body", received.Body);
        Assert.Equal("DEST", Assert.Single(received.Addresses).UserName);

        cts.Cancel();
    }

    /// <summary>A frame sent to the interfaces reaches an interface connected over real MSMT, down the connection it opened.</summary>
    [Fact]
    public async Task RealMsmt_SendToInterface_ReachesAConnectedInterface()
    {
        int port = 45000 + Random.Shared.Next(1000);
        (X509Certificate2 serverCertificate, X509Certificate2 clientCertificate, X509Certificate2Collection trustedAuthorities) = TestMsmtCertificates.Create();
        Mock<IUserService> user = new();
        user.Setup(s => s.GetCurrentUserInfo()).Returns(MakeUserInfo("LOCAL"));
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(e => e.InterfacePort).Returns(port);
        engineController.Setup(e => e.ConnectionOptions).Returns(new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = serverCertificate, TrustedAuthorities = trustedAuthorities },
            RequireFullyQualifiedHostname = false
        });
        await using InterfaceService svc = new(new IMsmtSessionPeer.Factory(), engineController.Object, Mock.Of<INetworkProcessing>(), user.Object, noLogger);
        using CancellationTokenSource cts = new();
        _ = svc.Start(cts.Token);

        await using IMsmtSessionPeer client = new IMsmtSessionPeer.Factory().Create(new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = clientCertificate, TrustedAuthorities = trustedAuthorities },
            RequireFullyQualifiedHostname = false
        });
        TaskCompletionSource<TestFrame> received = new();
        client.Receiver = (_, payload, responder) =>
        {
            using (payload)
            {
                received.TrySetResult((TestFrame)serializer.Deserialize(payload.Memory));
            }

            responder?.Accept(ReadOnlyMemory<byte>.Empty);
        };
        IMsmtConnection connection = client.Connect(new MsmtNameTarget { Host = "127.0.0.1", Port = port, ServerName = "127.0.0.1" });
        Assert.True(await connection.Wait());

        while (!received.Task.IsCompleted)
        {
            await svc.Send(TestMessagePriority.Normal, new TestFrame { Body = "To the interface" });
            await Task.Delay(100);
        }

        Assert.Equal("To the interface", (await received.Task.WaitAsync(TimeSpan.FromSeconds(10))).Body);
        cts.Cancel();
    }
}
