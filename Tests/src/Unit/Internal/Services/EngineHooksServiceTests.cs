namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="EngineHooksService"/> running the host's connection and message hooks.</summary>
public sealed class EngineHooksServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? MessageDelivered;
        public event Func<string, string, Task>? ConfirmationReceived;
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
        public bool HasMessageDeliveredSubscribers => MessageDelivered is not null;

        public Task FireUserConnected(string userName) => UserConnected is null ? Task.CompletedTask : UserConnected(userName);
        public Task FireUserDisconnected(string userName) => UserDisconnected is null ? Task.CompletedTask : UserDisconnected(userName);
        public Task FireMessageDelivered(object payload) => MessageDelivered is null ? Task.CompletedTask : MessageDelivered(payload);
        public Task FireConfirmationReceived(string messageId, string user) => ConfirmationReceived is null ? Task.CompletedTask : ConfirmationReceived(messageId, user);
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

        public Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> RouteMessage(string fromUser, object message, CancellationToken cancellation)
        {
            RoutedMessages.Add((fromUser, message));
            return Task.FromResult(Result);
        }
    }

    private static (EngineHooksService Service, FakePeerService Peer, Mock<TestEngineController> EngineController, Mock<IUserService> UserService, FakeMessageRoutingService Routing) Build()
    {
        FakePeerService peer = new();
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[]);
        engineController.Setup(e => e.UserDisconnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[]);
        engineController.Setup(e => e.MessageReceivedHooks).Returns((IReadOnlyList<Action<IMessageReceivedHookContext>>)[]);
        engineController.Setup(e => e.Users).Returns((IReadOnlyList<string>)[]);
        engineController.Setup(e => e.UserGroups).Returns(new Dictionary<string, IReadOnlyList<string>>());
        Mock<IUserService> userService = new();
        userService.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInfo { Name = "ME" });
        FakeMessageRoutingService routing = new();
        EngineHooksService service = new(peer, engineController.Object, userService.Object, routing, noLogger);
        return (service, peer, engineController, userService, routing);
    }

    /// <summary>With no hooks configured at all, Start returns immediately without subscribing to any peer event.</summary>
    [Fact]
    public async Task Start_NoHooksConfigured_ReturnsImmediatelyWithoutSubscribing()
    {
        (EngineHooksService service, FakePeerService peer, _, _, _) = Build();

        await service.Start(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(peer.HasUserConnectedSubscribers);
        Assert.False(peer.HasUserDisconnectedSubscribers);
        Assert.False(peer.HasMessageDeliveredSubscribers);
    }

    /// <summary>With at least one hook configured, Start subscribes and blocks until cancelled.</summary>
    [Fact]
    public async Task Start_HooksConfigured_SubscribesAndBlocksUntilCancelled()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[_ => { }]);
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        Assert.True(peer.HasUserConnectedSubscribers);
        Assert.False(startTask.IsCompleted);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(peer.HasUserConnectedSubscribers);
    }

    /// <summary>A UserConnected event runs every configured hook, in order, each handed the same context instance carrying the connected user's name.</summary>
    [Fact]
    public async Task UserConnected_RunsEveryHookInOrderWithSameContext()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        List<string> calls = [];
        IUserConnectionHookContext? firstContext = null;
        Action<IUserConnectionHookContext> first = context => { firstContext = context; calls.Add($"first:{context.TargetUser}:{context.CurrentUser.Name}"); };
        Action<IUserConnectionHookContext> second = context => calls.Add($"second:{context.TargetUser}:{ReferenceEquals(context, firstContext)}");
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[first, second]);
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
        engineController.Setup(e => e.UserDisconnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[context => calls.Add(context.TargetUser)]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserDisconnected("BOB");

        Assert.Equal(["BOB"], calls);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A hook that throws is logged and does not stop the remaining hooks from running.</summary>
    [Fact]
    public async Task UserConnected_HookThrows_OtherHooksStillRun()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        List<string> calls = [];
        Action<IUserConnectionHookContext> failing = _ => throw new InvalidOperationException("boom");
        Action<IUserConnectionHookContext> succeeding = context => calls.Add(context.TargetUser);
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[failing, succeeding]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        Assert.Equal(["ALICE"], calls);
        cts.Cancel();
        await startTask;
    }

    /// <summary>MessageDelivered hands every hook a context whose Message is the exact raw payload object.</summary>
    [Fact]
    public async Task MessageReceived_RunsEveryHookWithRawMessageObject()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        object? received = null;
        engineController.Setup(e => e.MessageReceivedHooks).Returns((IReadOnlyList<Action<IMessageReceivedHookContext>>)
        [
            context => received = context.Message
        ]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);
        TestMessage payload = new() { MessageId = "M1", FromUser = "SENDER", Subject = "Hi", Body = "Hello" };

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
        IUserConnectionHookContext? seen = null;
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[context => seen = context]);
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
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[_ => { }]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await Assert.ThrowsAsync<InvalidOperationException>(() => peer.FireUserConnected("ALICE"));

        cts.Cancel();
        await startTask;
    }

    /// <summary>SendMessage rejects an object that is not an instance of the configured message type, synchronously, before ever forking a background send.</summary>
    [Fact]
    public async Task Context_SendMessage_WrongType_ThrowsSynchronously()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing) = Build();
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)
        [
            context => Assert.Throws<ArgumentException>(() => context.SendMessage("not a message"))
        ]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        Assert.Empty(routing.RoutedMessages);
        cts.Cancel();
        await startTask;
    }

    /// <summary>SendMessage of a correctly-typed message is fire-and-forget - the hook returns immediately - but still routes it, from CurrentUser, in the background.</summary>
    [Fact]
    public async Task Context_SendMessage_CorrectType_RoutesInBackgroundFromCurrentUser()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing) = Build();
        TestMessage message = new() { Subject = "Hi" };
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)
        [
            context => context.SendMessage(message)
        ]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        await WaitUntil(() => routing.RoutedMessages.Count > 0, TimeSpan.FromSeconds(2));
        Assert.Equal("ME", routing.RoutedMessages[0].FromUser);
        Assert.Same(message, routing.RoutedMessages[0].Message);
        cts.Cancel();
        await startTask;
    }

    /// <summary>SendPacket throws when no packet type is configured at all.</summary>
    [Fact]
    public async Task Context_SendPacket_NoPacketTypeConfigured_Throws()
    {
        (EngineHooksService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _) = Build();
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)
        [
            context => Assert.Throws<ArgumentException>(() => context.SendPacket(new object(), "ALICE"))
        ]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        Assert.Empty(peer.SentPackets);
        cts.Cancel();
        await startTask;
    }

    /// <summary>With a packet type configured, SendPacket rejects an object that is not an instance of it.</summary>
    [Fact]
    public async Task Context_SendPacket_WrongType_Throws()
    {
        FakePeerService peer = new();
        Mock<TestPacketEngineController> engineController = new() { CallBase = true };
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)
        [
            context => Assert.Throws<ArgumentException>(() => context.SendPacket("not a packet", "ALICE"))
        ]);
        engineController.Setup(e => e.UserDisconnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[]);
        engineController.Setup(e => e.MessageReceivedHooks).Returns((IReadOnlyList<Action<IMessageReceivedHookContext>>)[]);
        Mock<IUserService> userService = new();
        userService.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInfo { Name = "ME" });
        EngineHooksService service = new(peer, engineController.Object, userService.Object, new FakeMessageRoutingService(), noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        Assert.Empty(peer.SentPackets);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A correctly-typed packet is sent, fire-and-forget, directly to every named user, bypassing IMessageRoutingService entirely.</summary>
    [Fact]
    public async Task Context_SendPacket_CorrectType_SendsToEachUserNameInBackground()
    {
        FakePeerService peer = new();
        Mock<TestPacketEngineController> engineController = new() { CallBase = true };
        TestPacket packet = new() { PayloadId = 1 };
        engineController.Setup(e => e.UserConnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)
        [
            context => context.SendPacket(packet, "ALICE", "BOB")
        ]);
        engineController.Setup(e => e.UserDisconnectedHooks).Returns((IReadOnlyList<Action<IUserConnectionHookContext>>)[]);
        engineController.Setup(e => e.MessageReceivedHooks).Returns((IReadOnlyList<Action<IMessageReceivedHookContext>>)[]);
        Mock<IUserService> userService = new();
        userService.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInfo { Name = "ME" });
        FakeMessageRoutingService routing = new();
        EngineHooksService service = new(peer, engineController.Object, userService.Object, routing, noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireUserConnected("ALICE");

        await WaitUntil(() => peer.SentPackets.Count == 2, TimeSpan.FromSeconds(2));
        Assert.Contains(peer.SentPackets, s => s.UserName == "ALICE" && ReferenceEquals(s.Packet, packet));
        Assert.Contains(peer.SentPackets, s => s.UserName == "BOB" && ReferenceEquals(s.Packet, packet));
        Assert.Empty(routing.RoutedMessages);
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
