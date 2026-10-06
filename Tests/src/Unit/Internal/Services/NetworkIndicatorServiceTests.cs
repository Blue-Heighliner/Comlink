namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="NetworkIndicator"/> and <see cref="NetworkIndicatorService"/>.</summary>
public sealed class NetworkIndicatorServiceTests : IAsyncLifetime
{
    private sealed class FakePeerService : IPeerService
    {
#pragma warning disable CS0067
        public event Func<object, Task>? FrameDelivered;
        public event Func<string, string, Task>? ReadReceiptReceived;
        public event Func<string, string, Task>? ReceiveReceiptReceived;
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067
        public event Func<string, Task>? UserConnected;
        public event Func<string, Task>? UserDisconnected;

        public HashSet<string> Connected { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool HasSubscribers => UserConnected is not null && UserDisconnected is not null;

        public IReadOnlyList<string> GetConnectedUsers() => [.. Connected];
        public bool IsUserConnected(string userName) => Connected.Contains(userName);
        public Task Start(CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default) => Task.FromResult(true);
        public Task DeliverLocal(object payload) => Task.CompletedTask;
        public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default) => Task.FromResult(true);

        public Task Disconnect(string userName)
        {
            Connected.Remove(userName);
            return UserDisconnected is null ? Task.CompletedTask : UserDisconnected(userName);
        }

        public Task Connect(string userName)
        {
            Connected.Add(userName);
            return UserConnected is null ? Task.CompletedTask : UserConnected(userName);
        }
    }

    private readonly FakePeerService peer = new();
    private readonly Mock<TestEngineController> controller = new() { CallBase = true };
    private readonly Mock<IEngineContextFactory> contexts = new();
    private readonly Mock<INetworkHandler> handler = new();
    private readonly NetworkIndicator indicator = new();
    private readonly CancellationTokenSource cancellation = new();
    private Task start = Task.CompletedTask;

    /// <summary>Sets up a client whose parent is RELAY, with no network processor.</summary>
    public Task InitializeAsync()
    {
        controller.SetupGet(c => c.Role).Returns(UserRole.Client);
        controller.SetupGet(c => c.ParentUser).Returns("RELAY");
        controller.SetupGet(c => c.NetworkHandler).Returns((INetworkHandler?)null);
        return Task.CompletedTask;
    }

    /// <summary>Stops the service.</summary>
    public async Task DisposeAsync()
    {
        await cancellation.CancelAsync();
        await start;
    }

    private async Task StartService()
    {
        NetworkIndicatorService service = new(peer, controller.Object, contexts.Object, indicator);
        start = service.Start(cancellation.Token);
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!peer.HasSubscribers && DateTime.UtcNow < deadline) { await Task.Delay(10); }
    }

    /// <summary>The indicator starts offline and only raises Changed when its state really changes.</summary>
    [Fact]
    public void Indicator_RaisesChangedOnlyOnChange()
    {
        List<bool> changes = [];
        indicator.Changed += changes.Add;

        Assert.False(indicator.IsOnline);
        indicator.Set(false);
        indicator.Set(true);
        indicator.Set(true);
        indicator.Set(false);

        Assert.Equal([true, false], changes);
    }

    /// <summary>By default the indicator follows the node's direct connection to its parent, and only that one.</summary>
    [Fact]
    public async Task Automatic_FollowsTheConnectionToTheParent()
    {
        await StartService();
        Assert.False(indicator.IsOnline);

        await peer.Connect("OTHER");
        Assert.False(indicator.IsOnline);

        await peer.Connect("relay");
        Assert.True(indicator.IsOnline);

        await peer.Disconnect("OTHER");
        Assert.True(indicator.IsOnline);

        await peer.Disconnect("RELAY");
        Assert.False(indicator.IsOnline);
    }

    /// <summary>A parent that is already connected when the service starts shows online straight away.</summary>
    [Fact]
    public async Task Automatic_ParentAlreadyConnected_StartsOnline()
    {
        peer.Connected.Add("RELAY");

        await StartService();

        Assert.True(indicator.IsOnline);
    }

    /// <summary>A server has no indicator, so nothing follows the connection.</summary>
    [Fact]
    public async Task Server_DoesNothing()
    {
        controller.SetupGet(c => c.Role).Returns(UserRole.Server);

        NetworkIndicatorService service = new(peer, controller.Object, contexts.Object, indicator);
        await service.Start(cancellation.Token);

        Assert.False(peer.HasSubscribers);
    }

    /// <summary>When the network processor takes the indicator over, the engine never sets it.</summary>
    [Fact]
    public async Task ProcessorTakingOver_DisablesTheAutomaticBehavior()
    {
        handler.Setup(h => h.UseAutomaticNetworkIndicator(It.IsAny<IEngineContext>())).Returns(false);
        controller.SetupGet(c => c.NetworkHandler).Returns(handler.Object);
        contexts.Setup(c => c.Create()).Returns(Mock.Of<IEngineContext>());

        NetworkIndicatorService service = new(peer, controller.Object, contexts.Object, indicator);
        await service.Start(cancellation.Token);
        await peer.Connect("RELAY");

        Assert.False(indicator.IsOnline);
        Assert.False(peer.HasSubscribers);
    }

    /// <summary>A processor that keeps the default leaves the automatic behavior on.</summary>
    [Fact]
    public async Task ProcessorKeepingTheDefault_KeepsTheAutomaticBehavior()
    {
        handler.Setup(h => h.UseAutomaticNetworkIndicator(It.IsAny<IEngineContext>())).Returns(true);
        controller.SetupGet(c => c.NetworkHandler).Returns(handler.Object);
        contexts.Setup(c => c.Create()).Returns(Mock.Of<IEngineContext>());

        await StartService();
        await peer.Connect("RELAY");

        Assert.True(indicator.IsOnline);
    }

    /// <summary>The context handed to a processor sets the indicator.</summary>
    [Fact]
    public void Context_SetNetworkIndicator_SetsTheIndicator()
    {
        NetworkUserContext context = new(Mock.Of<IEngineContext>(), "BOB", controller.Object, Mock.Of<IMessageRoutingService>(), indicator, LoggerFactory.Create(_ => { }).CreateLogger("t"));

        context.SetNetworkIndicator(true);
        Assert.True(indicator.IsOnline);

        context.SetNetworkIndicator(false);
        Assert.False(indicator.IsOnline);
    }
}
