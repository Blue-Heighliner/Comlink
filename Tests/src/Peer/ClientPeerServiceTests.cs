namespace BlueHeighliner.Comlink.Tests.Peer;

/// <summary>Unit tests for <see cref="ClientPeerService"/> connection retry, send coalescing, and message dispatch.</summary>
public sealed class ClientPeerServiceTests
{
    private static readonly ILoggerFactory NoLogger = LoggerFactory.Create(_ => { });
    private static readonly IMessageFormat Format = new TestMessageFormat();
    private static readonly UserEndpoint ServerEndpoint = new() { IpAddress = "10.0.0.1", Port = 9000 };

    private static Mock<IOftConnection> BuildConnection()
    {
        Mock<IOftConnection> connection = new();
        connection.SetupProperty(c => c.ReceivedHandler);
        connection.SetupProperty(c => c.DisconnectedHandler);
        connection.SetupGet(c => c.IsConnected).Returns(true);
        connection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        connection.Setup(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return connection;
    }

    private static (ClientPeerService Service, Mock<IOftConnector> Connector) Build(Mock<IOftConnection> connection, bool endpointConfigured = true, TimeSpan? retryInterval = null)
    {
        Mock<IOftConnector> connector = new();
        connector.Setup(c => c.Connect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<OftConnectionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        Mock<INetworkTopology> endpointProvider = new();
        endpointProvider.Setup(p => p.GetServerEndpoint()).Returns(endpointConfigured ? ServerEndpoint : null);

        Mock<IOftCertificateProvider> certProvider = new();
        certProvider.Setup(p => p.GetPeerOptions()).Returns(new OftPeerOptions { Info = "CLIENT" });

        ClientPeerService service = new(connector.Object, endpointProvider.Object, certProvider.Object, Format, NoLogger, retryInterval ?? TimeSpan.FromMilliseconds(20));
        return (service, connector);
    }

    private static ReadOnlyMemory<byte> Encode(TestMessage message)
    {
        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    /// <summary>Start with no server endpoint configured logs an error and never dials out.</summary>
    [Fact]
    public async Task Start_NoEndpointConfigured_DoesNotConnect()
    {
        Mock<IOftConnection> connection = BuildConnection();
        (ClientPeerService service, Mock<IOftConnector> connector) = Build(connection, endpointConfigured: false);

        await service.Start(CancellationToken.None);

        connector.Verify(c => c.Connect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<OftConnectionOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Start dials the configured server endpoint and wires the connection's handlers.</summary>
    [Fact]
    public async Task Start_ConnectsToConfiguredServer_SetsHandlers()
    {
        Mock<IOftConnection> connection = BuildConnection();
        (ClientPeerService service, Mock<IOftConnector> connector) = Build(connection);
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);

        connector.Verify(c => c.Connect(ServerEndpoint.IpAddress, ServerEndpoint.Port, It.IsAny<OftConnectionOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(connection.Object.ReceivedHandler);
        Assert.NotNull(connection.Object.DisconnectedHandler);

        cts.Cancel();
        await startTask;
    }

    /// <summary>After the connection disconnects, Start reconnects instead of giving up.</summary>
    [Fact]
    public async Task Start_ConnectionDisconnects_Reconnects()
    {
        Mock<IOftConnection> connection = BuildConnection();
        (ClientPeerService service, Mock<IOftConnector> connector) = Build(connection);
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);
        connection.Object.DisconnectedHandler!(null);

        await WaitUntil(() => connector.Invocations.Count >= 2, TimeSpan.FromSeconds(2));
        connector.Verify(c => c.Connect(ServerEndpoint.IpAddress, ServerEndpoint.Port, It.IsAny<OftConnectionOptions?>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));

        cts.Cancel();
        await startTask;
    }

    /// <summary>Send fails immediately while no connection is currently established.</summary>
    [Fact]
    public async Task Send_NotConnected_ReturnsFalse()
    {
        Mock<IOftConnection> connection = BuildConnection();
        (ClientPeerService service, _) = Build(connection);

        bool ok = await service.Send("ANY-USER", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.False(ok);
    }

    /// <summary>Send transmits over the single shared server connection once the connection is established.</summary>
    [Fact]
    public async Task Send_Connected_TransmitsOverSharedConnection()
    {
        Mock<IOftConnection> connection = BuildConnection();
        (ClientPeerService service, _) = Build(connection);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);

        bool ok = await service.Send("DEST", new TestMessage { MessageId = "M1", FromUser = "SOURCE" });

        Assert.True(ok);
        connection.Verify(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), 0, It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>
    /// Multiple Send calls for the same message ID (e.g. one per group member expanded by
    /// MessageRoutingService) are coalesced into a single physical transmission over the shared connection.
    /// </summary>
    [Fact]
    public async Task Send_SameMessageIdCalledConcurrently_TransmitsOnlyOnce()
    {
        Mock<IOftConnection> connection = BuildConnection();
        (ClientPeerService service, _) = Build(connection);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);

        TestMessage message = new() { MessageId = "M1", FromUser = "SOURCE" };
        bool[] results = await Task.WhenAll(
            service.Send("USER-A", message),
            service.Send("USER-B", message),
            service.Send("USER-C", message));

        Assert.All(results, Assert.True);
        connection.Verify(c => c.Send(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A valid message received over the connection fires MessageDelivered.</summary>
    [Fact]
    public async Task ReceivedHandler_ValidMessage_RaisesMessageDelivered()
    {
        Mock<IOftConnection> connection = BuildConnection();
        (ClientPeerService service, _) = Build(connection);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);

        TaskCompletionSource<object> tcs = new();
        service.MessageDelivered += message => { tcs.TrySetResult(message); return Task.CompletedTask; };

        using IMemoryOwner<byte> owner = new UnownedMemory(Encode(new TestMessage { MessageId = "MSG1", FromUser = "REMOTE" }));
        connection.Object.ReceivedHandler!(owner);

        object received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        TestMessage receivedMessage = Assert.IsType<TestMessage>(received);
        Assert.Equal("MSG1", receivedMessage.MessageId);

        cts.Cancel();
        await startTask;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(10);
        }
    }

    private sealed class UnownedMemory(ReadOnlyMemory<byte> data) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = data.ToArray();
        public void Dispose() { }
    }
}
