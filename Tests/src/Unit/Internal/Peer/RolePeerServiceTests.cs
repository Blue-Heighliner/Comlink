namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="RolePeerService"/>: creating the implementation for the role when it starts, and replacing it on restart.</summary>
public sealed class RolePeerServiceTests
{
    private sealed class Harness
    {
        public Mock<IEngineController> Controller { get; } = new();
        public List<Mock<IPeerTransport>> Transports { get; } = [];
        public RolePeerService Service { get; }

        public Harness()
        {
            Controller.SetupGet(c => c.Role).Returns(UserRole.Server);
            Controller.SetupGet(c => c.PeerPort).Returns(1234);
            Controller.SetupGet(c => c.OutgoingPoints).Returns([]);
            Controller.SetupGet(c => c.Servers).Returns(new Dictionary<string, ServerUserConfig> { ["SERVER"] = new ServerUserConfig { Children = [] } });
            Mock<IPeerTransportFactory> factory = new();
            factory.Setup(f => f.Create()).Returns(() =>
            {
                Mock<IPeerTransport> transport = new();
                transport.SetupGet(t => t.Received).Returns(new TestObservable<PeerReceivedEventArgs>());
                transport.SetupGet(t => t.Connected).Returns(new TestObservable<PeerConnectionEventArgs>());
                transport.SetupGet(t => t.Disconnected).Returns(new TestObservable<PeerConnectionEventArgs>());
                Transports.Add(transport);
                return transport.Object;
            });
            ServiceCollection services = new();
            services.AddSingleton(factory.Object);
            Mock<ICurrentUserProvider> currentUser = new();
            currentUser.SetupGet(p => p.UserName).Returns("SERVER");
            services.AddSingleton(currentUser.Object);
            services.AddSingleton(Mock.Of<IMessageStorageService>());
            services.AddSingleton(Controller.Object);
            services.AddSingleton(LoggerFactory.Create(_ => { }));
            Service = new RolePeerService(services.BuildServiceProvider(), Controller.Object);
        }

        public async Task WaitFor(Func<bool> condition)
        {
            for (int attempt = 0; attempt < 200 && !condition(); attempt++) { await Task.Delay(10); }
            Assert.True(condition());
        }
    }

    /// <summary>Before Start there is nothing connected and nothing can be sent; Restart before Start does nothing.</summary>
    [Fact]
    public async Task BeforeStart_NothingIsConnected_AndRestartIsANoOp()
    {
        Harness harness = new();

        harness.Service.Restart();

        Assert.Empty(harness.Service.GetConnectedUsers());
        Assert.False(await harness.Service.Send("ALICE", new object()));
        Assert.Empty(harness.Transports);
    }

    /// <summary>Start creates the implementation for the current role, and Restart drops it and creates another, from the role as it is then.</summary>
    [Fact]
    public async Task Restart_ReplacesTheImplementation_FromTheCurrentRole()
    {
        Harness harness = new();
        using CancellationTokenSource stop = new();
        Task run = harness.Service.Start(stop.Token);
        await harness.WaitFor(() => harness.Transports.Count == 1);
        harness.Transports[0].Verify(t => t.StartListener(1234), Times.Once);

        harness.Controller.SetupGet(c => c.PeerPort).Returns(4321);
        harness.Service.Restart();

        await harness.WaitFor(() => harness.Transports.Count == 2);
        harness.Transports[0].Verify(t => t.DisposeAsync(), Times.Once);
        harness.Transports[1].Verify(t => t.StartListener(4321), Times.Once);
        Assert.False(run.IsCompleted);

        await stop.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>Each restart raises StatusesChanged so the connection tables rebuild from the new implementation.</summary>
    [Fact]
    public async Task Restart_RaisesStatusesChanged()
    {
        Harness harness = new();
        int raised = 0;
        harness.Service.StatusesChanged += () => Interlocked.Increment(ref raised);
        using CancellationTokenSource stop = new();
        Task run = harness.Service.Start(stop.Token);
        await harness.WaitFor(() => raised == 1);

        harness.Service.Restart();

        await harness.WaitFor(() => raised == 2);
        await stop.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
