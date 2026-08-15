namespace BlueHeighliner.Comlink.Tests.Peer;

/// <summary>Unit tests for <see cref="ServerRoutingService"/> child/server connection classification and message routing.</summary>
public sealed class ServerRoutingServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private static readonly UserEndpoint serverAEndpoint = new() { IpAddress = "10.0.0.1", Port = 9001 };
    private static readonly UserEndpoint serverBEndpoint = new() { IpAddress = "10.0.0.2", Port = 9002 };

    private static Mock<IOftConnection> BuildConnection(string info)
    {
        Mock<IOftConnection> connection = new();
        connection.SetupProperty(c => c.ReceivedHandler);
        connection.SetupProperty(c => c.DisconnectedHandler);
        connection.SetupGet(c => c.IsConnected).Returns(true);
        connection.SetupGet(c => c.Identity).Returns(new OftIdentity { EndPoint = new IPEndPoint(IPAddress.Loopback, 0), Certificate = null, Info = info });
        connection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        connection.Setup(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return connection;
    }

    private sealed record Fixture(ServerRoutingService Service, Mock<IOftListener> Listener, Mock<IOftConnector> Connector, Task StartTask, CancellationTokenSource Cts);

    /// <summary>
    /// Builds a service for "ServerA" (this instance) with children ClientA1/ClientA2, alongside "ServerB"
    /// with children ClientB1/ClientB2, and starts it so its listener and outbound server connection are live.
    /// </summary>
    private static async Task<Fixture> BuildStarted(Mock<IOftConnection> serverBOutboundConnection)
    {
        Dictionary<string, ServerUserConfig> userMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerA"] = new ServerUserConfig { Endpoint = serverAEndpoint, ChildClients = ["ClientA1", "ClientA2"] },
            ["ServerB"] = new ServerUserConfig { Endpoint = serverBEndpoint, ChildClients = ["ClientB1", "ClientB2"] }
        };

        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(p => p.Servers).Returns(userMap);
        engineController.Setup(p => p.ConnectionOptions).Returns(new OftPeerOptions { Info = "ServerA" });

        Mock<ICurrentUserProvider> currentUser = new();
        currentUser.SetupGet(p => p.UserName).Returns("ServerA");

        Mock<IOftListener> listener = new();
        listener.SetupProperty(l => l.ConnectedHandler);

        Mock<IOftHoster> hoster = new();
        hoster.Setup(h => h.Host(It.IsAny<IPEndPoint>(), It.IsAny<OftConnectionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listener.Object);

        Mock<IOftConnector> connector = new();
        connector.Setup(c => c.Connect(serverBEndpoint.IpAddress, serverBEndpoint.Port, It.IsAny<OftConnectionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverBOutboundConnection.Object);

        ServerRoutingService service = new(hoster.Object, connector.Object, engineController.Object, currentUser.Object, noLogger, TimeSpan.FromMilliseconds(20));

        CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await WaitUntil(() => listener.Object.ConnectedHandler is not null && connector.Invocations.Count >= 1, TimeSpan.FromSeconds(2));

        return new Fixture(service, listener, connector, startTask, cts);
    }

    private static ReadOnlyMemory<byte> Encode(TestMessage message)
    {
        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    private static TestMessage MessageTo(params string[] users) => new()
    {
        MessageId = "M1",
        FromUser = "SOURCE",
        Addresses = [.. users.Select(u => new TestAddressEntry { UserName = u, Type = "To" })]
    };

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(10);
        }
    }

    /// <summary>An inbound connection whose hail identifies a known child of this server is tracked as a child connection.</summary>
    [Fact]
    public async Task OnConnected_KnownChild_TrackedAsChild()
    {
        Mock<IOftConnection> serverB = BuildConnection("ServerB");
        Fixture fx = await BuildStarted(serverB);
        Mock<IOftConnection> clientA1 = BuildConnection("ClientA1");

        fx.Listener.Object.ConnectedHandler!(clientA1.Object);

        Assert.NotNull(clientA1.Object.ReceivedHandler);
        clientA1.Verify(c => c.DisposeAsync(), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>An inbound connection whose hail identifies an unrecognized identity is disposed and ignored.</summary>
    [Fact]
    public async Task OnConnected_UnrecognizedIdentity_Disposed()
    {
        Mock<IOftConnection> serverB = BuildConnection("ServerB");
        Fixture fx = await BuildStarted(serverB);
        Mock<IOftConnection> stranger = BuildConnection("UNKNOWN-USER");

        fx.Listener.Object.ConnectedHandler!(stranger.Object);

        await WaitUntil(() => stranger.Invocations.Any(i => i.Method.Name == nameof(IAsyncDisposable.DisposeAsync)), TimeSpan.FromSeconds(2));

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message from one child addressed to a sibling child is routed directly to that sibling, not forwarded to any server.</summary>
    [Fact]
    public async Task FromChild_AddressedToSiblingChild_RoutesToSibling()
    {
        Mock<IOftConnection> serverB = BuildConnection("ServerB");
        Fixture fx = await BuildStarted(serverB);

        Mock<IOftConnection> clientA1 = BuildConnection("ClientA1");
        Mock<IOftConnection> clientA2 = BuildConnection("ClientA2");
        fx.Listener.Object.ConnectedHandler!(clientA1.Object);
        fx.Listener.Object.ConnectedHandler!(clientA2.Object);

        clientA1.Object.ReceivedHandler!(new UnownedMemory(Encode(MessageTo("ClientA2"))));

        await WaitUntil(() => clientA2.Invocations.Any(i => i.Method.Name == nameof(IOftConnection.Send)), TimeSpan.FromSeconds(2));
        clientA2.Verify(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
        serverB.Verify(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message from a child addressed to a child of another server is forwarded to that server once.</summary>
    [Fact]
    public async Task FromChild_AddressedToRemoteServersChild_ForwardsToThatServerOnce()
    {
        Mock<IOftConnection> serverB = BuildConnection("ServerB");
        Fixture fx = await BuildStarted(serverB);

        Mock<IOftConnection> clientA1 = BuildConnection("ClientA1");
        fx.Listener.Object.ConnectedHandler!(clientA1.Object);

        // Addressed to both of ServerB's children — should still forward to ServerB exactly once.
        clientA1.Object.ReceivedHandler!(new UnownedMemory(Encode(MessageTo("ClientB1", "ClientB2"))));

        await WaitUntil(() => serverB.Invocations.Any(i => i.Method.Name == nameof(IOftConnection.Send)), TimeSpan.FromSeconds(2));
        serverB.Verify(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>A message received from another server is delivered only to local children it addresses, never re-forwarded to other servers.</summary>
    [Fact]
    public async Task FromServer_AddressedToLocalChild_DeliversLocallyOnlyNeverReforwarded()
    {
        Mock<IOftConnection> serverB = BuildConnection("ServerB");
        Fixture fx = await BuildStarted(serverB);

        Mock<IOftConnection> clientA1 = BuildConnection("ClientA1");
        fx.Listener.Object.ConnectedHandler!(clientA1.Object);

        serverB.Object.ReceivedHandler!(new UnownedMemory(Encode(MessageTo("ClientA1"))));

        await WaitUntil(() => clientA1.Invocations.Any(i => i.Method.Name == nameof(IOftConnection.Send)), TimeSpan.FromSeconds(2));
        clientA1.Verify(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Never re-forwarded back out over the same (or any) server connection.
        serverB.Verify(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    /// <summary>The server user map keeps retrying an outbound connection to another server after it disconnects.</summary>
    [Fact]
    public async Task MaintainServerConnection_Disconnects_Reconnects()
    {
        Mock<IOftConnection> serverB = BuildConnection("ServerB");
        Fixture fx = await BuildStarted(serverB);

        serverB.Object.DisconnectedHandler!(null);

        await WaitUntil(() => fx.Connector.Invocations.Count >= 2, TimeSpan.FromSeconds(2));
        fx.Connector.Verify(c => c.Connect(serverBEndpoint.IpAddress, serverBEndpoint.Port, It.IsAny<OftConnectionOptions?>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));

        fx.Cts.Cancel();
        await fx.StartTask;
    }

    private sealed class UnownedMemory(ReadOnlyMemory<byte> data) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = data.ToArray();
        public void Dispose() { }
    }
}
