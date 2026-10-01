namespace BlueHeighliner.Comlink.Tests.Integration;

/// <summary>
/// Real-I/O tests of the peer network: nodes configured with only the points they listen on and connect out to find out who
/// is on the other end of each connection from its certificate, and route messages over whichever connection is identified as
/// the recipient, including one the recipient itself opened.
/// </summary>
public sealed class PeerNetworkTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(30);

    private static readonly HashSet<int> handedOut = [];
    private static readonly Lock portGate = new();

    private static int FreePort()
    {
        lock (portGate)
        {
            while (true)
            {
                using TcpListener listener = new(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                if (handedOut.Add(port)) { return port; }
            }
        }
    }

    private static ConnectionPoint Local(int port) => new() { IpAddress = "127.0.0.1", Port = port };

    private sealed class Node : IAsyncDisposable
    {
        private readonly CancellationTokenSource cts = new();
        private readonly Task run;

        public Node(string user, X509Certificate2 identity, X509Certificate2Collection authorities, UserRole role, int peerPort, IReadOnlyList<ConnectionPoint> outgoing, IReadOnlyDictionary<string, ServerUserConfig>? servers = null, IMessageStorageService? storage = null)
        {
            Mock<TestEngineController> controller = new() { CallBase = true };
            controller.Setup(c => c.Role).Returns(role);
            controller.Setup(c => c.PeerPort).Returns(peerPort);
            controller.Setup(c => c.OutgoingPoints).Returns(outgoing);
            controller.Setup(c => c.Servers).Returns(servers ?? new Dictionary<string, ServerUserConfig>());
            controller.Setup(c => c.ConnectionOptions).Returns(new MsmtSessionPeerOptions
            {
                Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = authorities },
                RequireFullyQualifiedHostname = false
            });
            Mock<ICurrentUserProvider> currentUser = new();
            currentUser.SetupGet(p => p.UserName).Returns(user);
            PeerTransportFactory factory = new(new IMsmtSessionPeer.Factory(), Mock.Of<IMicroGatePeerFactory>(), controller.Object, noLogger);

            (Service, Status) = role switch
            {
                UserRole.Server => Both(new ServerRoutingService(factory, controller.Object, currentUser.Object, storage ?? Mock.Of<IMessageStorageService>(), noLogger)),
                UserRole.Client => Both(new ClientPeerService(factory, controller.Object, noLogger)),
                _ => (new PeerService(factory, controller.Object, noLogger), null)
            };
            Service.FrameDelivered += message => { Delivered.Enqueue((TestFrame)message); return Task.CompletedTask; };
            run = Service.Start(cts.Token);
        }

        public IPeerService Service { get; }
        public IConnectionStatusService? Status { get; }
        public ConcurrentQueue<TestFrame> Delivered { get; } = [];

        public bool IsUp(string user)
            => Status!.GetStatuses().Any(s => string.Equals(s.UserName, user, StringComparison.OrdinalIgnoreCase) && s.IsConnected);

        public async ValueTask DisposeAsync()
        {
            await cts.CancelAsync();
            await run;
            await ((IAsyncDisposable)Service).DisposeAsync();
        }

        private static (IPeerService, IConnectionStatusService) Both<T>(T service) where T : IPeerService, IConnectionStatusService => (service, service);
    }

    private static TestFrame MessageTo(string from, string to, string id = "M1")
        => new() { MessageId = id, FromUser = from, Subject = "Hi", Addresses = [new TestAddressEntry { UserName = to, Type = "To" }] };

    private static async Task WaitUntil(Func<bool> condition, string what)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException($"Timed out waiting for {what}."); }
            await Task.Delay(20);
        }
    }

    private static async Task WaitUntil(Func<Task<bool>> condition, string what)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException($"Timed out waiting for {what}."); }
            await Task.Delay(20);
        }
    }

    private static Dictionary<string, ServerUserConfig> OneServer(params string[] children)
        => new(StringComparer.OrdinalIgnoreCase) { ["Server"] = new ServerUserConfig { ChildClients = children } };

    /// <summary>A server that only listens routes a message between two clients that only connect out: it is pushed to the recipient over the connection the recipient opened, and the rows on both sides are named from the certificates.</summary>
    [Fact]
    public async Task ClientServer_MessageBetweenClients_TravelsOverTheClientsOwnConnections()
    {
        (Dictionary<string, X509Certificate2> certificates, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("Server", "Client1", "Client2");
        int serverPort = FreePort();
        await using Node server = new("Server", certificates["Server"], authorities, UserRole.Server, serverPort, [], OneServer("Client1", "Client2"));
        await using Node client1 = new("Client1", certificates["Client1"], authorities, UserRole.Client, 0, [Local(serverPort)]);
        await using Node client2 = new("Client2", certificates["Client2"], authorities, UserRole.Client, 0, [Local(serverPort)]);

        await WaitUntil(() => server.IsUp("Client1") && server.IsUp("Client2") && client1.IsUp("Server") && client2.IsUp("Server"), "every client to connect and be identified");
        bool sent = await client1.Service.Send("Client2", MessageTo("Client1", "Client2"));

        Assert.True(sent);
        await WaitUntil(() => !client2.Delivered.IsEmpty, "the message to reach Client2");
        Assert.True(client2.Delivered.TryPeek(out TestFrame? message));
        Assert.Equal("M1", message.MessageId);
        Assert.Equal("Client1", message.FromUser);
        Assert.Empty(client1.Delivered);
    }

    /// <summary>
    /// A storage server keeps what it routes and answers a client's retrieval request over the same connections: the
    /// author asks for its own sent message by ID and gets back a copy addressed only to it, while the original
    /// recipient hears nothing more.
    /// </summary>
    [Fact]
    public async Task ClientServer_StorageServer_AnswersARetrievalRequestWithACopy()
    {
        string appName = Guid.NewGuid().ToString();
        using LiteDbContext db = new(new TestAppDataPathProvider(appName));
        db.Initialize();
        try
        {
            Mock<TestEngineController> storageController = new() { CallBase = true };
            storageController.Setup(c => c.StorageServers).Returns(["Server"]);
            Mock<ICurrentUserProvider> storageUser = new();
            storageUser.SetupGet(p => p.UserName).Returns("Server");
            MessageStorageService storage = new(new StoredMessageRepository(db), storageController.Object, storageUser.Object, noLogger);

            (Dictionary<string, X509Certificate2> certificates, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("Server", "Client1", "Client2");
            int serverPort = FreePort();
            await using Node server = new("Server", certificates["Server"], authorities, UserRole.Server, serverPort, [], OneServer("Client1", "Client2"), storage);
            await using Node client1 = new("Client1", certificates["Client1"], authorities, UserRole.Client, 0, [Local(serverPort)]);
            await using Node client2 = new("Client2", certificates["Client2"], authorities, UserRole.Client, 0, [Local(serverPort)]);
            await WaitUntil(() => server.IsUp("Client1") && server.IsUp("Client2") && client1.IsUp("Server") && client2.IsUp("Server"), "every client to connect");
            Assert.True(await client1.Service.Send("Client2", MessageTo("Client1", "Client2")));
            await WaitUntil(() => !client2.Delivered.IsEmpty, "the original to reach Client2");

            TestFrame request = new()
            {
                MessageId = "REQ1",
                FromUser = "Client1",
                IsRetrieval = true,
                RetrievalIds = ["M1"],
                Addresses = [new TestAddressEntry { UserName = "Server", Type = "To" }]
            };
            Assert.True(await client1.Service.Send("Server", request));

            await WaitUntil(() => !client1.Delivered.IsEmpty, "the stored copy to reach Client1");
            Assert.True(client1.Delivered.TryPeek(out TestFrame? copy));
            Assert.Equal(("M1", "Client1", "Hi"), (copy.MessageId, copy.FromUser, copy.Subject));
            Assert.Equal("Client1", Assert.Single(copy.Addresses).UserName);
            await Task.Delay(300);
            Assert.Single(client1.Delivered);
            Assert.Single(client2.Delivered);
        }
        finally
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
            db.Dispose();
            if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); }
        }
    }

    /// <summary>A connection whose certificate is not one of the server's children or a server in the cluster is refused: the server never counts it, and the client never reports it up.</summary>
    [Fact]
    public async Task ClientServer_UnknownCertificate_IsRefused()
    {
        (Dictionary<string, X509Certificate2> certificates, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("Server", "Client1", "Stranger");
        int serverPort = FreePort();
        await using Node server = new("Server", certificates["Server"], authorities, UserRole.Server, serverPort, [], OneServer("Client1"));
        await using Node stranger = new("Stranger", certificates["Stranger"], authorities, UserRole.Client, 0, [Local(serverPort)]);
        await using Node client1 = new("Client1", certificates["Client1"], authorities, UserRole.Client, 0, [Local(serverPort)]);

        await WaitUntil(() => server.IsUp("Client1") && client1.IsUp("Server"), "the known client to connect");
        await Task.Delay(500);

        Assert.False(stranger.IsUp("Server"));
        Assert.DoesNotContain(server.Status!.GetStatuses(), s => s.UserName == "Stranger");
        Assert.False(await stranger.Service.Send("Client1", MessageTo("Stranger", "Client1")));
    }

    /// <summary>Closing a client from the server drops it and keeps it out, and reopening lets it come back on its own.</summary>
    [Fact]
    public async Task ClientServer_CloseAndReopen_DropsThenReconnects()
    {
        (Dictionary<string, X509Certificate2> certificates, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("Server", "Client1");
        int serverPort = FreePort();
        await using Node server = new("Server", certificates["Server"], authorities, UserRole.Server, serverPort, [], OneServer("Client1"));
        await using Node client1 = new("Client1", certificates["Client1"], authorities, UserRole.Client, 0, [Local(serverPort)]);
        await WaitUntil(() => server.IsUp("Client1") && client1.IsUp("Server"), "the client to connect");

        server.Status!.SetClosed(PeerConnectionKind.Client, "Client1", true);

        await WaitUntil(() => !client1.IsUp("Server"), "the client to notice");
        Assert.False(server.IsUp("Client1"));
        Assert.True(server.Status.GetStatuses().Single(s => s.UserName == "Client1").IsClosed);

        server.Status.SetClosed(PeerConnectionKind.Client, "Client1", false);

        await WaitUntil(() => server.IsUp("Client1") && client1.IsUp("Server"), "the client to reconnect");
    }

    /// <summary>Two servers where only one is told about the other still route between their clients, since the connection one dialed carries traffic both ways.</summary>
    [Fact]
    public async Task ServerCluster_OneServerDialsTheOther_RoutesBetweenTheirClients()
    {
        (Dictionary<string, X509Certificate2> certificates, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("Server1", "Server2", "Client1", "Client2");
        int port1 = FreePort();
        int port2 = FreePort();
        Dictionary<string, ServerUserConfig> topology = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Server1"] = new ServerUserConfig { ChildClients = ["Client1"] },
            ["Server2"] = new ServerUserConfig { ChildClients = ["Client2"] }
        };
        await using Node server2 = new("Server2", certificates["Server2"], authorities, UserRole.Server, port2, [], topology);
        await using Node server1 = new("Server1", certificates["Server1"], authorities, UserRole.Server, port1, [Local(port2)], topology);
        await using Node client1 = new("Client1", certificates["Client1"], authorities, UserRole.Client, 0, [Local(port1)]);
        await using Node client2 = new("Client2", certificates["Client2"], authorities, UserRole.Client, 0, [Local(port2)]);
        await WaitUntil(() => server1.IsUp("Server2") && server2.IsUp("Server1") && client1.IsUp("Server1") && client2.IsUp("Server2"), "the whole cluster to connect");

        Assert.True(await client1.Service.Send("Client2", MessageTo("Client1", "Client2", "FROM-1")));
        Assert.True(await client2.Service.Send("Client1", MessageTo("Client2", "Client1", "FROM-2")));

        await WaitUntil(() => !client2.Delivered.IsEmpty && !client1.Delivered.IsEmpty, "both messages to arrive");
        Assert.Equal("FROM-1", client2.Delivered.Single().MessageId);
        Assert.Equal("FROM-2", client1.Delivered.Single().MessageId);
    }

    /// <summary>Peers that each list only the other's listener find out who is who from the certificates, and a peer that only listens can still reply over the connection it was dialed on.</summary>
    [Fact]
    public async Task Peers_OneDialsTheOther_MessagesFlowBothWays()
    {
        (Dictionary<string, X509Certificate2> certificates, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("Alice", "Bob");
        int bobPort = FreePort();
        await using Node bob = new("Bob", certificates["Bob"], authorities, UserRole.Peer, bobPort, []);
        await using Node alice = new("Alice", certificates["Alice"], authorities, UserRole.Peer, 0, [Local(bobPort)]);

        await WaitUntil(() => alice.Service.Send("Bob", MessageTo("Alice", "Bob", "TO-BOB")), "Alice to reach Bob");
        await WaitUntil(() => bob.Service.Send("Alice", MessageTo("Bob", "Alice", "TO-ALICE")), "Bob to reach Alice over the connection Alice opened");

        await WaitUntil(() => !bob.Delivered.IsEmpty && !alice.Delivered.IsEmpty, "both messages to arrive");
        Assert.Equal("TO-BOB", bob.Delivered.First().MessageId);
        Assert.Equal("TO-ALICE", alice.Delivered.First().MessageId);
    }

    /// <summary>A peer has no connection to a user nobody has identified, so sending to them fails rather than dialing anywhere.</summary>
    [Fact]
    public async Task Peers_SendToUserWithNoConnection_Fails()
    {
        (Dictionary<string, X509Certificate2> certificates, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("Alice");
        await using Node alice = new("Alice", certificates["Alice"], authorities, UserRole.Peer, 0, []);

        Assert.False(await alice.Service.Send("Nobody", MessageTo("Alice", "Nobody")));
    }
}
