namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="EngineHooksService"/> running the host's network processor.</summary>
public sealed class EngineHooksServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private sealed class RecordingHandler : INetworkHandler
    {
        public List<Action<INetworkUserContext>> Connected { get; } = [];
        public List<Action<INetworkUserContext>> Disconnected { get; } = [];
        public List<Action<INetworkFrameContext>> Received { get; } = [];

        public Task OnConnected(INetworkUserContext context)
        {
            foreach (Action<INetworkUserContext> action in Connected) { action(context); }
            return Task.CompletedTask;
        }

        public Task OnDisconnected(INetworkUserContext context)
        {
            foreach (Action<INetworkUserContext> action in Disconnected) { action(context); }
            return Task.CompletedTask;
        }

        public Task OnReceived(INetworkFrameContext context)
        {
            foreach (Action<INetworkFrameContext> action in Received) { action(context); }
            return Task.CompletedTask;
        }
    }

    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? FrameDelivered;
        public event Func<string, string, Task>? ReadReceiptReceived;
        public event Func<string, string, Task>? ReceiveReceiptReceived;
#pragma warning disable CS0067
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067
        public event Func<string, Task>? UserConnected;
        public event Func<string, Task>? UserDisconnected;

        public IReadOnlyList<string> ConnectedUsers { get; set; } = [];
        public List<(string UserName, object Packet)> SentPackets { get; } = [];
        public bool SendPacketResult { get; set; } = true;

        public IReadOnlyList<string> GetConnectedUsers() => ConnectedUsers;
        public bool IsUserConnected(string userName) => ConnectedUsers.Contains(userName, StringComparer.OrdinalIgnoreCase);
        public Task Start(CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default) => Task.FromResult(true);
        public Task DeliverLocal(object payload) => Task.CompletedTask;

        public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default)
        {
            SentPackets.Add((userName, packet));
            return Task.FromResult(SendPacketResult);
        }

        public bool HasUserConnectedSubscribers => UserConnected is not null;
        public bool HasUserDisconnectedSubscribers => UserDisconnected is not null;
        public bool HasMessageDeliveredSubscribers => FrameDelivered is not null;

        public Task FireUserConnected(string userName) => UserConnected is null ? Task.CompletedTask : UserConnected(userName);
        public Task FireUserDisconnected(string userName) => UserDisconnected is null ? Task.CompletedTask : UserDisconnected(userName);
        public Task FireMessageDelivered(object payload) => FrameDelivered is null ? Task.CompletedTask : FrameDelivered(payload);
        public Task FireReceiveReceiptReceived(string messageId, string user) => ReceiveReceiptReceived is null ? Task.CompletedTask : ReceiveReceiptReceived(messageId, user);
        public Task FireReadReceiptReceived(string messageId, string user) => ReadReceiptReceived is null ? Task.CompletedTask : ReadReceiptReceived(messageId, user);
    }

    private sealed class FakeMessageRoutingService : IMessageRoutingService
    {
#pragma warning disable CS0067
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067

        public List<(string FromUser, object Message)> RoutedMessages { get; } = [];
        public (string MessageId, IReadOnlyList<UserDeliveryResult> UserResults) Result { get; set; } = ("M1", []);

        public Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> Route(string fromUser, SendMessagePayload payload, CancellationToken cancellation)
            => Task.FromResult(Result);

        public Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> RouteFrame(string fromUser, object message, CancellationToken cancellation)
        {
            RoutedMessages.Add((fromUser, message));
            return Task.FromResult(Result);
        }
    }

    private static RecordingHandler Handler<T>(Mock<T> engineController) where T : class, IEngineController => (RecordingHandler)engineController.Object.NetworkHandler!;

    private static (EngineHooksService Service, FakePeerService Peer, Mock<TestEngineController> EngineController, Mock<IUserService> UserService, FakeMessageRoutingService Routing) Build()
    {
        FakePeerService peer = new();
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(e => e.NetworkHandler).Returns(new RecordingHandler());
        engineController.Setup(e => e.Users).Returns((IReadOnlyList<string>)[]);
        engineController.Setup(e => e.UserGroups).Returns(new Dictionary<string, IReadOnlyList<string>>());
        Mock<IUserService> userService = new();
        userService.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInfo { Name = "ME" });
        FakeMessageRoutingService routing = new();
        EngineHooksService service = new(peer, engineController.Object, userService.Object, routing, noLogger);
        return (service, peer, engineController, userService, routing);
    }

    /// <summary>With no processor configured, Start returns immediately without subscribing to any peer event.</summary>
    [Fact]
    public async Task Start_NoProcessorConfigured_ReturnsImmediatelyWithoutSubscribing()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        engineController.Setup(e => e.NetworkHandler).Returns((INetworkHandler?)null);

        await service.Start(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(peer.HasUserConnectedSubscribers);
        Assert.False(peer.HasUserDisconnectedSubscribers);
        Assert.False(peer.HasMessageDeliveredSubscribers);
    }

    /// <summary>With at least one hook configured, Start subscribes and blocks until cancelled.</summary>
    [Fact]
    public async Task Start_HooksConfigured_SubscribesAndBlocksUntilCancelled()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        Handler(engineController).Connected.AddRange([_ => { }]);
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        Assert.True(peer.HasUserConnectedSubscribers);
        Assert.False(startTask.IsCompleted);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(peer.HasUserConnectedSubscribers);
    }

    /// <summary>A UserConnected event runs the processor with a context carrying the connected user's name.</summary>
    [Fact]
    public async Task UserConnected_RunsEveryHookInOrderWithSameContext()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        List<string> calls = [];
        INetworkUserContext? firstContext = null;
        Action<INetworkUserContext> first = context => { firstContext = context; calls.Add($"first:{context.TargetUser}:{context.CurrentUser.Name}"); };
        Action<INetworkUserContext> second = context => calls.Add($"second:{context.TargetUser}:{ReferenceEquals(context, firstContext)}");
        Handler(engineController).Connected.AddRange([first, second]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        Assert.Equal(["first:ALICE:ME", "second:ALICE:True"], calls);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A UserDisconnected event runs every configured hook with the context's TargetUser set to who disconnected.</summary>
    [Fact]
    public async Task UserDisconnected_RunsEveryHook()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        List<string> calls = [];
        Handler(engineController).Disconnected.AddRange([context => calls.Add(context.TargetUser)]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserDisconnected("BOB");

        Assert.Equal(["BOB"], calls);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A processor that throws is logged and never thrown back into the peer service.</summary>
    [Fact]
    public async Task UserConnected_ProcessorThrows_IsLoggedNotThrown()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        Handler(engineController).Connected.Add(_ => throw new InvalidOperationException("boom"));
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        cts.Cancel();
        await startTask;
    }

    /// <summary>FrameDelivered hands every hook a context whose Message is the exact raw payload object.</summary>
    [Fact]
    public async Task MessageReceived_RunsEveryHookWithRawMessageObject()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        object? received = null;
        Handler(engineController).Received.AddRange([
            context => received = context.Frame
        ]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);
        TestFrame payload = new() { MessageId = "M1", FromUser = "SENDER", Subject = "Hi", Body = "Hello" };

        await peer.FireMessageDelivered(payload);

        Assert.Same(payload, received);
        cts.Cancel();
        await startTask;
    }

    /// <summary>The network processor is handed every received frame, including one that is not a message.</summary>
    [Fact]
    public async Task FrameReceived_NotAMessage_StillReachesTheProcessor()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        object? received = null;
        Handler(engineController).Received.Add(context => received = context.Frame);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        TestFrame payload = new() { MessageId = "F1", FromUser = "SENDER", IsHidden = true };

        await peer.FireMessageDelivered(payload);

        Assert.Same(payload, received);
        cts.Cancel();
        await startTask;
    }

    /// <summary>The context's Users/ConnectedUsers reflect IEngineController.Users/IPeerService.IsUserConnected, and each entry resolves its direct group membership from IEngineController.UserGroups.</summary>
    [Fact]
    public async Task Context_UsersAndConnectedUsers_ResolveDirectGroupMembership()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        engineController.Setup(e => e.Users).Returns((IReadOnlyList<string>)["Alice", "Bob"]);
        engineController.Setup(e => e.UserGroups).Returns(new Dictionary<string, IReadOnlyList<string>> { ["OPS"] = ["Alice"] });
        peer.ConnectedUsers = ["Alice"];
        INetworkUserContext? seen = null;
        Handler(engineController).Connected.AddRange([context => seen = context]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("Alice");

        Assert.NotNull(seen);
        List<UserInfo> users = [.. seen!.Users];
        Assert.Equal(2, users.Count);
        UserInfo alice = Assert.Single(users, u => u.Name == "Alice");
        Assert.Equal(["OPS"], alice.Groups);
        UserInfo bob = Assert.Single(users, u => u.Name == "Bob");
        Assert.Empty(bob.Groups);
        Assert.True(seen.IsConnected("Alice"));
        Assert.False(seen.IsConnected("Bob"));
        UserInfo onlyConnected = Assert.Single(seen.ConnectedUsers);
        Assert.Equal("Alice", onlyConnected.Name);
        cts.Cancel();
        await startTask;
    }

    /// <summary>
    /// A hook firing with no installed user (which should never actually happen, since EngineHooksService.Start
    /// only ever runs once one is installed) throws rather than handing a hook a broken context.
    /// </summary>
    [Fact]
    public async Task BuildContext_NoInstalledUser_Throws()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, Mock<IUserService> userService, _) = Build();
        userService.Setup(u => u.GetCurrentUserInfo()).Returns((UserInfo?)null);
        Handler(engineController).Connected.AddRange([_ => { }]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await Assert.ThrowsAsync<InvalidOperationException>(() => peer.FireUserConnected("ALICE"));

        cts.Cancel();
        await startTask;
    }

    /// <summary>Send rejects an object that is not an instance of the configured frame type, synchronously, before ever forking a background send.</summary>
    [Fact]
    public async Task Context_Send_WrongType_ThrowsSynchronously()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing) = Build();
        Handler(engineController).Connected.AddRange([
            context => Assert.Throws<ArgumentException>(() => context.Send("not a message"))
        ]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        Assert.Empty(routing.RoutedMessages);
        cts.Cancel();
        await startTask;
    }

    /// <summary>Send of a correctly-typed message is fire-and-forget - the hook returns immediately - but still routes it, from CurrentUser, in the background.</summary>
    [Fact]
    public async Task Context_Send_CorrectType_RoutesInBackgroundFromCurrentUser()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing) = Build();
        TestFrame message = new() { Subject = "Hi" };
        Handler(engineController).Connected.AddRange([
            context => context.Send(message)
        ]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        await WaitUntil(() => routing.RoutedMessages.Count > 0, TimeSpan.FromSeconds(30));
        Assert.Equal("ME", routing.RoutedMessages[0].FromUser);
        Assert.Same(message, routing.RoutedMessages[0].Message);
        cts.Cancel();
        await startTask;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(10);
        }
    }
}
